// OpenAuth.App/SSO/WxPhoneRequest.cs

using System.Text.Json.Serialization;

namespace OpenAuth.App.SSO
{
    /// <summary>
    /// 获取手机号请求
    /// </summary>
    public class WxPhoneRequest
    {
        /// <summary>
        /// 手机号获取凭证（从 getPhoneNumber 回调中获取）
        /// </summary>
        public string Code { get; set; }
    }

    /// <summary>
    /// 微信获取手机号接口返回
    /// </summary>
    public class WxPhoneResponse
    {
        [JsonPropertyName("errcode")]
        public int ErrCode { get; set; }

        [JsonPropertyName("errmsg")]
        public string ErrMsg { get; set; }

        [JsonPropertyName("phone_info")]
        public WxPhoneInfo PhoneInfo { get; set; }
    }

    public class WxPhoneInfo
    {
        [JsonPropertyName("phoneNumber")]
        public string PhoneNumber { get; set; }

        [JsonPropertyName("purePhoneNumber")]
        public string PurePhoneNumber { get; set; }

        [JsonPropertyName("countryCode")]
        public string CountryCode { get; set; }

        [JsonPropertyName("watermark")]
        public WxWatermark Watermark { get; set; }
    }

    public class WxWatermark
    {
        [JsonPropertyName("timestamp")]
        public long Timestamp { get; set; }

        [JsonPropertyName("appid")]
        public string AppId { get; set; }
    }
}