// OpenAuth.App/Response/UserProfileResp.cs

namespace OpenAuth.App.Response
{
    public class UserProfileResp
    {
        public string Id { get; set; }
        public string OpenId { get; set; }
        public string NickName { get; set; }
        public string AvatarUrl { get; set; }
        public string UserPhone { get; set; }

        /// <summary>
        /// 待审核头像URL
        /// </summary>
        public string PendingAvatarUrl { get; set; }

        /// <summary>
        /// 头像审核状态：pending-审核中, pass-通过, reject-拒绝
        /// </summary>
        public string AvatarAuditStatus { get; set; }

        /// <summary>
        /// 提示消息（用于前端展示）
        /// </summary>
        public string Message { get; set; }
    }
}