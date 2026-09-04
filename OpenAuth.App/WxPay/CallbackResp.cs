// OpenAuth.App/Request/WeChatPayCallbackModels.cs

using System.Text.Json.Serialization;

namespace OpenAuth.App.Request
{

    #region 支付回调模型
    /// <summary>
    /// V3 回调通知根对象
    /// </summary>
    public class CallbackNotification
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("resource_type")]
        public string ResourceType { get; set; }

        [JsonPropertyName("event_type")]
        public string EventType { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("resource")]
        public CallbackResource Resource { get; set; }
    }

    /// <summary>
    /// 回调资源对象
    /// </summary>
    public class CallbackResource
    {
        [JsonPropertyName("original_type")]
        public string OriginalType { get; set; }

        [JsonPropertyName("algorithm")]
        public string Algorithm { get; set; }

        [JsonPropertyName("ciphertext")]
        public string Ciphertext { get; set; }

        [JsonPropertyName("associated_data")]
        public string AssociatedData { get; set; }

        [JsonPropertyName("nonce")]
        public string Nonce { get; set; }
    }

    /// <summary>
    /// 解密后的支付结果数据
    /// </summary>
    public class PayCallbackData
    {
        [JsonPropertyName("appid")]
        public string AppId { get; set; }

        [JsonPropertyName("mchid")]
        public string MchId { get; set; }

        [JsonPropertyName("out_trade_no")]
        public string OutTradeNo { get; set; }

        [JsonPropertyName("transaction_id")]
        public string TransactionId { get; set; }

        [JsonPropertyName("trade_type")]
        public string TradeType { get; set; }

        [JsonPropertyName("trade_state")]
        public string TradeState { get; set; }

        [JsonPropertyName("trade_state_desc")]
        public string TradeStateDesc { get; set; }

        [JsonPropertyName("bank_type")]
        public string BankType { get; set; }

        [JsonPropertyName("attach")]
        public string Attach { get; set; }

        [JsonPropertyName("success_time")]
        public string SuccessTime { get; set; }

        [JsonPropertyName("payer")]
        public PayerInfo Payer { get; set; }

        [JsonPropertyName("amount")]
        public AmountInfo Amount { get; set; }

        [JsonPropertyName("promotion_detail")]
        public object[] PromotionDetail { get; set; }
    }

    /// <summary>
    /// 支付者信息
    /// </summary>
    public class PayerInfo
    {
        [JsonPropertyName("openid")]
        public string OpenId { get; set; }
    }

    /// <summary>
    /// 金额信息
    /// </summary>
    public class AmountInfo
    {
        [JsonPropertyName("total")]
        public int Total { get; set; }

        [JsonPropertyName("payer_total")]
        public int PayerTotal { get; set; }

        [JsonPropertyName("currency")]
        public string Currency { get; set; }

        [JsonPropertyName("payer_currency")]
        public string PayerCurrency { get; set; }
    }

    /// <summary>
    /// 回调处理结果
    /// </summary>
    public class PayCallbackResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public PayCallbackData PayData { get; set; }
    }
    #endregion 支付回调模型

    #region 退款回调模型

    /// <summary>
    /// 退款回调通知根对象
    /// </summary>
    public class RefundCallbackNotification
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("create_time")]
        public string CreateTime { get; set; }

        [JsonPropertyName("resource_type")]
        public string ResourceType { get; set; }

        [JsonPropertyName("event_type")]
        public string EventType { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; }

        [JsonPropertyName("resource")]
        public CallbackResource Resource { get; set; }
    }

    /// <summary>
    /// 解密后的退款回调数据
    /// </summary>
    public class RefundCallbackData
    {
        /// <summary>
        /// 商户号
        /// </summary>
        [JsonPropertyName("mchid")]
        public string MchId { get; set; }

        /// <summary>
        /// 微信支付订单号
        /// </summary>
        [JsonPropertyName("transaction_id")]
        public string TransactionId { get; set; }

        /// <summary>
        /// 商户订单号
        /// </summary>
        [JsonPropertyName("out_trade_no")]
        public string OutTradeNo { get; set; }

        /// <summary>
        /// 微信退款单号
        /// </summary>
        [JsonPropertyName("refund_id")]
        public string RefundId { get; set; }

        /// <summary>
        /// 商户退款单号
        /// </summary>
        [JsonPropertyName("out_refund_no")]
        public string OutRefundNo { get; set; }

        /// <summary>
        /// 退款状态：SUCCESS-退款成功，CLOSED-退款关闭，PROCESSING-退款处理中，ABNORMAL-退款异常
        /// </summary>
        [JsonPropertyName("refund_status")]
        public string RefundStatus { get; set; }

        /// <summary>
        /// 退款成功时间
        /// </summary>
        [JsonPropertyName("success_time")]
        public string SuccessTime { get; set; }

        /// <summary>
        /// 退款入账账户
        /// </summary>
        [JsonPropertyName("user_received_account")]
        public string UserReceivedAccount { get; set; }

        /// <summary>
        /// 金额信息
        /// </summary>
        [JsonPropertyName("amount")]
        public RefundCallbackAmount Amount { get; set; }
    }

    /// <summary>
    /// 退款回调金额信息
    /// </summary>
    public class RefundCallbackAmount
    {
        /// <summary>
        /// 原订单金额（分）
        /// </summary>
        [JsonPropertyName("total")]
        public int Total { get; set; }

        /// <summary>
        /// 退款金额（分）
        /// </summary>
        [JsonPropertyName("refund")]
        public int Refund { get; set; }

        /// <summary>
        /// 用户实际支付金额（分）
        /// </summary>
        [JsonPropertyName("payer_total")]
        public int PayerTotal { get; set; }

        /// <summary>
        /// 用户退款金额（分）
        /// </summary>
        [JsonPropertyName("payer_refund")]
        public int PayerRefund { get; set; }
    }

    /// <summary>
    /// 退款回调处理结果
    /// </summary>
    public class RefundCallbackResult
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public RefundCallbackData RefundData { get; set; }
    }

    #endregion
}