// OpenAuth.App/SSO/WxMiniProgramLoginRequest.cs

using Infrastructure;
using System.Text.Json.Serialization;

namespace OpenAuth.App.SSO
{
    /// <summary>
    /// 微信小程序登录请求
    /// </summary>
    public class WxMiniProgramLoginRequest
    {
        /// <summary>
        /// 微信登录凭证（wx.login 返回的 code）
        /// </summary>
        public string Code { get; set; }

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