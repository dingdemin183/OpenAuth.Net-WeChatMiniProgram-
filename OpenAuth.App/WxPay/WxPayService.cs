
using Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAuth.App.Request;
using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenAuth.App.WxPay
{
    /// <summary>
    /// 微信支付服务（V3 版）
    /// </summary>
    public class WxPayService
    {
        private readonly IOptions<AppSetting> _appConfiguration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<WxPayService> _logger;
        private readonly WeChatPayV3Signer _signer;

        public WxPayService(
            IOptions<AppSetting> appConfiguration,
            IHttpClientFactory httpClientFactory,
            ILogger<WxPayService> logger,
            WeChatPayV3Signer signer)
        {
            _appConfiguration = appConfiguration ?? throw new ArgumentNullException(nameof(appConfiguration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _signer = signer ?? throw new ArgumentNullException(nameof(signer));
        }

        /// <summary>
        /// V3 统一下单（JSAPI/小程序）
        /// </summary>
        /// <param name="req">请求参数</param>
        /// <param name="openId">用户openid</param>
        /// <param name="outTradeNo">商户订单号</param>
        /// <returns></returns>
        public async Task<WeChatPayResp> UnifiedOrderAsync(CreateWarrantyPayOrderReq req, string openId, string outTradeNo)
        {
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            if (req.Amount <= 0)
                throw new ArgumentException("支付金额必须大于0");
            if (string.IsNullOrEmpty(openId))
                throw new ArgumentException("用户openid不能为空");
            if (string.IsNullOrEmpty(outTradeNo))
                throw new ArgumentException("商户订单号不能为空");

            try
            {
                var config = _appConfiguration.Value.WeChatPay;
                if (config == null)
                    throw new InvalidOperationException("微信支付配置未找到");

                // 商品描述
                var description = $"延保-{req.ProductBrand}{req.ProductType}";

                // 构建请求参数
                var requestBody = new UnifiedOrderReq
                {
                    AppId = config.AppId,
                    MchId = config.MchId,
                    Description = description,
                    OutTradeNo = outTradeNo,
                    NotifyUrl = config.NotifyUrl,
                    Amount = new WeChatPayV3Amount
                    {
                        Total = (int)(req.Amount * 100)
                    },
                    Payer = new WeChatPayV3Payer
                    {
                        OpenId = openId
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
                var url = "/v3/pay/transactions/jsapi";
                var method = "POST";

                // 生成 Authorization 头
                var authorization = _signer.GenerateAuthorization(method, url, bodyJson, nonceStr, timestamp);

                // 发送请求
                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("Accept", "application/json");
                client.DefaultRequestHeaders.Add("Authorization", authorization);

                var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
                var response = await client.PostAsync($"https://api.mch.weixin.qq.com{url}", content);

                var responseContent = await response.Content.ReadAsStringAsync();

                // 判断业务结果
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("统一下单失败（HTTP {StatusCode}）：{ResponseContent}", response.StatusCode, responseContent);
                    throw new Exception($"统一下单失败：{responseContent}");
                }

                var result = JsonSerializer.Deserialize<UnifiedOrderResp>(responseContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (string.IsNullOrEmpty(result?.PrepayId))
                {
                    throw new Exception($"统一下单失败：未获取到 prepay_id，响应：{responseContent}");
                }

                // 生成前端调起支付参数
                var payResult = BuildPayResult(result.PrepayId, config);

                return payResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "统一下单异常，订单号：{OutTradeNo}", outTradeNo);
                throw;
            }
        }

        /// <summary>
        /// 构建前端调起支付参数（V3 版，RSA 签名）
        /// </summary>
        public WeChatPayResp BuildPayResult(string prepayId, WeChatPaySetting config)
        {
            var timeStamp = WeChatPayV3Signer.GenerateTimestamp();
            var nonceStr = WeChatPayV3Signer.GenerateNonceStr();
            var package = $"prepay_id={prepayId}";
            var signType = "RSA";

            // 构建待签名串（按 V3 调起支付要求）
            // 签名串格式：appId\n时间戳\n随机串\npackage\n
            var signStr = $"{config.AppId}\n{timeStamp}\n{nonceStr}\n{package}\n";

            var paySign = _signer.Sign(signStr);

            return new WeChatPayResp
            {
                AppId = config.AppId,
                TimeStamp = timeStamp,
                NonceStr = nonceStr,
                Package = package,
                SignType = signType,
                PaySign = paySign
            };
        }

        public object TestSign()
        {
            try
            {
                //  直接使用 Base64 字符串，不经过 PEM 清理 
                // 这是从 PEM 中提取的纯 Base64 字符串（已去掉所有标记和换行）
                var base64PrivateKey = "MIIEvAIBADANBgkqhkiG9w0BAQEFAASCBKYwggSiAgEAAoIBAQCm2mb6q8gMKH/3\r\nCNTbpJAIrbqiBiQGEOtjGcBrDYltsGynWgNscqT7WvfzU14FQbYcQUC5T4Wvva7m\r\ni3fIp3OgX8VqMDNA0qebnr38Pe6kqiLyZgFpJPXlSKDyPyqhRbVTbXssvSMQeVKc\r\ndXeVxoNNeoOlNFHgF/P0io6AmAVnz+hN8SiZKuOsth5/zUTLGvtkgxBcQooQrtXh\r\nRcpLT798OyIb9xeJ2HO3xRtMv2+perEzb4gMibI74UBz+2QEbnkubPE+2jU2rRZu\r\ndnNEz/BPOt3Qj/w2V6/G0VumGDh6+UeMU0jv4aupHztWITC4Akn0l7lBCNy3lgl8\r\nVFaJnkIxAgMBAAECggEAYGL8aESB7NwciDWW2UdoWUsa7GxFtSdjAz2mFXGdeTsY\r\nmVh7b9OOkRGM+Qio4LqEHDBp1mMk5E/cUJwy1zw8pGGO5nfvs7u9TT3XnHaefIs4\r\nYvUgTYAneIuLRkXNN5rQU+CD7mVYczTSz0Vgjqo9wa1LjUz7G0xbBmJgTdMEFGJs\r\neJjy6AbJo0CGIwp6HJbTm4CmOUgXnnDAIbEGTIRImkZFH/rzneIeR7oZ77FVwxr1\r\nCZB2gfRCov/yRPbw8vnryYkmvQ7D/ze3j5097vRg/MoDGBSdoOwcmo75vyofr0AS\r\nzytMjmHYyifqkf5slPropSiJeGf4p/7gtKyF6dE/XQKBgQDVAlJ+4U5ZVGOuDc3+\r\nsAhz8CTzgFNlq9vKuSoFK6hOz2L+cwj+E7NXGkOe2DsHHZNy2Xqxk7caKhPEp1z9\r\nhhpMpyLVMoFt6CKemyoRBWDCQwLLwem9SZF/IAyovBkLiH36P42Jm26gUkNMKC/5\r\nZhtqxf6RZgRQzbVudJi47vIRCwKBgQDIh0+v27Oo+DM3fhObH4I1NrXpWOEGH7OQ\r\nG1dEsMuFYF4hjGhg0kBEP3w9vVdl2+mRllZKTsx9oqjb8OibPLLIH8xsdbAB0WLf\r\nJvjLu4wl/ILUzN1RI03dWnnv2EnEeQn6c3hizvrJ9wR5U4ue9RPVnQooJ0hZF1PU\r\nuCL5fWK3MwKBgElReU/PAYbh80WP3t3Rfbdaa32dKBeQ5iCLR5lsA4zM+YgX1HqQ\r\nEWTj126vgvHaDkyz6vWAoL/Sx+cirHFfXWIRDX5Q2hgYlQH+6qXdMgbrxeSYpHnQ\r\n/tHBGFpkFELSAnrGsVMyOwvYBO4LzyeLK9i+ufcWJFoj1FVmsMLHDG8tAoGARdbi\r\niQQCoYG4DMarO2aQ6cmhN6EN1h0qY7EyBqlwaIZ0okiNfdMcMOjPc41DKCWcRmlO\r\nqlihXcxN9TQFPzO3rH1urAOdBjUPs1qWYhZyrDQyuLyVBBJApyxAtajloDjrob+f\r\nmQIvVDHk7ACN6xG+E7K6+9salnTKbJapD618uQMCgYBNy6XUvzLkP/A1U/UZdtcx\r\nl8GwU/dturLxz4CyGbqDw4ubaYY2e13lnqHUqQgPtiSyH51nq3tdo8G0YAJdfkSv\r\nKvnfslW91fyEBUKnkdW1o3/1UFU/wprZ7ixVL/F42A4xDu7OFE8EnweJOZ0jWceE\r\nOdhCkaIGBCfRnlECRK8UyQ==";

                // 验证 Base64 字符串是否有效
                try
                {
                    var testBytes = Convert.FromBase64String(base64PrivateKey);
                    _logger.LogInformation($"Base64 解码成功，长度: {testBytes.Length} 字节");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Base64 字符串无效: {ex.Message}");
                    throw;
                }

                var privateKeyBytes = Convert.FromBase64String(base64PrivateKey);
                using var rsa = RSA.Create();
                rsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);

                // 微信官方测试数据
                var mchId = "1900007291";
                var serialNo = "408B07E79B8269FEC3D5D3E6AB8ED163A6A380DB";
                var method = "POST";
                var url = "/v3/pay/transactions/jsapi";
                var timestamp = "1554208460";
                var nonceStr = "593BEC0C930BF1AFEB40B4A08C8FB242";
                var body = "{\"appid\":\"wxd678efh567hg6787\",\"mchid\":\"1900007291\",\"description\":\"Image形象店-深圳腾大-QQ公仔\",\"out_trade_no\":\"1217752501201407033233368018\",\"notify_url\":\"https://www.weixin.qq.com/wxpay/pay.php\",\"amount\":{\"total\":100,\"currency\":\"CNY\"},\"payer\":{\"openid\":\"oUpF8uMuAJO_M2pxb1Q9zNjWeS6o\"}}";

                var expectedSignature = "jnks4dlrPw3ZX+ozVvSK39oa0t7OMBsg83BHAwd8BRdUFiVaQNTLTvci+wURgP1OQBbKYhFGvt7iqYpDSTQkp7Uq1sltaQKyncCyrA1g88m5bsKERQfPyT0ahSwKTYJ1CAn9QiJuSJRq1QsQs07eehbU/k9BCS51jTyc1Jpsi2H77HF9f/BnjXAOP3/sPObg6V5Ee4EzwLox684hhuMuIwHo7D8KFk3LIHOKDcNI4It1aCXydFWNpNK+SG86VUDe5kwoDpw4Ulqfu9z8OFDGbDs9TCxEv8iqQzbpxOlEVoOe2kalSYM5kApQb3nZcxdUtoE0liJGW3RGUNE0t4v01A==";

                // 构造签名串（注意：最后有一个换行符）
                var signatureStr = $"{method}\n{url}\n{timestamp}\n{nonceStr}\n{body}\n";

                // 计算签名
                var data = Encoding.UTF8.GetBytes(signatureStr);
                var signedBytes = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var actualSignature = Convert.ToBase64String(signedBytes);

                // 生成 Authorization 头
                var authorization = $"WECHATPAY2-SHA256-RSA2048 mchid=\"{mchId}\",nonce_str=\"{nonceStr}\",signature=\"{actualSignature}\",timestamp=\"{timestamp}\",serial_no=\"{serialNo}\"";

                // 记录日志
                _logger.LogInformation("===== 微信支付V3签名测试 =====");
                _logger.LogInformation($"签名串: {signatureStr.Replace("\n", "\\n")}");
                _logger.LogInformation($"期望签名: {expectedSignature}");
                _logger.LogInformation($"实际签名: {actualSignature}");
                _logger.LogInformation($"签名匹配: {expectedSignature == actualSignature}");

                return new
                {
                    SignatureString = signatureStr.Replace("\n", "\\n"),
                    ExpectedSignature = expectedSignature,
                    ActualSignature = actualSignature,
                    IsMatch = expectedSignature == actualSignature,
                    Authorization = authorization,
                    Note = expectedSignature == actualSignature
                        ? "✅ 签名生成正确！"
                        : "❌ 签名不匹配，请检查签名串格式"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "签名测试失败");
                return new
                {
                    Error = ex.Message,
                    StackTrace = ex.StackTrace
                };
            }
        }
    }
}