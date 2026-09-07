using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenAuth.App.WxPay;
using SKIT.FlurlHttpClient.Wechat.TenpayV3;
using System;
using System.IO;
using System.Threading.Tasks;

namespace OpenAuth.WebApi.Controllers
{
    /// <summary>
    /// 支付回调接口（不需要认证，微信服务器调用）
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "支付回调_PayCallback")]
    public class PayCallbackController : ControllerBase
    {
        private readonly WechatTenpayClient _client;
        private readonly CallBackService _callBackService;
        private readonly ILogger<PayCallbackController> _logger;

        public PayCallbackController(
            WechatTenpayClient client,
            CallBackService callBackService,
            ILogger<PayCallbackController> logger)
        {
            _client = client;
            _callBackService = callBackService;
            _logger = logger;
        }

        #region 微信支付异步回调（V3）

        /// <summary>
        /// 微信支付异步回调（V3）
        /// </summary>
        [HttpPost("WeChatPayNotify")]
        [IgnoreAntiforgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> WeChatPayNotify()
        {
            try
            {
                // 读取请求体原始内容
                var requestBody = await new StreamReader(Request.Body).ReadToEndAsync();

                _logger.LogInformation($"收到微信支付V3回调，Body: {requestBody}");

                // 获取验签所需的 HTTP Headers
                var wechatpaySignature = Request.Headers["Wechatpay-Signature"].ToString();
                var wechatpayTimestamp = Request.Headers["Wechatpay-Timestamp"].ToString();
                var wechatpayNonce = Request.Headers["Wechatpay-Nonce"].ToString();
                var wechatpaySerial = Request.Headers["Wechatpay-Serial"].ToString();

                // 检查必要的 Header
                if (string.IsNullOrEmpty(wechatpaySignature) ||
                    string.IsNullOrEmpty(wechatpayTimestamp) ||
                    string.IsNullOrEmpty(wechatpayNonce) ||
                    string.IsNullOrEmpty(wechatpaySerial))
                {
                    _logger.LogWarning("回调请求缺少必要的签名头");
                    return StatusCode(400, new { code = "FAIL", message = "缺少必要的签名头" });
                }

                //正确的验签方法：VerifyEventSignature
                var isValid = _client.VerifyEventSignature(
                    webhookTimestamp: wechatpayTimestamp,
                    webhookNonce: wechatpayNonce,
                    webhookBody: requestBody,
                    webhookSignature: wechatpaySignature,
                    webhookSerialNumber: wechatpaySerial
                );

                if (!isValid)
                {
                    _logger.LogWarning("回调验签失败");
                    return StatusCode(400, new { code = "FAIL", message = "验签失败" });
                }

                // 先反序列化为 WechatTenpayEvent
                var callbackModel = _client.DeserializeEvent(requestBody);

                // 根据 EventType 判断事件类型，再解密资源
                if ("TRANSACTION.SUCCESS".Equals(callbackModel.EventType))
                {
                    var payData = _client.DecryptEventResource<SKIT.FlurlHttpClient.Wechat.TenpayV3.Events.TransactionResource>(callbackModel);

                    _logger.LogInformation($"支付成功，商户订单号: {payData.OutTradeNumber}, 微信交易单号: {payData.TransactionId}");

                    // 调用业务服务处理
                    await _callBackService.ProcessPaymentAsync(payData);
                }
                else
                {
                    _logger.LogWarning($"收到未处理的支付事件类型: {callbackModel.EventType}");
                }

                // 微信支付要求成功返回 200 或 204，无需返回报文体
                return StatusCode(204);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "支付回调处理异常");
                return StatusCode(500, new { code = "FAIL", message = "处理异常" });
            }
        }

        #endregion

        #region 微信退款异步回调

        /// <summary>
        /// 微信退款异步回调
        /// </summary>
        [HttpPost("WeChatRefundNotify")]
        [IgnoreAntiforgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> WeChatRefundNotify()
        {
            try
            {
                // 读取请求体原始内容
                var requestBody = await new StreamReader(Request.Body).ReadToEndAsync();

                _logger.LogInformation($"收到微信支付退款回调，Body: {requestBody}");

                // 获取验签所需的 HTTP Headers
                var wechatpaySignature = Request.Headers["Wechatpay-Signature"].ToString();
                var wechatpayTimestamp = Request.Headers["Wechatpay-Timestamp"].ToString();
                var wechatpayNonce = Request.Headers["Wechatpay-Nonce"].ToString();
                var wechatpaySerial = Request.Headers["Wechatpay-Serial"].ToString();

                // 检查必要的 Header
                if (string.IsNullOrEmpty(wechatpaySignature) ||
                    string.IsNullOrEmpty(wechatpayTimestamp) ||
                    string.IsNullOrEmpty(wechatpayNonce) ||
                    string.IsNullOrEmpty(wechatpaySerial))
                {
                    _logger.LogWarning("退款回调请求缺少必要的签名头");
                    return StatusCode(400, new { code = "FAIL", message = "缺少必要的签名头" });
                }

                // 正确的验签方法
                var isValid = _client.VerifyEventSignature(
                    webhookTimestamp: wechatpayTimestamp,
                    webhookNonce: wechatpayNonce,
                    webhookBody: requestBody,
                    webhookSignature: wechatpaySignature,
                    webhookSerialNumber: wechatpaySerial
                );

                if (!isValid)
                {
                    _logger.LogWarning("退款回调验签失败");
                    return StatusCode(400, new { code = "FAIL", message = "验签失败" });
                }

                // 解析事件
                var callbackModel = _client.DeserializeEvent(requestBody);

                // 退款事件类型：REFUND.SUCCESS / REFUND.ABNORMAL / REFUND.CLOSED
                if ("REFUND.SUCCESS".Equals(callbackModel.EventType) ||
                    "REFUND.ABNORMAL".Equals(callbackModel.EventType) ||
                    "REFUND.CLOSED".Equals(callbackModel.EventType))
                {
                    var refundData = _client.DecryptEventResource<SKIT.FlurlHttpClient.Wechat.TenpayV3.Events.RefundResource>(callbackModel);

                    _logger.LogInformation($"退款回调处理，商户退款单号: {refundData.OutRefundNumber}, 微信退款单号: {refundData.RefundId}, 状态: {callbackModel.EventType}");

                    // 调用业务服务处理
                    await _callBackService.ProcessRefundAsync(refundData, callbackModel.EventType);
                }
                else
                {
                    _logger.LogWarning($"收到未处理的退款事件类型: {callbackModel.EventType}");
                }

                // 返回 204
                return StatusCode(204);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "退款回调处理异常");
                return StatusCode(500, new { code = "FAIL", message = "处理异常" });
            }
        }

        #endregion
    }
}






