using System;

namespace OpenAuth.App.Request
{
    /// <summary>
    /// 更新用户个人资料请求（头像昵称填写）
    /// </summary>
    public class UpdateUserProfileReq
    {
        /// <summary>
        /// 用户昵称（从微信昵称填写组件获取）
        /// </summary>
        public string NickName { get; set; }

        /// <summary>
        /// 头像URL
        /// </summary>
        public string AvatarUrl { get; set; }
    }

   
}