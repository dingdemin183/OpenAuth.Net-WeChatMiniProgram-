// OpenAuth.Repository/Enums/WarrantyStatusEnum.cs

using Infrastructure;

namespace OpenAuth.Repository.Enums
{
    /// <summary>
    /// 延保记录状态
    /// </summary>
    public enum WarrantyStatusEnum
    {
        /// <summary>
        /// 待支付
        /// </summary>
        [Text("待支付")]
        Pending = 0,

        /// <summary>
        /// 已支付/待审核
        /// </summary>
        [Text("已支付")]
        Paid = 1,

        /// <summary>
        /// 生效中
        /// </summary>
        [Text("生效中")]
        Active = 2,

        /// <summary>
        /// 已过期
        /// </summary>
        [Text("已过期")]
        Expired = 3,

        /// <summary>
        /// 已退款
        /// </summary>
        [Text("已退款")]
        Refunded = 4,

        /// <summary>
        /// 退款失败
        /// </summary>
        [Text("退款失败")]
        RefundFailed = 5
    }
}