using Infrastructure;
using Microsoft.Extensions.Logging;
using OpenAuth.App.Interface;
using OpenAuth.Repository.Domain;
using OpenAuth.Repository.Enums;
using SqlSugar;
using System;
using System.Threading.Tasks;

namespace OpenAuth.App.WxPay
{
    /// <summary>
    /// 微信回调业务处理服务（只负责业务逻辑，不处理验签和解密）
    /// </summary>
    public class CallBackService : SqlSugarBaseApp<WarrantyRecord>
    {
        private readonly ISqlSugarClient _db;
        private readonly ILogger<CallBackService> _logger;

        public CallBackService(
            ISqlSugarClient db,
            IAuth auth,
            ILogger<CallBackService> logger) : base(db, auth)
        {
            _db = db;
            _logger = logger;
        }

        #region 支付回调业务处理

        /// <summary>
        /// 处理支付成功业务逻辑
        /// </summary>
        /// <param name="payData">解密后的支付回调数据（SKIT TransactionResource）</param>
        /// <returns>处理结果，Controller 据此决定返回状态码</returns>
        public async Task<CallbackProcessResult> ProcessPaymentAsync(SKIT.FlurlHttpClient.Wechat.TenpayV3.Events.TransactionResource payData)
        {
            if (payData == null)
                return CallbackProcessResult.PermanentFailure("回调数据为空");

            var orderNo = payData.OutTradeNumber;
            var transactionId = payData.TransactionId;
            var totalFee = payData.Amount?.Total ?? 0;

            try
            {
                _logger.LogInformation("开始处理支付回调：订单号={OrderNo}，交易号={TransactionId}，金额={TotalFee}分",
                    orderNo, transactionId, totalFee);

                // 查询订单（查询在事务外，避免长事务）
                var order = await _db.Queryable<WarrantyRecord>()
                    .Where(o => o.OrderNo == orderNo && o.IsDeleted == false)
                    .FirstAsync()
                    .ConfigureAwait(false);

                if (order == null)
                {
                    _logger.LogWarning("支付回调订单不存在：{OrderNo}", orderNo);
                    return CallbackProcessResult.PermanentFailure($"订单不存在：{orderNo}");
                }

                // 已支付，幂等返回
                if (order.OrderStatus == WarrantyStatusEnum.Paid)
                {
                    _logger.LogInformation("订单已支付，幂等忽略：{OrderNo}", orderNo);
                    return CallbackProcessResult.AlreadyProcessed($"订单已支付：{orderNo}");
                }


                var expectedAmount = (int)Math.Round(order.Amount * 100, MidpointRounding.AwayFromZero);
                if (expectedAmount != totalFee)
                {
                    _logger.LogError("【金额不一致告警】订单号={OrderNo}，订单金额={Expected}分，支付金额={Actual}分，请人工对账",
                        orderNo, expectedAmount, totalFee);
                    return CallbackProcessResult.PermanentFailure(
                        $"金额不一致：订单{expectedAmount}分，支付{totalFee}分");
                }

                var now = DateTime.Now;
                var affected = await _db.Updateable<WarrantyRecord>()
                    .SetColumns(o => new WarrantyRecord
                    {
                        OrderStatus = WarrantyStatusEnum.Paid,
                        TransactionId = transactionId,
                        PayTime = now,
                        UpdateTime = now
                    })
                    .Where(o => o.Id == order.Id && o.OrderStatus == WarrantyStatusEnum.Pending)
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                if (affected == 0)
                {
                    _logger.LogInformation("订单已被并发处理，幂等忽略：{OrderNo}", orderNo);
                    return CallbackProcessResult.AlreadyProcessed($"订单已被并发处理：{orderNo}");
                }

                _logger.LogInformation("订单支付成功：{OrderNo}，交易号={TransactionId}", orderNo, transactionId);
                return CallbackProcessResult.Success();
            }
            catch (Exception ex)
            {
                // 临时性失败（数据库异常等），让微信重试
                _logger.LogError(ex, "处理支付回调异常：{OrderNo}", orderNo);
                return CallbackProcessResult.TemporaryFailure($"处理异常：{ex.Message}");
            }
        }

        #endregion

        #region 退款回调业务处理

