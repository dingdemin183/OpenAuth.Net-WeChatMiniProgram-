using SqlSugar;
using System;

namespace OpenAuth.App.Response
{
    
    /// <summary>
    /// 延保订单响应DTO
    /// </summary>
    public class WarrantyCardResp
    {

        /// <summary>
        /// 主键ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 订单号
        /// </summary>
        public string OrderNo { get; set; }

        /// <summary>
        /// 微信支付交易单号
        /// </summary>
        public string TransactionId { get; set; }

        /// <summary>
        /// 支付时间
        /// </summary>
        public DateTime? PayTime { get; set; }

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
        /// 能效照片URL
        /// </summary>
        public string EnergyImage { get; set; }

        /// <summary>
        /// 交易图片URL
        /// </summary>
        public string TradeImage { get; set; }

        /// <summary>
        /// 产品购买日期
        /// </summary>
        public DateTime? PurchaseDate { get; set; }

        /// <summary>
        /// 延保年限：
        /// </summary>
        public int WarrantyYears { get; set; }

        /// <summary>
        /// 支付金额（冗余）
        /// </summary>
        public decimal PaidAmount { get; set; }

        /// <summary>
        /// 到期日期
        /// </summary>
        public DateTime ? EndDate { get; set; }

        /// <summary>
        /// 卡状态：0-待支付，1-已支付，2-生效中，3-已过期，4-已退款
        /// </summary>
        public int CardStatus { get; set; }

        /// <summary>
        /// 卡状态名称
        /// </summary>
        public string CardStatusName { get; set; }

        /// <summary>
        /// 剩余天数
        /// </summary>
        public int RemainingDays { get; set; }
    }
}