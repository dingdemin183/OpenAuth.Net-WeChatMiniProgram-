// OpenAuth.Repository/Domain/WarrantyRecord.cs

using OpenAuth.Repository.Core;
using OpenAuth.Repository.Enums;
using SqlSugar;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenAuth.Repository.Domain
{
    /// <summary>
    /// 延保记录表
    /// </summary>
    [Table("warranty_record")]
    [SugarTable("warranty_record")]
    public class WarrantyRecord : StringEntity
    {
        /// <summary>
        /// 订单号（业务唯一）
        /// </summary>
        [SugarColumn(Length = 32)]
        public string OrderNo { get; set; }

        /// <summary>
        /// 微信支付交易单号
        /// </summary>
        [SugarColumn(Length = 64)]
        public string TransactionId { get; set; }

        /// <summary>
        /// 支付时间
        /// </summary>
        public DateTime? PayTime { get; set; }

        /// <summary>
        /// 用户ID
        /// </summary>
        [SugarColumn(Length = 50)]
        public string UserId { get; set; }

        /// <summary>
        /// 姓名
        /// </summary>
        [SugarColumn(Length = 50)]
        public string UserName { get; set; }

        /// <summary>
        /// 手机号码
        /// </summary>
        [SugarColumn(Length = 20)]
        public string Phone { get; set; }

        /// <summary>
        /// 产品品牌
        /// </summary>
        [SugarColumn(Length = 50)]
        public string ProductBrand { get; set; }

        /// <summary>
        /// 产品类型
        /// </summary>
        [SugarColumn(Length = 50)]
        public string ProductType { get; set; }

        /// <summary>
        /// 产品型号
        /// </summary>
        [SugarColumn(Length = 100)]
        public string ProductModel { get; set; }

        /// <summary>
        /// 产品购买日期
        /// </summary>
        public DateTime PurchaseDate { get; set; }

        /// <summary>
        /// 能效照片URL
        /// </summary>
        [SugarColumn(Length = 500)]
        public string EnergyImage { get; set; }

        /// <summary>
        /// 交易照片URL
        /// </summary>
        [SugarColumn(Length = 500)]
        public string TradeImage { get; set; }

        /// <summary>
        /// 延保年限
        /// </summary>
        public int WarrantyYears { get; set; } = 3;

        /// <summary>
        /// 支付金额
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// 生效日期
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>
        /// 到期日期
        /// </summary>
        public DateTime? EndDate { get; set; }

        /// <summary>
        /// 订单状态：0-待支付，1-已支付，2-生效中，3-已过期，4-已退款，5-退款失败
        /// </summary>
        public WarrantyStatusEnum OrderStatus { get; set; } = WarrantyStatusEnum.Pending;

        /// <summary>
        /// 审核备注
        /// </summary>
        [SugarColumn(Length = 500)]
        public string AuditRemark { get; set; }

        /// <summary>
        /// 退款单号
        /// </summary>
        [SugarColumn(Length = 32)]
        public string RefundNo { get; set; }

        /// <summary>
        /// 微信退款单号
        /// </summary>
        [SugarColumn(Length = 32)]
        public string RefundId { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime? UpdateTime { get; set; }

        /// <summary>
        /// 是否删除：0-未删除，1-已删除
        /// </summary>
        public bool IsDeleted { get; set; } = false;
    }
}