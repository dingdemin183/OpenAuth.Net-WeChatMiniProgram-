// OpenAuth.Repository/Domain/SysUserExternalAuth.cs

using OpenAuth.Repository.Core;
using SqlSugar;
using System;

namespace OpenAuth.Repository.Domain
{
    /// <summary>
    /// 用户第三方认证关联表
    /// 注意：OpenId 就是用户的唯一标识，不再关联 SysUser 表
    /// </summary>
    [SugarTable("sys_user_external_auth")]
    public class SysUserExternalAuth : StringEntity
    {
        /// <summary>
        /// 登录提供方：WeChatMiniProgram, WeChatOfficial, QQ, DingTalk
        /// </summary>
        [SugarColumn(ColumnName = "Provider", ColumnDescription = "登录提供方", Length = 50, IsNullable = false)]
        public string Provider { get; set; }

        /// <summary>
        /// 第三方唯一标识（小程序里就是 openid，作为用户ID使用）
        /// </summary>
        [SugarColumn(ColumnName = "OpenId", ColumnDescription = "第三方唯一标识", Length = 100, IsNullable = false)]
        public string OpenId { get; set; }

        /// <summary>
        /// 手机号
        /// </summary>
        [SugarColumn(ColumnName = "UserPhone", ColumnDescription = "手机号", Length = 20, IsNullable = true)]
        public string UserPhone { get; set; }

        /// <summary>
        /// 开放平台 UnionId（可选）
        /// </summary>
        [SugarColumn(ColumnName = "UnionId", ColumnDescription = "开放平台UnionId", Length = 100, IsNullable = true)]
        public string UnionId { get; set; }

        /// <summary>
        /// 会话密钥（微信专用）
        /// </summary>
        [SugarColumn(ColumnName = "SessionKey", ColumnDescription = "会话密钥", Length = 100, IsNullable = true)]
        public string SessionKey { get; set; }

        /// <summary>
        /// 昵称
        /// </summary>
        [SugarColumn(ColumnName = "NickName", ColumnDescription = "昵称", Length = 100, IsNullable = true)]
        public string NickName { get; set; }

        /// <summary>
        /// 头像
        /// </summary>
        [SugarColumn(ColumnName = "AvatarUrl", ColumnDescription = "头像", Length = 500, IsNullable = true)]
        public string AvatarUrl { get; set; }

        /// <summary>
        /// 最后登录IP地址
        /// </summary>
        [SugarColumn(ColumnName = "LastLoginIp", ColumnDescription = "最后登录IP地址", Length = 50, IsNullable = true)]
        public string LastLoginIp { get; set; }

        /// <summary>
        /// 扩展数据（JSON格式）
        /// </summary>
        [SugarColumn(ColumnName = "ExtraData", ColumnDescription = "扩展数据", Length = 2000, IsNullable = true)]
        public string ExtraData { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        [SugarColumn(ColumnName = "CreateTime", ColumnDescription = "创建时间", IsNullable = false)]
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        [SugarColumn(ColumnName = "UpdateTime", ColumnDescription = "更新时间", IsNullable = true)]
        public DateTime? UpdateTime { get; set; }

        /// <summary>
        /// 是否删除
        /// </summary>
        [SugarColumn(ColumnName = "IsDeleted", ColumnDescription = "是否删除", IsNullable = false, DefaultValue = "0")]
        public bool IsDeleted { get; set; }
    }
}