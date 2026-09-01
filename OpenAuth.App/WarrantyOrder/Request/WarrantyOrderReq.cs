using System;

namespace OpenAuth.App.Request
{
    /// <summary>
    /// 创建延保支付订单请求
    /// </summary>
    public class CreateWarrantyPayOrderReq
    {
        /// <summary>
        /// 姓名
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
        /// 购买日期
        /// </summary>
        public DateTime? PurchaseDate { get; set; }

        /// <summary>
        /// 能效照片URL
        /// </summary>
        public string EnergyImage { get; set; }

        /// <summary>
        /// 整机照片URL
        /// </summary>
        public string ProductImage { get; set; }

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
    }

    /// <summary>
    /// 微信支付统一下单请求参数
    /// </summary>
    public class WeChatPayUnifiedOrderReq
    {
        /// <summary>
        /// 应用ID
        /// </summary>
        public string AppId { get; set; }

        /// <summary>
        /// 商户号
        /// </summary>
        public string MchId { get; set; }

        /// <summary>
        /// 商品描述
        /// </summary>
        public string Body { get; set; }

        /// <summary>
        /// 商户订单号
        /// </summary>
        public string OutTradeNo { get; set; }

        /// <summary>
        /// 标价金额(分)
        /// </summary>
        public int TotalFee { get; set; }

        /// <summary>
        /// 终端IP
        /// </summary>
        public string SpbillCreateIp { get; set; }

        /// <summary>
        /// 通知地址
        /// </summary>
        public string NotifyUrl { get; set; }

        /// <summary>
        /// 交易类型
        /// </summary>
        public string TradeType { get; set; } = "JSAPI";

        /// <summary>
        /// 用户标识(小程序openid)
        /// </summary>
        public string OpenId { get; set; }
    }

    /// <summary>
    /// 微信支付回调请求
    /// </summary>
    public class WeChatPayCallbackReq
    {
        /// <summary>
        /// 订单号
        /// </summary>
        public string OrderNo { get; set; }

        /// <summary>
        /// 微信交易单号
        /// </summary>
        public string TransactionId { get; set; }

        /// <summary>
        /// 支付金额(分)
        /// </summary>
        public int TotalFee { get; set; }

        /// <summary>
        /// 回调原始数据(XML)
        /// </summary>
        public string CallbackData { get; set; }
    }

    /// <summary>
    /// 微信支付返回参数(用于前端调起支付)
    /// </summary>
    public class WeChatPayResultResp
    {
        /// <summary>
        /// 小程序appId
        /// </summary>
        public string AppId { get; set; }

        /// <summary>
        /// 时间戳
        /// </summary>
        public string TimeStamp { get; set; }

        /// <summary>
        /// 随机字符串
        /// </summary>
        public string NonceStr { get; set; }

        /// <summary>
        /// 订单详情扩展字符串(包含prepay_id)
        /// </summary>
        public string Package { get; set; }

        /// <summary>
        /// 签名方式
        /// </summary>
        public string SignType { get; set; } = "MD5";

        /// <summary>
        /// 支付签名
        /// </summary>
        public string PaySign { get; set; }

        /// <summary>
        /// 订单号
        /// </summary>
        public string OrderNo { get; set; }
    }
}