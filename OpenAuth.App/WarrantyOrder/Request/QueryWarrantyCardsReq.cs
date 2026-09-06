// OpenAuth.App/Request/QueryWarrantyCardsReq.cs
using System;

namespace OpenAuth.App.Request
{
    /// <summary>
    /// 查询延保卡列表请求（后台管理）
    /// </summary>
    public class QueryWarrantyCardsReq
    {
        /// <summary>
        /// 页码
        /// </summary>
        /// <example>1</example>
        public int Page { get; set; } = 1;

        /// <summary>
        /// 每页条数
        /// </summary>
        /// <example>10</example>
        public int Limit { get; set; } = 10;

        /// <summary>
        /// 关键词（模糊查询：用户ID、姓名、订单号）
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// 订单号（精确查询）
        /// </summary>
        public string OrderNo { get; set; }

        /// <summary>
        /// 订单状态：0-待支付，1-已支付待审核，2-生效中，3-已过期，4-已退款，5-退款失败
        /// </summary>
        public int? OrderStatus { get; set; }

        /// <summary>
        /// 开始时间（创建时间）
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// 结束时间（创建时间）
        /// </summary>
        public DateTime? EndTime { get; set; }
    }
}