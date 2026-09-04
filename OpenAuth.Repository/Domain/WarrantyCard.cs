//using OpenAuth.Repository.Core;
//using OpenAuth.Repository.Enums;
//using SqlSugar;
//using System;
//using System.ComponentModel.DataAnnotations.Schema;

//namespace OpenAuth.Repository.Domain
//{
//    /// <summary>
//    /// 延保卡表
//    /// </summary>
//    [Table("warranty_card")]
//    [SugarTable("warranty_card")]
//    public class WarrantyCard : StringEntity
//    {
//        /// <summary>
//        /// 延保卡号（业务唯一）
//        /// </summary>
//        [SugarColumn(Length = 32)]
//        public string CardNo { get; set; }

//        /// <summary>
//        /// 用户ID（关联sys_user_external_auth.Id）
//        /// </summary>
//        [SugarColumn(Length = 50)]
//        public string UserId { get; set; }

//        /// <summary>
//        /// 关联订单ID（warranty_order.Id）
//        /// </summary>
//        [SugarColumn(Length = 50)]
//        public string OrderId { get; set; }

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
//        /// 产品唯一标识
//        /// </summary>
//        public string DeviceUniqueKey {  get; set; }

//        /// <summary>
//        /// 购买产品日期
//        /// </summary>
//        public DateTime? PurchaseDate { get; set; }

//        /// <summary>
//        /// 延保年限：1/2/3
//        /// </summary>
//        public int WarrantyYears { get; set; }

//        /// <summary>
//        /// 支付金额
//        /// </summary>
//        public decimal PaidAmount { get; set; }

//        /// <summary>
//        /// 生效日期（支付成功日）
//        /// </summary>
//        public DateTime StartDate { get; set; }

//        /// <summary>
//        /// 到期日期（支付成功日+延保年限）
//        /// </summary>
//        public DateTime EndDate { get; set; }

//        /// <summary>
//        /// 卡状态：0-待生效，1-生效中，2-已过期，3-已退款
//        /// </summary>
//        public CardStatusEnum CardStatus { get; set; } = CardStatusEnum.Pending;


//        public DateTime CreateTime { get; set; } = DateTime.Now;
//        public DateTime? UpdateTime { get; set; }

//        /// <summary>
//        /// 是否删除：0-未删除，1-已删除
//        /// </summary>
//        public bool IsDeleted { get; set; } = false;
//    }
//}