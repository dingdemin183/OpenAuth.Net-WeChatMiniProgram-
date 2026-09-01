using Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using OpenAuth.App.Warranty;
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

        public PayCallbackController(
            WarrantyApp warrantyApp,
            ILogger<PayCallbackController> logger)
        {
            _warrantyApp = warrantyApp;
            _logger = logger;
        }

        /// <summary>
        /// 微信支付异步回调
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<string> WeChatPayNotify()
        {
            try
            {
                // 读取XML请求体
                var xmlBody = await new StreamReader(Request.Body).ReadToEndAsync();

                // 解析XML
                var callbackData = ParseXml(xmlBody);

                _logger.LogInformation($"收到微信支付回调：{xmlBody}");

                // 处理回调
                var success = await _warrantyApp.HandlePayCallbackAsync(callbackData);

                if (success)
                {
                    // 返回成功标识给微信
                    return "<xml><return_code><![CDATA[SUCCESS]]></return_code><return_msg><![CDATA[OK]]></return_msg></xml>";
                }
                else
                {
                    return "<xml><return_code><![CDATA[FAIL]]></return_code><return_msg><![CDATA[处理失败]]></return_msg></xml>";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "支付回调处理异常");
                return "<xml><return_code><![CDATA[FAIL]]></return_code><return_msg><![CDATA[" + ex.Message + "]]></return_msg></xml>";
            }
        }

        /// <summary>
        /// 解析XML为字典
        /// </summary>
        private SortedDictionary<string, string> ParseXml(string xml)
        {
            var result = new SortedDictionary<string, string>();
            var doc = new System.Xml.XmlDocument();
            doc.LoadXml(xml);

            foreach (System.Xml.XmlNode node in doc.DocumentElement.ChildNodes)
            {
                result[node.Name] = node.InnerText;
            }

            return result;
        }
    }
}