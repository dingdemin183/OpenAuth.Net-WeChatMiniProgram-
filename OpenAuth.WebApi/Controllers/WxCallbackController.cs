using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAuth.App.UserProfile;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OpenAuth.WebApi.Controllers
{
    /// <summary>
    /// 微信消息推送回调处理
    /// </summary>
    [Route("api/wx/callback")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [ApiController]
    public class WxCallbackController : ControllerBase
    {
        private readonly ILogger<WxCallbackController> _logger;
        private readonly UserProfileApp _userProfileApp;
        private readonly IOptions<AppSetting> _appConfiguration;

        public WxCallbackController(
            ILogger<WxCallbackController> logger,
            UserProfileApp userProfileApp,
            IOptions<AppSetting> appConfiguration)
        {
            _logger = logger;
            _userProfileApp = userProfileApp;
            _appConfiguration = appConfiguration;
        }

        /// <summary>
        /// 微信服务器验证（GET请求）
        /// </summary>
        [HttpGet]
        [IgnoreAntiforgeryToken]
        [AllowAnonymous]
        public IActionResult Verify(
            [FromQuery] string signature,
            [FromQuery] string timestamp,
            [FromQuery] string nonce,
            [FromQuery] string echostr)
        {
            try
            {
                var token = _appConfiguration.Value.WeChatMiniProgram?.Token;
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogError("WeChatMiniProgram:Token 未配置");
                    return BadRequest("Token未配置");
                }

                _logger.LogInformation("收到微信验证请求: signature={Signature}, timestamp={Timestamp}, nonce={Nonce}",
                    signature, timestamp, nonce);

                // 验证签名
                if (!CheckSignature(signature, timestamp, nonce, token))
                {
                    _logger.LogWarning("签名验证失败");
                    return BadRequest("签名验证失败");
                }

                // 验证通过，返回 echostr
                _logger.LogInformation("验证通过，返回 echostr: {Echostr}", echostr);
                return Content(echostr, "text/plain");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "验证请求处理异常");
                return BadRequest("验证失败");
            }
        }

        /// <summary>
        /// 接收微信推送的消息/事件（POST请求）
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> ReceiveCallback()
        {
            try
            {
                //  读取请求体
                string requestBody;
                using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                {
                    requestBody = await reader.ReadToEndAsync();
                }

                _logger.LogDebug("收到微信回调: {RequestBody}", requestBody);

                // 解析消息（微信推送的是XML格式）
                var xmlDoc = XDocument.Parse(requestBody);
                var root = xmlDoc.Root;

                // 提取关键字段
                var msgType = root.Element("MsgType")?.Value;
                var eventType = root.Element("Event")?.Value;

                // 处理不同消息类型
                if (msgType == "event" && eventType == "wxa_media_check")
                {
                    // 媒体内容安全审核结果事件
                    await HandleMediaCheckResultAsync(root);
                }
                else
                {
                    // 其他消息类型，记录日志
                    _logger.LogInformation("收到未处理的消息类型: MsgType={MsgType}, Event={Event}", msgType, eventType);
                }

                // 返回 success（微信要求返回空字符串或 success）
                return Content("success", "text/plain");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理微信回调异常");
                // 即使处理失败，也要返回 success，否则微信会重复推送
                return Content("success", "text/plain");
            }
        }

        /// <summary>
        /// 处理图片/音频审核结果
        /// </summary>
        private async Task HandleMediaCheckResultAsync(XElement root)
        {
            try
            {
                // 提取审核结果
                var traceId = root.Element("trace_id")?.Value;

                // 获取 errcode（0表示成功）
                var errCode = root.Element("errcode")?.Value;
                if (errCode != "0")
                {
                    _logger.LogWarning("审核任务失败, trace_id: {TraceId}, errcode: {ErrCode}", traceId, errCode);
                    return;
                }

                // 获取结果
                var resultElement = root.Element("result");
                if (resultElement == null)
                {
                    _logger.LogWarning("未找到 result 节点, trace_id: {TraceId}", traceId);
                    return;
                }

                var suggest = resultElement.Element("suggest")?.Value; // pass, risky, review
                var label = resultElement.Element("label")?.Value;

                _logger.LogInformation(
                    "收到审核结果: trace_id={TraceId}, suggest={Suggest}, label={Label}",
                    traceId, suggest, label);

                // 调用业务层处理审核结果
                await _userProfileApp.HandleMediaCheckResultAsync(traceId, suggest);

                // 如果是 review（疑似违规需人工复核），可以发送通知或记录日志
                if (suggest == "review")
                {
                    _logger.LogWarning("内容疑似违规，需人工复核, trace_id: {TraceId}, label: {Label}", traceId, label);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理审核结果异常, trace_id: {TraceId}", root.Element("trace_id")?.Value);
                throw;
            }
        }

        /// <summary>
        /// 验证微信签名
        /// </summary>
        private bool CheckSignature(string signature, string timestamp, string nonce, string token)
        {
            try
            {
                // 将 token、timestamp、nonce 三个参数进行字典序排序
                var arr = new[] { token, timestamp, nonce };
                Array.Sort(arr);
                var str = string.Concat(arr);

                // 使用 SHA1 加密
                using var sha1 = SHA1.Create();
                var hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(str));
                var computedSignature = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

                return computedSignature == signature;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "签名验证异常");
                return false;
            }
        }
    }
}