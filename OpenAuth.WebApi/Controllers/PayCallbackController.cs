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
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> WeChatPayNotify()
        {
            string requestBody;
            using (var reader = new StreamReader(Request.Body))
            {
                requestBody = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            // 验签所需的 HTTP Headers
            var wechatpaySignature = Request.Headers["Wechatpay-Signature"].ToString();
            var wechatpayTimestamp = Request.Headers["Wechatpay-Timestamp"].ToString();
            var wechatpayNonce = Request.Headers["Wechatpay-Nonce"].ToString();
            var wechatpaySerial = Request.Headers["Wechatpay-Serial"].ToString();

            // 检查必要的 Header（验签失败返回 4xx，微信不会重试）
            if (string.IsNullOrEmpty(wechatpaySignature) ||
                string.IsNullOrEmpty(wechatpayTimestamp) ||
                string.IsNullOrEmpty(wechatpayNonce) ||
                string.IsNullOrEmpty(wechatpaySerial))
            {
                _logger.LogWarning("支付回调缺少必要的签名头");
                return StatusCode(400, new { code = "FAIL", message = "缺少必要的签名头" });
            }

            // 验签：失败返回 4xx，微信不重试
            var isValid = _client.VerifyEventSignature(
                webhookTimestamp: wechatpayTimestamp,
                webhookNonce: wechatpayNonce,
                webhookBody: requestBody,
                webhookSignature: wechatpaySignature,
                webhookSerialNumber: wechatpaySerial
            );

            if (!isValid)
            {
                _logger.LogWarning("支付回调验签失败");
                return StatusCode(401, new { code = "FAIL", message = "验签失败" });
            }

            // 反序列化为事件
            var callbackModel = _client.DeserializeEvent(requestBody);

            // 只处理 TRANSACTION.SUCCESS
            if (!"TRANSACTION.SUCCESS".Equals(callbackModel.EventType))
            {
                _logger.LogWarning("收到未处理的支付事件类型：{EventType}", callbackModel.EventType);
                // 未处理的事件类型返回 204，让微信不再重试
                return StatusCode(204);
            }

            // 解密资源
            var payData = _client.DecryptEventResource<SKIT.FlurlHttpClient.Wechat.TenpayV3.Events.TransactionResource>(callbackModel);

            _logger.LogInformation("收到支付成功回调：商户订单号={OutTradeNo}，微信交易单号={TransactionId}，金额={Amount}分，支付时间={SuccessTime}",
                payData.OutTradeNumber,
                payData.TransactionId,
                payData.Amount?.Total,
                payData.SuccessTime);

            // 调用业务服务处理，根据结果决定返回码
            CallbackProcessResult result;
            try
            {
                result = await _callBackService.ProcessPaymentAsync(payData).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // 兜底：业务层未捕获的异常，按临时性失败处理，让微信重试
                _logger.LogError(ex, "支付回调业务处理抛出异常：订单号={OrderNo}", payData.OutTradeNumber);
                return StatusCode(500, new { code = "FAIL", message = "处理异常" });
            }

            // 根据业务结果决定状态码：
            // - Success / AlreadyProcessed / PermanentFailure → 2xx，微信不再重试
            // - TemporaryFailure → 500，微信会重试
            if (result.ShouldRetry)
            {
                _logger.LogWarning("支付回调业务临时失败，将让微信重试：{OrderNo}，{Message}",
                    payData.OutTradeNumber, result.Message);
                return StatusCode(500, new { code = "FAIL", message = result.Message });
            }

            if (!result.IsSuccess)
            {
                // 永久性失败（订单不存在、金额不一致等），返回 200 + 失败原因
                // 不让微信重试（重试结果相同），由对账系统或人工补偿
                _logger.LogError("【支付回调永久失败告警】{OrderNo}，{Message}，请人工对账",
                    payData.OutTradeNumber, result.Message);
                return StatusCode(200, new { code = "FAIL", message = result.Message });
            }

            // 成功或幂等已处理
            return StatusCode(204);
        }

        #endregion

        #region 微信退款异步回调

        /// <summary>
        /// 微信退款异步回调
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> WeChatRefundNotify()
        {
            string requestBody;
            using (var reader = new StreamReader(Request.Body))
            {
                requestBody = await reader.ReadToEndAsync().ConfigureAwait(false);
            }

            // 验签所需的 HTTP Headers
            var wechatpaySignature = Request.Headers["Wechatpay-Signature"].ToString();
            var wechatpayTimestamp = Request.Headers["Wechatpay-Timestamp"].ToString();
            var wechatpayNonce = Request.Headers["Wechatpay-Nonce"].ToString();
            var wechatpaySerial = Request.Headers["Wechatpay-Serial"].ToString();

            if (string.IsNullOrEmpty(wechatpaySignature) ||
                string.IsNullOrEmpty(wechatpayTimestamp) ||
                string.IsNullOrEmpty(wechatpayNonce) ||
                string.IsNullOrEmpty(wechatpaySerial))
            {
                _logger.LogWarning("退款回调缺少必要的签名头");
                return StatusCode(400, new { code = "FAIL", message = "缺少必要的签名头" });
            }

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
                return StatusCode(401, new { code = "FAIL", message = "验签失败" });
            }

            var callbackModel = _client.DeserializeEvent(requestBody);

            // 退款事件类型：REFUND.SUCCESS / REFUND.ABNORMAL / REFUND.CLOSED
            if (!"REFUND.SUCCESS".Equals(callbackModel.EventType) &&
                !"REFUND.ABNORMAL".Equals(callbackModel.EventType) &&
                !"REFUND.CLOSED".Equals(callbackModel.EventType))
            {
                _logger.LogWarning("收到未处理的退款事件类型：{EventType}", callbackModel.EventType);
                return StatusCode(204);
            }

            var refundData = _client.DecryptEventResource<SKIT.FlurlHttpClient.Wechat.TenpayV3.Events.RefundResource>(callbackModel);

            _logger.LogInformation("收到退款回调：商户退款单号={OutRefundNo}，微信退款单号={RefundId}，事件={EventType}",
                refundData.OutRefundNumber,
                refundData.RefundId,
                callbackModel.EventType);

            CallbackProcessResult result;
            try
            {
                result = await _callBackService.ProcessRefundAsync(refundData, callbackModel.EventType).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "退款回调业务处理抛出异常：退款单号={RefundNo}",
                    refundData.OutRefundNumber);
                return StatusCode(500, new { code = "FAIL", message = "处理异常" });
            }

            if (result.ShouldRetry)
            {
                _logger.LogWarning("退款回调业务临时失败，将让微信重试：{RefundNo}，{Message}",
                    refundData.OutRefundNumber, result.Message);
                return StatusCode(500, new { code = "FAIL", message = result.Message });
            }

            if (!result.IsSuccess)
            {
                _logger.LogError("【退款回调永久失败告警】退款单号={RefundNo}，{Message}，请人工对账",
                    refundData.OutRefundNumber, result.Message);
                return StatusCode(200, new { code = "FAIL", message = result.Message });
            }

            return StatusCode(204);
        }

        #endregion
    }
}
