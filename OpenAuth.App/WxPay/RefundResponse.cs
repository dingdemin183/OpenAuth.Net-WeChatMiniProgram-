// OpenAuth.App/Response/RefundResponse.cs

using System;
using System.Text.Json.Serialization;

namespace OpenAuth.App.Response
{
    /// <summary>
    /// 退款请求参数（V3）
    /// </summary>
    public class RefundReq
    {

        /// <summary>
        /// 微信支付订单号
        /// </summary>
        [JsonPropertyName("transaction_id")]
        public string TransactionId { get; set; }

        /// <summary>
        /// 商户退款单号（必填）
        /// </summary>
        [JsonPropertyName("out_refund_no")]
        public string OutRefundNo { get; set; }

        /// <summary>
        /// 退款原因
        /// </summary>
        [JsonPropertyName("reason")]
        public string Reason { get; set; }

        /// <summary>
        /// 退款结果回调url
        /// </summary>
        [JsonPropertyName("notify_url")]
        public string NotifyUrl { get; set; }

        /// <summary>
        /// 退款金额（必填）
        /// </summary>
        [JsonPropertyName("amount")]
        public RefundAmount Amount { get; set; }
    }

    /// <summary>
    /// 退款金额信息
    /// </summary>
    public class RefundAmount
    {
        /// <summary>
        /// 退款金额（分，必填）
        /// </summary>
        [JsonPropertyName("refund")]
        public int Refund { get; set; }

        /// <summary>
        /// 原订单金额（分，必填）
        /// </summary>
        [JsonPropertyName("total")]
        public int Total { get; set; }

        /// <summary>
        /// 退款币种，默认CNY
        /// </summary>
        [JsonPropertyName("currency")]
        public string Currency { get; set; } = "CNY";
    }

    /// <summary>
    /// 退款响应（V3）
    /// </summary>
    public class RefundResp
    {
        [JsonPropertyName("refund_id")]
        public string RefundId { get; set; }

        [JsonPropertyName("out_refund_no")]
        public string OutRefundNo { get; set; }

        [JsonPropertyName("out_trade_no")]
        public string OutTradeNo { get; set; }

        [JsonPropertyName("transaction_id")]
        public string TransactionId { get; set; }

        [JsonPropertyName("amount")]
        public RefundAmountResp Amount { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }
    }

    public class RefundAmountResp
    {
        [JsonPropertyName("refund")]
        public int Refund { get; set; }

        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        [JsonPropertyName("payer_refund")]
        public int PayerRefund { get; set; }

        [JsonPropertyName("payer_total")]
        public int PayerTotal { get; set; }
    }
}