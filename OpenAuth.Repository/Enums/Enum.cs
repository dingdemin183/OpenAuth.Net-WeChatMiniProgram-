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
    /// <summary>
    /// 产品类型枚举
    /// 备注：暂时设计为用户手动输入，后期要管理产品类型，则用此枚举
    /// </summary>
    public enum ProductTypeEnum
    {
        /// <summary>
        /// 冰箱
        /// </summary>
        [Text("冰箱")]
        Refrigerator = 0,

        /// <summary>
        /// 冰柜
        /// </summary>
        [Text("冰柜")]
        freezer = 1
    }
}