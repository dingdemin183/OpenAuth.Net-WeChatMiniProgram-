//using OpenAuth.Repository.Core;
//using SqlSugar;
//using System;
//using System.ComponentModel.DataAnnotations.Schema;
//using OpenAuth.Repository.Enums;

//namespace OpenAuth.Repository.Domain
//{
//    /// <summary>
//    /// 延保订单表
//    /// </summary>
//    [Table("warranty_order")]
//    [SugarTable("warranty_order")]
//    public class WarrantyOrder : StringEntity
//    {
//        /// <summary>
//        /// 订单号（业务唯一，如：YB20260901001）
//        /// </summary>
//        [SugarColumn(Length = 32)]
//        public string OrderNo { get; set; }

//        /// <summary>
//        /// 用户ID（关联sys_user_external_auth.Id）
//        /// </summary>
//        [SugarColumn(Length = 50)]
//        public string UserId { get; set; }

//        /// <summary>
//        /// 姓名
//        /// </summary>
//        [SugarColumn(Length = 50)]
//        public string UserName { get; set; }

//        /// <summary>
//        /// 手机号码
//        /// </summary>
//        [SugarColumn(Length = 20)]
//        public string Phone { get; set; }

//        /// <summary>
//        /// 商品名称
//        /// </summary>
//        [SugarColumn(Length = 100)]
//        public string ProductName { get; set; }


//        /// <summary>
//        /// 产品品牌
//        /// </summary>
//        [SugarColumn(Length = 50)]
//        public string ProductBrand { get; set; }

//        /// <summary>
//        /// 产品类型
//        /// </summary>
//        [SugarColumn(Length = 50)]
//        public string ProductType { get; set; }

//        /// <summary>
//        /// 产品型号
//        /// </summary>
//        [SugarColumn(Length = 100)]
//        public string ProductModel { get; set; }

//        /// <summary>
//        /// 购买日期
//        /// </summary>
//        public DateTime PurchaseDate { get; set; }

//        /// <summary>
//        /// 能效照片URL
//        /// </summary>
//        [SugarColumn(Length = 500)]
//        public string EnergyImage { get; set; }

//        /// <summary>
//        /// 交易图片URL
//        /// </summary>
//        [SugarColumn(Length = 500)]
//        public string TradeImage { get; set; }

//        /// <summary>
//        /// 延保年限：1/2/3
//        /// </summary>
//        public int WarrantyYears { get; set; }

//        /// <summary>
//        /// 支付金额
//        /// </summary>
//        public decimal Amount { get; set; }

//        /// <summary>
//        /// 支付状态：0-待支付，1-已支付，2-已取消，3-已退款
//        /// </summary>
//        public PayStatusEnum PayStatus { get; set; } = 0;

//        /// <summary>
//        /// 审核状态：0-待审核，1-审核通过，2-审核拒绝
//        /// </summary>
//        public AuditStatusEnum AuditStatus { get; set; }

//        /// <summary>
//        /// 审核备注（拒绝原因等）
//        /// </summary>
//        public string AuditRemark { get; set; }

//        /// <summary>
//        /// 支付时间
//        /// </summary>
//        public DateTime? PayTime { get; set; }

//        /// <summary>
//        /// 微信支付交易单号
//        /// </summary>
//        [SugarColumn(Length = 64)]
//        public string TransactionId { get; set; }

//        /// <summary>
//        /// 微信预支付ID
//        /// </summary>
//        [SugarColumn(Length = 64)]
//        public string PrepayId { get; set; }


//        /// <summary>
//        /// 支付回调原始数据（JSON）
//        /// </summary>
//        [SugarColumn(ColumnDataType = "TEXT")]
//        public string CallbackData { get; set; }

//        /// <summary>
//        /// 回调时间
//        /// </summary>
//        public DateTime? CallbackTime { get; set; }

//        public DateTime CreateTime { get; set; } = DateTime.Now;
//        public DateTime? UpdateTime { get; set; }

//        /// <summary>
//        /// 是否删除：0-未删除，1-已删除
//        /// </summary>
//        public bool IsDeleted { get; set; } = false;
//    }
//}