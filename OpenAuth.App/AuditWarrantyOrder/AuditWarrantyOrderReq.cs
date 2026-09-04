using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAuth.App.AuditWarrantyOrder
{
    /// <summary>
    /// 延保订单审核请求
    /// </summary>
    public class AuditWarrantyOrderReq
    {
        /// <summary>
        /// 订单号
        /// </summary>
        public string OrderNo { get; set; }

        /// <summary>
        /// 延保到期时间(如果审核通过，则需要填写延保到期时间)
        /// </summary>
        public DateTime?EndTime { get; set; }

        /// <summary>
        /// 审核结果：true-通过，false-拒绝
        /// </summary>
        public bool IsApproved { get; set; }

        /// <summary>
        /// 审核备注（拒绝时必须填写原因）
        /// </summary>
        public string Remark { get; set; }
    }

    /// <summary>
    /// 重新退款请求
    /// </summary>
    public class RetryRefundReq
    {
        /// <summary>
        /// 订单号
        /// </summary>
        public string OrderNo { get; set; }
    }
}
