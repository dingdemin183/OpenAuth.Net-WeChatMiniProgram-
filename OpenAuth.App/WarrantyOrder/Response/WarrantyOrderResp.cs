using System;

namespace OpenAuth.App.Response
{
    /// <summary>
    /// 延保订单响应DTO
    /// </summary>
    public class WarrantyOrderResp
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 订单号（业务唯一，如：YB20260901001）
        /// </summary>
        public string OrderNo { get; set; }

        /// <summary>
        /// 用户ID（关联sys_user_external_auth.Id）
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// 用户姓名（冗余）
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 手机号码（冗余）
        /// </summary>
        public string Phone { get; set; }

        /// <summary>
        /// 商品名称（冗余）
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// 产品品牌（冗余）
        /// </summary>
        public string ProductBrand { get; set; }

        /// <summary>
        /// 产品类型（冗余）
        /// </summary>
        public string ProductType { get; set; }

        /// <summary>
        /// 产品型号（冗余）
        /// </summary>
        public string ProductModel { get; set; }

        /// <summary>
        /// 产品购买日期
        /// </summary>
        public DateTime? PurchaseDate { get; set; }

        /// <summary>
        /// 能效照片URL
        /// </summary>
        public string EnergyImage { get; set; }

        /// <summary>
        /// 交易图片URL
        /// </summary>
        public string TradeImage { get; set; }

        /// <summary>
        /// 延保年限：1/2/3
        /// </summary>
        public int WarrantyYears { get; set; }

        /// <summary>
        /// 支付金额
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// 支付状态：0-待支付，1-已支付，2-已取消，3-已退款
        /// </summary>
        public int PayStatus { get; set; }

        /// <summary>
        /// 支付状态名称
        /// </summary>
        public string PayStatusName { get; set; }

        /// <summary>
        /// 支付时间
        /// </summary>
        public DateTime? PayTime { get; set; }

        /// <summary>
        /// 微信支付交易单号
        /// </summary>
        public string TransactionId { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime? UpdateTime { get; set; }
    }

    /// <summary>
    /// 延保卡响应DTO
    /// </summary>
    public class WarrantyCardResp
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 延保卡号（业务唯一）
        /// </summary>
        public string CardNo { get; set; }

        /// <summary>
        /// 用户ID（关联sys_user_external_auth.Id）
        /// </summary>
        public string UserId { get; set; }

        /// <summary>
        /// 用户姓名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 手机号码
        /// </summary>
        public string Phone { get; set; }

        /// <summary>
        /// 商品名称
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// 产品品牌
        /// </summary>
        public string ProductBrand { get; set; }

        /// <summary>
        /// 产品类型
        /// </summary>
        public string ProductType { get; set; }

        /// <summary>
        /// 产品型号
        /// </summary>
        public string ProductModel { get; set; }

        /// <summary>
        /// 产品购买日期
        /// </summary>
        public DateTime? PurchaseDate { get; set; }

        /// <summary>
        /// 延保年限：1/2/3
        /// </summary>
        public int WarrantyYears { get; set; }

        /// <summary>
        /// 支付金额（冗余）
        /// </summary>
        public decimal PaidAmount { get; set; }

        /// <summary>
        /// 生效日期（支付成功日）
        /// </summary>
        public DateTime StartDate { get; set; }

        /// <summary>
        /// 到期日期（支付成功日 + 延保年限）
        /// </summary>
        public DateTime EndDate { get; set; }

        /// <summary>
        /// 卡状态：0-待生效，1-生效中，2-已过期，3-已退款
        /// </summary>
        public int CardStatus { get; set; }

        /// <summary>
        /// 卡状态名称
        /// </summary>
        public string CardStatusName { get; set; }

        /// <summary>
        /// 是否已过期
        /// </summary>
        public bool IsExpired { get; set; }

        /// <summary>
        /// 剩余天数
        /// </summary>
        public int RemainingDays { get; set; }

        /// <summary>
        /// 关联订单ID（warranty_order.Id）
        /// </summary>
        public string OrderId { get; set; }

        /// <summary>
        /// 续费时关联原卡ID
        /// </summary>
        public string ParentCardId { get; set; }

        /// <summary>
        /// 是否续费：0-否，1-是
        /// </summary>
        public bool IsRenewal { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime? UpdateTime { get; set; }

        /// <summary>
        /// 关联订单信息（扩展）
        /// </summary>
        public WarrantyOrderResp Order { get; set; }
    }
}