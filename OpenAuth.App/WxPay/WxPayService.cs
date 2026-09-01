using Infrastructure;
using Microsoft.Extensions.Options;
using OpenAuth.App.Request;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OpenAuth.App.WxPay
{
    /// <summary>
    /// 微信支付服务
    /// </summary>
    public class WxPayService
    {
        private readonly IOptions<AppSetting> _appConfiguration;
        private readonly IHttpClientFactory _httpClientFactory;

        public WxPayService(
            IOptions<AppSetting> appConfiguration,
            IHttpClientFactory httpClientFactory)
        {
            _appConfiguration = appConfiguration;
            _httpClientFactory = httpClientFactory;
        }

        /// <summary>
        /// 统一下单
        /// </summary>
        public async Task<WeChatPayResultResp> UnifiedOrderAsync(WeChatPayUnifiedOrderReq req)
        {
            var config = _appConfiguration.Value.WeChatPay;

            // 构建请求参数
            var param = new SortedDictionary<string, string>
            {
                ["appid"] = config.AppId,
                ["mch_id"] = config.MchId,
                ["nonce_str"] = GenerateNonceStr(),
                ["body"] = req.Body,
                ["out_trade_no"] = req.OutTradeNo,
                ["total_fee"] = req.TotalFee.ToString(),
                ["spbill_create_ip"] = req.SpbillCreateIp,
                ["notify_url"] = req.NotifyUrl,
                ["trade_type"] = req.TradeType,
                ["openid"] = req.OpenId
            };

            // 生成签名
            var sign = GenerateSign(param, config.ApiKey);
            param["sign"] = sign;

            // 构建XML请求体
            var xmlBody = BuildXmlBody(param);

            // 发送请求
            var client = _httpClientFactory.CreateClient();
            var content = new StringContent(xmlBody, Encoding.UTF8, "application/xml");
            var response = await client.PostAsync("https://api.mch.weixin.qq.com/pay/unifiedorder", content);

            var xmlResult = await response.Content.ReadAsStringAsync();
            var result = ParseXmlResult(xmlResult);

            if (result["return_code"] != "SUCCESS")
            {
                throw new Exception($"统一下单失败：{result.GetValueOrDefault("return_msg", "未知错误")}");
            }

            if (result["result_code"] != "SUCCESS")
            {
                throw new Exception($"统一下单失败：{result.GetValueOrDefault("err_code_des", "未知错误")}");
            }

            // 生成前端调起支付参数
            var prepayId = result["prepay_id"];
            var payResult = BuildPayResult(prepayId, config);

            return payResult;
        }

        /// <summary>
        /// 构建前端调起支付参数
        /// </summary>
        public WeChatPayResultResp BuildPayResult(string prepayId, WeChatPaySetting config)
        {
            var timeStamp = DateTimeOffset.Now.ToUnixTimeSeconds().ToString();
            var nonceStr = GenerateNonceStr();
            var package = $"prepay_id={prepayId}";

            var param = new SortedDictionary<string, string>
            {
                ["appId"] = config.AppId,
                ["timeStamp"] = timeStamp,
                ["nonceStr"] = nonceStr,
                ["package"] = package,
                ["signType"] = "MD5"
            };

            var paySign = GenerateSign(param, config.ApiKey);

            return new WeChatPayResultResp
            {
                AppId = config.AppId,
                TimeStamp = timeStamp,
                NonceStr = nonceStr,
                Package = package,
                SignType = "MD5",
                PaySign = paySign
            };
        }

        /// <summary>
        /// 验证支付回调签名
        /// </summary>
        public bool VerifyCallbackSign(SortedDictionary<string, string> param, string apiKey)
        {
            if (!param.ContainsKey("sign"))
                return false;

            var sign = param["sign"];
            param.Remove("sign");
            var computedSign = GenerateSign(param, apiKey);
            param["sign"] = sign;

            return sign == computedSign;
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
            var sign = BitConverter.ToString(bytes).Replace("-", "").ToUpper();

            return sign;
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