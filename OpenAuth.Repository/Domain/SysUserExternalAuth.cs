// OpenAuth.Repository/Domain/SysUserExternalAuth.cs

using OpenAuth.Repository.Core;
using System;

namespace OpenAuth.Repository.Domain
{
    /// <summary>
    /// 用户第三方认证关联表
    /// 注意：OpenId 就是用户的唯一标识，不再关联 SysUser 表
    /// </summary>
    public class SysUserExternalAuth : StringEntity
    {
        /// <summary>
        /// 登录提供方：WeChatMiniProgram, WeChatOfficial, QQ, DingTalk
        /// </summary>
        public string Provider { get; set; }

        /// <summary>
        /// 第三方唯一标识（小程序里就是 openid，作为用户ID使用）
        /// </summary>
        public string OpenId { get; set; }

        /// <summary>
        /// 手机号
        /// </summary>
        public string UserPhone { get; set; }

        /// <summary>
        /// 开放平台 UnionId（可选）
        /// </summary>
        public string UnionId { get; set; }

        /// <summary>
        /// 会话密钥（微信专用）
        /// </summary>
        public string SessionKey { get; set; }

        /// <summary>
        /// 昵称
        /// </summary>
        public string NickName { get; set; }

        /// <summary>
        /// 头像
        /// </summary>
        public string AvatarUrl { get; set; }

        /// <summary>
        /// 扩展数据（JSON格式）
        /// </summary>
        public string ExtraData { get; set; }

        public DateTime CreateTime { get; set; }
        public DateTime? UpdateTime { get; set; }
        public bool IsDeleted { get; set; }
    }
}