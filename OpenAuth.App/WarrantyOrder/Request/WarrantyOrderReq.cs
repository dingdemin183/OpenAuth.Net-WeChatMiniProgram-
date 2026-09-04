using System;
using System.Text.Json.Serialization;

namespace OpenAuth.App.Request
{
    /// <summary>
    /// 创建延保支付订单请求
    /// </summary>
    public class CreateWarrantyPayOrderReq
    {
        /// <summary>
        /// 二次支付时传订单号，首次支付时不传
        /// </summary>
        public string OrderNo { get; set; }

        /// <summary>
        /// 姓名
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
        /// 购买日期
        /// </summary>
        public DateTime PurchaseDate { get; set; }

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
        /// 支付金额(元）
        /// </summary>
        public decimal Amount { get; set; }
    }

    /// <summary>
    /// V3 统一下单请求参数（只保留必填）
    /// </summary>
    public class UnifiedOrderReq
    {
        /// <summary>
        /// 公众账号ID（必填）
        /// </summary>
        [JsonPropertyName("appid")]
        public string AppId { get; set; }

        /// <summary>
        /// 商户号（必填）
        /// </summary>
        [JsonPropertyName("mchid")]
        public string MchId { get; set; }

        /// <summary>
        /// 商品描述（必填）
        /// </summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// 商户订单号（必填，6-32位）
        /// </summary>
        [JsonPropertyName("out_trade_no")]
        public string OutTradeNo { get; set; }

        /// <summary>
        /// 商户回调地址（必填）
        /// </summary>
        [JsonPropertyName("notify_url")]
        public string NotifyUrl { get; set; }

        /// <summary>
        /// 订单金额（必填）
        /// </summary>
        [JsonPropertyName("amount")]
        public WeChatPayV3Amount Amount { get; set; }

        /// <summary>
        /// 支付者信息（必填）
        /// </summary>
        [JsonPropertyName("payer")]
        public WeChatPayV3Payer Payer { get; set; }
    }

    /// <summary>
    /// 订单金额
    /// </summary>
    public class WeChatPayV3Amount
    {
        /// <summary>
        /// 总金额（分，必填）
        /// </summary>
        [JsonPropertyName("total")]
        public int Total { get; set; }
    }

    /// <summary>
    /// 支付者信息
    /// </summary>
    public class WeChatPayV3Payer
    {
        /// <summary>
        /// 用户openid（必填）
        /// </summary>
        [JsonPropertyName("openid")]
        public string OpenId { get; set; }
    }

    /// <summary>
    /// 微信支付回调请求（V3 版）
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
        /// 回调原始数据（V3 是 JSON）
        /// </summary>
        public string CallbackData { get; set; }
    }

    /// <summary>
    /// V3 统一下单响应
    /// </summary>
    public class UnifiedOrderResp
    {
        [JsonPropertyName("prepay_id")]
        public string PrepayId { get; set; }
    }

    /// <summary>
    /// 前端调起支付接口所需参数模型
    /// </summary>
    public class WeChatPayResp
    {
        /// <summary>
        /// 应用ID
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
        /// prepay_id参数值
        /// </summary>
        public string Package { get; set; }

        /// <summary>
        /// 签名类型，默认为RSA
        /// </summary>
        public string SignType { get; set; } = "RSA";

        /// <summary>
        /// 签名值
        /// </summary>
        public string PaySign { get; set; }
    }
}