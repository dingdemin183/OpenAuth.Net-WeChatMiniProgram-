using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenAuth.App.Warranty;
using OpenAuth.App.WxPay;
using System;
using System.Collections.Generic;
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
        private readonly WarrantyApp _warrantyApp;
        private readonly ILogger<PayCallbackController> _logger;
        private readonly CallBackService _callBackService;

        public PayCallbackController(
            WarrantyApp warrantyApp,
            ILogger<PayCallbackController> logger,
             CallBackService callBackService)
        {
            _warrantyApp = warrantyApp;
            _logger = logger;
            _callBackService = callBackService;
        }

        #region 微信支付异步回调（V3）
        /// <summary>
        /// 微信支付异步回调（V3）
        /// </summary>
        /// <returns></returns>
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
                    return Content("缺少必要的签名头", "application/json");
                }

                // 处理回调
                var result = await _callBackService.HandlePayCallbackAsync(
                    requestBody,
                    wechatpaySignature,
                    wechatpayTimestamp,
                    wechatpayNonce,
                    wechatpaySerial
                );

                if (!result.Success)
                {
                    _logger.LogWarning($"回调处理失败: {result.ErrorMessage}");
                    return StatusCode(400, new { code = "FAIL", message = result.ErrorMessage });
                }

                // 验签通过返回 200（无内容）
                return StatusCode(200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "支付回调处理异常");
                return StatusCode(500, new { code = "FAIL", message = "处理异常" });
            }
        }
        #endregion

        #region 微信退款 异步回调

        /// <summary>
        /// 微信支付异步回调（退款回调）
        /// </summary>
        /// <returns></returns>
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
                    return Content("缺少必要的签名头", "application/json");
                }

                // 处理退款回调
                var result = await _callBackService.HandleRefundCallbackAsync(
                    requestBody,
                    wechatpaySignature,
                    wechatpayTimestamp,
                    wechatpayNonce,
                    wechatpaySerial
                );

                if (!result.Success)
                {
                    _logger.LogWarning($"退款回调处理失败: {result.ErrorMessage}");
                    return StatusCode(400, new { code = "FAIL", message = result.ErrorMessage });
                }

                // 验签通过返回 200（无内容）
                return StatusCode(200);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "退款回调处理异常");
                return StatusCode(500, new { code = "FAIL", message = "处理异常" });
            }
        }
        #endregion 退款回调




    }
}