        /// <summary>
        /// 处理退款回调业务逻辑
        /// </summary>
        /// <param name="refundData">解密后的退款回调数据（SKIT RefundResource）</param>
        /// <param name="eventType">事件类型（REFUND.SUCCESS / REFUND.ABNORMAL / REFUND.CLOSED）</param>
        /// <returns>处理结果，Controller 据此决定返回状态码</returns>
        public async Task<CallbackProcessResult> ProcessRefundAsync(
            SKIT.FlurlHttpClient.Wechat.TenpayV3.Events.RefundResource refundData, string eventType)
        {
            if (refundData == null)
                return CallbackProcessResult.PermanentFailure("回调数据为空");

            var orderNo = refundData.OutTradeNumber;
            var refundNo = refundData.OutRefundNumber;
            var refundId = refundData.RefundId;
            var refundAmount = refundData.Amount?.Refund ?? 0;

            try
            {
                _logger.LogInformation("开始处理退款回调：订单号={OrderNo}，退款单号={RefundNo}，事件={EventType}",
                    orderNo, refundNo, eventType);

                // 查询订单
                var order = await _db.Queryable<WarrantyRecord>()
                    .Where(o => o.OrderNo == orderNo && o.IsDeleted == false)
                    .FirstAsync()
                    .ConfigureAwait(false);

                if (order == null)
                {
                    _logger.LogWarning("退款回调订单不存在：{OrderNo}", orderNo);
                    // 同支付回调，订单不存在按永久失败处理（同步提交 + 时间差足够）
                    return CallbackProcessResult.PermanentFailure($"订单不存在：{orderNo}");
                }
                // 同一笔退款（RefundId 相同）的重复回调直接幂等返回
                if (!string.IsNullOrEmpty(order.RefundId) && order.RefundId == refundId)
                {
                    _logger.LogInformation("该退款单已处理，幂等忽略：{OrderNo}，{RefundId}", orderNo, refundId);
                    return CallbackProcessResult.AlreadyProcessed($"退款单已处理：{refundId}");
                }

                var maxRefundAmount = (int)Math.Round(order.Amount * 100, MidpointRounding.AwayFromZero);
                if (refundAmount <= 0 || refundAmount > maxRefundAmount)
                {
                    _logger.LogError("【退款金额异常】订单号={OrderNo}，订单金额={Max}分，退款金额={Actual}分，请人工对账",
                        orderNo, maxRefundAmount, refundAmount);
                    return CallbackProcessResult.PermanentFailure(
                        $"退款金额异常：订单{maxRefundAmount}分，退款{refundAmount}分");
                }

                var now = DateTime.Now;
                int affected;
                if (eventType == "REFUND.SUCCESS")
                {
                    // 终态：退款成功。只更新 RefundId 为空的订单，避免覆盖已有的成功退款记录
                    affected = await _db.Updateable<WarrantyRecord>()
                        .SetColumns(o => new WarrantyRecord
                        {
                            OrderStatus = WarrantyStatusEnum.Refunded,
                            RefundNo = refundNo,
                            RefundId = refundId,
                            UpdateTime = now
                        })
                        .Where(o => o.Id == order.Id
                            && (o.RefundId == null || o.RefundId == ""))
                        .ExecuteCommandAsync()
                        .ConfigureAwait(false);
                }
                else if (eventType == "REFUND.ABNORMAL" || eventType == "REFUND.CLOSED")
                {
                    var remark = eventType == "REFUND.ABNORMAL"
                        ? $"退款异常，微信退款单号：{refundId}"
                        : $"退款关闭，微信退款单号：{refundId}";

                    affected = await _db.Updateable<WarrantyRecord>()
                        .SetColumns(o => new WarrantyRecord
                        {
                            OrderStatus = WarrantyStatusEnum.RefundFailed,
                            RefundNo = refundNo,
                            AuditRemark = remark,
                            UpdateTime = now
                        })
                        .Where(o => o.Id == order.Id
                            && (o.RefundId == null || o.RefundId == ""))
                        .ExecuteCommandAsync()
                        .ConfigureAwait(false);
                }
                else
                {
                    _logger.LogWarning("未知退款事件类型：{EventType}，订单号={OrderNo}", eventType, orderNo);
                    return CallbackProcessResult.PermanentFailure($"未知事件类型：{eventType}");
                }

                if (affected == 0)
                {
                    // 被并发处理或已被处理，幂等返回
                    _logger.LogInformation("退款回调被并发处理或已处理，幂等忽略：{OrderNo}，{RefundId}",
                        orderNo, refundId);
                    return CallbackProcessResult.AlreadyProcessed($"退款单已被处理：{refundId}");
                }

                _logger.LogInformation("退款回调处理完成：{OrderNo}，{RefundId}，{EventType}",
                    orderNo, refundId, eventType);
                return CallbackProcessResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "处理退款回调异常：{OrderNo}", orderNo);
                return CallbackProcessResult.TemporaryFailure($"处理异常：{ex.Message}");
            }
        }

        #endregion
    }

    /// <summary>
    /// 回调业务处理结果。Controller 据此决定返回给微信的状态码。
    /// 微信约定：返回 2xx 不重试；返回非 2xx 会在 4 小时内重试最多 8 次。
    /// </summary>
    public class CallbackProcessResult
    {
        /// <summary>
        /// true 表示临时性失败，应让微信重试（返回 500）；false 表示已处理或永久失败，不重试（返回 200/204）
        /// </summary>
        public bool ShouldRetry { get; set; }

        /// <summary>
        /// true 表示处理成功或幂等已处理；false 表示失败
        /// </summary>
        public bool IsSuccess { get; set; }

        public string Message { get; set; }

        public static CallbackProcessResult Success() =>
            new CallbackProcessResult { IsSuccess = true, ShouldRetry = false, Message = "处理成功" };

        public static CallbackProcessResult AlreadyProcessed(string msg) =>
            new CallbackProcessResult { IsSuccess = true, ShouldRetry = false, Message = msg };

        /// <summary>永久性失败（订单不存在、金额不一致等），不重试，需要人工对账</summary>
        public static CallbackProcessResult PermanentFailure(string msg) =>
            new CallbackProcessResult { IsSuccess = false, ShouldRetry = false, Message = msg };

        /// <summary>临时性失败（数据库异常等），应让微信重试</summary>
        public static CallbackProcessResult TemporaryFailure(string msg) =>
            new CallbackProcessResult { IsSuccess = false, ShouldRetry = true, Message = msg };
    }
}
