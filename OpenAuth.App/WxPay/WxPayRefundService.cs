
using System;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAuth.App.Response;
using SKIT.FlurlHttpClient.Wechat.TenpayV3;
using SKIT.FlurlHttpClient.Wechat.TenpayV3.Models;

namespace OpenAuth.App.WxPay
{
    /// <summary>
    /// 微信支付退款服务（使用 SKIT 库）
    /// </summary>
    public class WxPayRefundService
    {
        private readonly WechatTenpayClient _client;
        private readonly WeChatPaySetting _config;
        private readonly ILogger<WxPayRefundService> _logger;

        public WxPayRefundService(
            WechatTenpayClient client,
            IOptions<AppSetting> appConfiguration,
            ILogger<WxPayRefundService> logger)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _config = appConfiguration?.Value?.WeChatPay
                ?? throw new InvalidOperationException("微信支付配置未找到");
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// 生成商户退款单号：RF + 订单号 + 时间戳后4位 + 随机数
        /// </summary>
        public string GenerateRefundNo(string orderNo)
        {
            var timestamp = DateTime.Now.Ticks.ToString().Substring(12, 4);
            var random = new Random().Next(100, 999).ToString();
            return $"RF{orderNo}{timestamp}{random}";
        }

        /// <summary>
        /// V3 退款申请（使用 SKIT 库）
        /// </summary>
        public async Task<RefundResp> CreateRefundAsync(RefundReq req)
        {
            // 参数校验
            if (req == null)
                throw new ArgumentNullException(nameof(req));
            if (string.IsNullOrEmpty(req.TransactionId))
                throw new ArgumentException("微信订单号不能为空");
            if (string.IsNullOrEmpty(req.OutRefundNo))
                throw new ArgumentException("商户退款单号不能为空");
            if (req.Amount?.Refund <= 0)
                throw new ArgumentException("退款金额必须大于0");

            try
            {
                // 构建 SKIT 退款请求对象
                var request = new CreateRefundDomesticRefundRequest()
                {
                    TransactionId = req.TransactionId,
                    OutRefundNumber = req.OutRefundNo,
                    Reason = req.Reason,
                    NotifyUrl = req.NotifyUrl ?? _config.RefundNotifyUrl ?? _config.NotifyUrl,
                    Amount = new CreateRefundDomesticRefundRequest.Types.Amount()
                    {
                        Refund = req.Amount.Refund,
                        Total = req.Amount.Total,
                        Currency = req.Amount.Currency ?? "CNY"
                    }
                };

                _logger.LogInformation("开始申请退款，商户退款单号：{OutRefundNo}，金额：{Amount}分",
                    req.OutRefundNo, req.Amount.Refund);

                //  调用 SKIT 退款接口
                var response = await _client.ExecuteCreateRefundDomesticRefundAsync(request);

                if (response.IsSuccessful())
                {
                    _logger.LogInformation("退款申请成功，微信退款单号：{RefundId}，状态：{Status}",
                        response.RefundId, response.Status);

                    //  转换为你的原有响应类
                    return new RefundResp
                    {
                        RefundId = response.RefundId,
                        OutRefundNo = response.OutRefundNumber,
                        OutTradeNo = response.OutTradeNumber,
                        TransactionId = response.TransactionId,
                        Status = response.Status,
                        CreateTime = response.CreateTime.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                        Amount = response.Amount != null ? new RefundAmountResp
                        {
                            Refund = response.Amount.Refund,
                            Total = response.Amount.Total,
                            Currency = response.Amount.Currency,
                            PayerRefund = response.Amount.PayerRefund,
                            PayerTotal = response.Amount.PayerTotal
                        } : null
                    };
                }
                else
                {
                    _logger.LogError("退款申请失败：{ErrorCode} - {ErrorMessage}，退款单号：{OutRefundNo}",
                        response.ErrorCode, response.ErrorMessage, req.OutRefundNo);
                    throw new Exception($"退款失败：{response.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "退款申请异常，退款单号：{OutRefundNo}", req.OutRefundNo);
                throw;
            }
        }

        /// <summary>
        /// 查询退款（通过商户退款单号）
        /// </summary>
        public async Task<RefundResp> QueryRefundByOutRefundNoAsync(string outRefundNo)
        {
            if (string.IsNullOrEmpty(outRefundNo))
                throw new ArgumentException("商户退款单号不能为空");

            try
            {
                var request = new GetRefundDomesticRefundByOutRefundNumberRequest()
                {
                    OutRefundNumber = outRefundNo
                };

                var response = await _client.ExecuteGetRefundDomesticRefundByOutRefundNumberAsync(request);

                if (response.IsSuccessful())
                {
                    return new RefundResp
                    {
                        RefundId = response.RefundId,
                        OutRefundNo = response.OutRefundNumber,
                        OutTradeNo = response.OutTradeNumber,
                        TransactionId = response.TransactionId,
                        Status = response.Status,
                        CreateTime = response.CreateTime.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                        Amount = response.Amount != null ? new RefundAmountResp
                        {
                            Refund = response.Amount.Refund,
                            Total = response.Amount.Total,
                            Currency = response.Amount.Currency,
                            PayerRefund = response.Amount.PayerRefund,
                            PayerTotal = response.Amount.PayerTotal
                        } : null
                    };
                }
                else
                {
                    _logger.LogError("查询退款失败：{ErrorCode} - {ErrorMessage}",
                        response.ErrorCode, response.ErrorMessage);
                    throw new Exception($"查询退款失败：{response.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "查询退款异常，退款单号：{OutRefundNo}", outRefundNo);
                throw;
            }
        }

        
    }
}




//using Infrastructure;
//using Microsoft.Extensions.Logging;
//using Microsoft.Extensions.Options;
//using OpenAuth.App.Response;
//using OpenAuth.App.WxPay;
//using System.Net.Http;
//using System.Text.Json;
//using System.Text;
//using System.Threading.Tasks;
//using System;

//public class WxPayRefundService
//{
//    private readonly WeChatPayV3Signer _signer;
//    private readonly IHttpClientFactory _httpClientFactory;
//    private readonly ILogger<WxPayRefundService> _logger;
//    private readonly WeChatPaySetting _config;

//    public WxPayRefundService(
//        WeChatPayV3Signer signer,
//        IHttpClientFactory httpClientFactory,
//        ILogger<WxPayRefundService> logger,
//        IOptions<AppSetting> appConfiguration)
//    {
//        _signer = signer;
//        _httpClientFactory = httpClientFactory;
//        _logger = logger;
//        _config = appConfiguration.Value.WeChatPay;
//    }

//    /// <summary>
//    /// 生成商户退款单号：RF + 订单号 + 时间戳后4位
//    /// </summary>
//    public string GenerateRefundNo(string orderNo)
//    {
//        var timestamp = DateTime.Now.Ticks.ToString().Substring(12, 4);
//        var random = new Random().Next(100, 999).ToString();
//        return $"RF{orderNo}{timestamp}{random}";
//    }

//    /// <summary>
//    /// V3 退款申请（不需要双向证书）
//    /// </summary>
//    public async Task<RefundResp> CreateRefundAsync(RefundReq req)
//    {
//        // 参数校验
//        if (req == null) throw new ArgumentNullException(nameof(req));
//        if (string.IsNullOrEmpty(req.TransactionId))
//            throw new ArgumentException("微信订单号不能为空");
//        if (string.IsNullOrEmpty(req.OutRefundNo))
//            throw new ArgumentException("商户退款单号不能为空");
//        if (req.Amount?.Refund <= 0)
//            throw new ArgumentException("退款金额必须大于0");

//        // 构建请求体（JSON格式）
//        var requestBody = new
//        {
//            transaction_id = req.TransactionId,
//            out_refund_no = req.OutRefundNo,
//            reason = req.Reason,
//            notify_url = req.NotifyUrl ?? _config.NotifyUrl,
//            amount = new
//            {
//                refund = req.Amount.Refund,
//                total = req.Amount.Total,
//                currency = req.Amount.Currency ?? "CNY"
//            }
//        };

//        var bodyJson = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
//        {
//            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
//            WriteIndented = false
//        });

//        // 生成签名（ V3方式，用Authorization头）
//        var nonceStr = WeChatPayV3Signer.GenerateNonceStr();
//        var timestamp = WeChatPayV3Signer.GenerateTimestamp();
//        var url = "/v3/refund/domestic/refunds";
//        var method = "POST";

//        var authorization = _signer.GenerateAuthorization(method, url, bodyJson, nonceStr, timestamp);

//        //  发送请求 - 不需要 p12 证书，用普通 HttpClient 即可
//        var client = _httpClientFactory.CreateClient();
//        client.DefaultRequestHeaders.Add("Accept", "application/json");
//        client.DefaultRequestHeaders.Add("Authorization", authorization);
//        client.DefaultRequestHeaders.Add("User-Agent", "WeChatPay-V3/1.0");

//        var content = new StringContent(bodyJson, Encoding.UTF8, "application/json");

//        var response = await client.PostAsync(
//            "https://api.mch.weixin.qq.com/v3/refund/domestic/refunds",
//            content);

//        var responseContent = await response.Content.ReadAsStringAsync();

//        if (!response.IsSuccessStatusCode)
//        {
//            _logger.LogError($"退款失败：{responseContent}");
//            throw new Exception($"退款失败：{responseContent}");
//        }

//        var result = JsonSerializer.Deserialize<RefundResp>(responseContent, new JsonSerializerOptions
//        {
//            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
//        });

//        return result;
//    }
//}