//using Infrastructure;
//using Microsoft.AspNetCore.Authorization;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Extensions.Logging;
//using OpenAuth.App.Warranty;
//using OpenAuth.App.WxPay;
//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Threading.Tasks;

//namespace OpenAuth.WebApi.Controllers
//{
//    /// <summary>
//    /// 支付回调接口（不需要认证，微信服务器调用）
//    /// </summary>
//    [Route("api/[controller]/[action]")]
//    [ApiController]
//    [ApiExplorerSettings(GroupName = "支付回调_PayCallback")]
//    public class PayCallbackController : ControllerBase
//    {
//        private readonly WarrantyApp _warrantyApp;
//        private readonly ILogger<PayCallbackController> _logger;
//        private readonly CallBackService _callBackService;

//        public PayCallbackController(
//            WarrantyApp warrantyApp,
//            ILogger<PayCallbackController> logger,
//             CallBackService callBackService)
//        {
//            _warrantyApp = warrantyApp;
//            _logger = logger;
//            _callBackService = callBackService;
//        }

//        #region 微信支付异步回调（V3）
//        /// <summary>
//        /// 微信支付异步回调（V3）
//        /// </summary>
//        /// <returns></returns>
//        [HttpPost("WeChatPayNotify")]
//        [IgnoreAntiforgeryToken]
//        [AllowAnonymous]
//        public async Task<IActionResult> WeChatPayNotify()
//        {
//            try
//            {
//                // 读取请求体原始内容
//                var requestBody = await new StreamReader(Request.Body).ReadToEndAsync();

