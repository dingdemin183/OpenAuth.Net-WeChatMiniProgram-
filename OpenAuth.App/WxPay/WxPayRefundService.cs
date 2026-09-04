// OpenAuth.App/WxPay/WxPayRefundService.cs

using Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAuth.App.Response;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;

namespace OpenAuth.App.WxPay
{ 

    /// <summary>
    /// 微信退款服务
    /// </summary>
    public class WxPayRefundService
    {
        private readonly IOptions<AppSetting> _appConfiguration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WxPayRefundService> _logger;
        private WeChatPayV3Signer _signer;

        public WxPayRefundService(
            IOptions<AppSetting> appConfiguration,
            IHttpClientFactory httpClientFactory,
            ILogger<WxPayRefundService> logger,
            WeChatPayV3Signer signer)
        {
            _appConfiguration = appConfiguration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _signer = signer;
        }
        /// <summary>
        /// 获取签名器
        /// </summary>
        private WeChatPayV3Signer GetSigner()
        {
            if (_signer == null)
            {
                var config = _appConfiguration.Value.WeChatPay;
                _signer = new WeChatPayV3Signer(
                    config.PrivateKeyPath,
                    config.MchId,
                    config.SerialNo
                );
            }
            return _signer;
        }


        /// <summary>
        /// 加载商户证书（用于双向证书认证）
        /// </summary>
        private X509Certificate2 LoadMerchantCertificate(WeChatPaySetting config)
        {
            try
            {
                var certPath = config.CertPath;

                // 如果配置的路径不存在，尝试在 Cert 目录下查找
                if (!System.IO.File.Exists(certPath))
                {
                    var basePath = AppDomain.CurrentDomain.BaseDirectory;
                    var fallbackPath = System.IO.Path.Combine(basePath, "Cert", "apiclient_cert.p12");
                    if (System.IO.File.Exists(fallbackPath))
                    {
                        certPath = fallbackPath;
                    }
                    else
                    {
                        throw new FileNotFoundException($"证书文件不存在: {certPath}");
                    }
                }

                // V3 退款需要使用 p12 证书，密码通常为商户号
                return new X509Certificate2(certPath, config.MchId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载商户证书失败");
                throw new Exception($"加载商户证书失败: {ex.Message}");
            }
        }
        /// <summary>
        /// V3 退款申请（需要双向证书认证）
        /// </summary>
        /// <param name="req">退款请求参数</param>
        /// <param name="config">微信支付配置</param>
        /// <returns></returns>
        public async Task<RefundResp> CreateRefundAsync(RefundReq req, WeChatPaySetting config)
        {
            try
            {
                // 参数校验
                if (req == null)
                    throw new ArgumentNullException(nameof(req));
                if (string.IsNullOrEmpty(req.TransactionId))
                    throw new ArgumentException("微信订单号不能为空");
                if (string.IsNullOrEmpty(req.OutRefundNo))
                    throw new ArgumentException("商户退款单号不能为空");
                if (req.Amount == null || req.Amount.Refund <= 0)
                    throw new ArgumentException("退款金额必须大于0");

                var signer = GetSigner();
                

                // 构建请求体
                var requestBody = new
                {
                    transaction_id = req.TransactionId,
                    out_refund_no = req.OutRefundNo,
                    reason = req.Reason,
                    notify_url = req.NotifyUrl ?? config.NotifyUrl,
                    amount = new
                    {
                        refund = req.Amount.Refund,
                        total = req.Amount.Total,
                        currency = req.Amount.Currency ?? "CNY"
                    }
                };

                // 序列化为 JSON（必须是一行，与签名保持一致）
                var bodyJson = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = false
                });

                // 生成签名参数
                var nonceStr = WeChatPayV3Signer.GenerateNonceStr();
                var timestamp = WeChatPayV3Signer.GenerateTimestamp();
                var url = "/v3/refund/domestic/refunds";
                var method = "POST";

                // 生成 Authorization 头
                var authorization = signer.GenerateAuthorization(method, url, bodyJson, nonceStr, timestamp);

                // 使用带证书的 HttpClient 发送请求
                var response = await SendRefundRequestV3Async(bodyJson, authorization, config);

                // 解析响应
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"退款失败（HTTP {response.StatusCode}）：{responseContent}");
                    throw new Exception($"退款失败：{responseContent}");
                }

                var result = JsonSerializer.Deserialize<RefundResp>(responseContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (string.IsNullOrEmpty(result?.RefundId))
                {
                    throw new Exception($"退款失败：未获取到 refund_id，响应：{responseContent}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"退款申请失败：退款单号 {req?.OutRefundNo}");
                throw;
            }
        }

        /// <summary>
        /// 发送 V3 退款请求（使用双向证书认证）
        /// </summary>
        private async Task<HttpResponseMessage> SendRefundRequestV3Async(string bodyJson, string authorization, WeChatPaySetting config)
        {
            try
            {
                // 加载证书（双向证书认证）
                var certificate = LoadMerchantCertificate(config);

                var handler = new HttpClientHandler();
                handler.ClientCertificates.Add(certificate);
                // 忽略证书链验证（生产环境建议启用）
                // handler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                using var client = new HttpClient(handler);
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Authorization", authorization);
                client.DefaultRequestHeaders.Add("User-Agent", "WeChatPay-V3");

                var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

                var url = "https://api.mch.weixin.qq.com/v3/refund/domestic/refunds";
                var response = await client.PostAsync(url, content);

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "发送退款请求失败");
                throw;
            }
        }
        

        /// <summary>
        /// 生成商户退款单号：RF + 订单号 + 时间戳后4位
        /// </summary>
        public string GenerateRefundNo(string orderNo)
        {
            var timestamp = DateTime.Now.Ticks.ToString().Substring(12, 4);
            var random = new Random().Next(100, 999).ToString();
            return $"RF{orderNo}{timestamp}{random}";
        }

        /// <summary>
        /// 生成随机字符串
        /// </summary>
        private string GenerateNonceStr()
        {
            var chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            var result = new char[32];
            for (int i = 0; i < 32; i++)
            {
                result[i] = chars[random.Next(chars.Length)];
            }
            return new string(result);
        }

        /// <summary>
        /// 生成签名
        /// </summary>
        private string GenerateSign(SortedDictionary<string, string> param, string apiKey)
        {
            var sb = new StringBuilder();
            foreach (var kv in param)
            {
                if (!string.IsNullOrEmpty(kv.Value))
                {
                    sb.Append($"{kv.Key}={kv.Value}&");
                }
            }
            sb.Append($"key={apiKey}");

            using var md5 = MD5.Create();
            var bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return BitConverter.ToString(bytes).Replace("-", "").ToUpper();
        }

        /// <summary>
        /// 构建XML请求体
        /// </summary>
        private string BuildXmlBody(SortedDictionary<string, string> param)
        {
            var sb = new StringBuilder("<xml>");
            foreach (var kv in param)
            {
                sb.Append($"<{kv.Key}>{kv.Value}</{kv.Key}>");
            }
            sb.Append("</xml>");
            return sb.ToString();
        }

        /// <summary>
        /// 解析XML结果
        /// </summary>
        private Dictionary<string, string> ParseXmlResult(string xml)
        {
            var result = new Dictionary<string, string>();
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            foreach (XmlNode node in doc.DocumentElement.ChildNodes)
            {
                result[node.Name] = node.InnerText;
            }

            return result;
        }
    }
}