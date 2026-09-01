// OpenAuth.App/SSO/WxPhoneRequest.cs

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
        public int ErrCode { get; set; }
        public string ErrMsg { get; set; }
        public WxPhoneInfo PhoneInfo { get; set; }
    }

    public class WxPhoneInfo
    {
        public string PhoneNumber { get; set; }
        public string PurePhoneNumber { get; set; }
        public string CountryCode { get; set; }
        public WxWatermark Watermark { get; set; }
    }

    public class WxWatermark
    {
        public long Timestamp { get; set; }
        public string AppId { get; set; }
    }
}