//                _logger.LogInformation($"收到微信支付V3回调，Body: {requestBody}");

//                // 获取验签所需的 HTTP Headers
//                var wechatpaySignature = Request.Headers["Wechatpay-Signature"].ToString();
//                var wechatpayTimestamp = Request.Headers["Wechatpay-Timestamp"].ToString();
//                var wechatpayNonce = Request.Headers["Wechatpay-Nonce"].ToString();
//                var wechatpaySerial = Request.Headers["Wechatpay-Serial"].ToString();

//                // 检查必要的 Header
//                if (string.IsNullOrEmpty(wechatpaySignature) ||
//                    string.IsNullOrEmpty(wechatpayTimestamp) ||
//                    string.IsNullOrEmpty(wechatpayNonce) ||
//                    string.IsNullOrEmpty(wechatpaySerial))
//                {
//                    _logger.LogWarning("回调请求缺少必要的签名头");
//                    return Content("缺少必要的签名头", "application/json");
//                }

//                // 处理回调
//                var result = await _callBackService.HandlePayCallbackAsync(
//                    requestBody,
//                    wechatpaySignature,
//                    wechatpayTimestamp,
//                    wechatpayNonce,
//                    wechatpaySerial
//                );

//                if (!result.Success)
//                {
//                    _logger.LogWarning($"回调处理失败: {result.ErrorMessage}");
//                    return StatusCode(400, new { code = "FAIL", message = result.ErrorMessage });
//                }

//                // 验签通过返回 200（无内容）
//                return StatusCode(200);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "支付回调处理异常");
//                return StatusCode(500, new { code = "FAIL", message = "处理异常" });
//            }
//        }
//        #endregion

//        #region 微信退款 异步回调

//        /// <summary>
//        /// 微信支付异步回调（退款回调）
//        /// </summary>
//        /// <returns></returns>
//        [HttpPost("WeChatRefundNotify")]
//        [IgnoreAntiforgeryToken]
//        [AllowAnonymous]
//        public async Task<IActionResult> WeChatRefundNotify()
//        {
//            try
//            {
//                // 读取请求体原始内容
//                var requestBody = await new StreamReader(Request.Body).ReadToEndAsync();

//                _logger.LogInformation($"收到微信支付退款回调，Body: {requestBody}");

//                // 获取验签所需的 HTTP Headers
//                var wechatpaySignature = Request.Headers["Wechatpay-Signature"].ToString();
//                var wechatpayTimestamp = Request.Headers["Wechatpay-Timestamp"].ToString();
//                var wechatpayNonce = Request.Headers["Wechatpay-Nonce"].ToString();
//                var wechatpaySerial = Request.Headers["Wechatpay-Serial"].ToString();

//                // 检查必要的 Header
//                if (string.IsNullOrEmpty(wechatpaySignature) ||
//                    string.IsNullOrEmpty(wechatpayTimestamp) ||
//                    string.IsNullOrEmpty(wechatpayNonce) ||
//                    string.IsNullOrEmpty(wechatpaySerial))
//                {
//                    _logger.LogWarning("退款回调请求缺少必要的签名头");
//                    return Content("缺少必要的签名头", "application/json");
//                }

//                // 处理退款回调
//                var result = await _callBackService.HandleRefundCallbackAsync(
//                    requestBody,
//                    wechatpaySignature,
//                    wechatpayTimestamp,
//                    wechatpayNonce,
//                    wechatpaySerial
//                );

//                if (!result.Success)
//                {
//                    _logger.LogWarning($"退款回调处理失败: {result.ErrorMessage}");
//                    return StatusCode(400, new { code = "FAIL", message = result.ErrorMessage });
//                }

//                // 验签通过返回 200（无内容）
//                return StatusCode(200);
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "退款回调处理异常");
//                return StatusCode(500, new { code = "FAIL", message = "处理异常" });
//            }
//        }
//        #endregion 退款回调




//    }
//}