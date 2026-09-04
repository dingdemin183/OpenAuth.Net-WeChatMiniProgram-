// OpenAuth.App/SSO/WxMiniProgramLoginRequest.cs

using Infrastructure;
using System.Text.Json.Serialization;

namespace OpenAuth.App.SSO
{
    /// <summary>
    /// 微信小程序一键登录请求参数
    /// </summary>
    public class WxMiniProgramLoginWithPhoneRequest
    {
        /// <summary>
        /// 微信登录 code（从 wx.login 获取）
        /// </summary>
        public string LoginCode { get; set; }

        /// <summary>
        /// 手机号获取 code（从 getPhoneNumber 回调获取）
        /// </summary>
        public string PhoneCode { get; set; }
    }

    /// <summary>
    /// 微信接口返回的 openid/session_key
    /// </summary>
    public class WxJscode2SessionResponse
    {
        [JsonPropertyName("openid")]
        public string OpenId { get; set; }

        [JsonPropertyName("session_key")]
        public string SessionKey { get; set; }

        [JsonPropertyName("unionid")]
        public string UnionId { get; set; }

        [JsonPropertyName("errcode")]
        public int ErrCode { get; set; }

        [JsonPropertyName("errmsg")]
        public string ErrMsg { get; set; }
    }
}