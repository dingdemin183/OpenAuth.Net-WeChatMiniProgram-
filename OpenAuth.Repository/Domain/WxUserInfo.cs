// Infrastructure/Domain/UserInfo.cs

using System;

namespace Infrastructure.Domain
{
    /// <summary>
    /// 微信用户信息
    /// </summary>
    public class WxUserInfo
    {
        public string OpenId { get; set; }
        public string UnionId { get; set; }
        public string NickName { get; set; }
        public string SessionKey { get; set; }
        public string AvatarUrl { get; set; }
        public DateTime? CreateTime { get; set; }
        public DateTime LoginTime { get; set; }
        public string Token { get; set; }
    }

    /// <summary>
    /// 当前登录用户信息（统一模型）
    /// </summary>
    public class CurrentUserInfo
    {
        public string Account { get; set; }
        public string Name { get; set; }
        public string UserId { get; set; }
        public string AppKey { get; set; }
        public string LoginProvider { get; set; } // Local / WeChatMiniProgram
        public DateTime LoginTime { get; set; }
        public string Token { get; set; }

        // 微信特有字段
        public string OpenId { get; set; }
        public string UnionId { get; set; }
        public string NickName { get; set; }
        public string AvatarUrl { get; set; }
    }
}