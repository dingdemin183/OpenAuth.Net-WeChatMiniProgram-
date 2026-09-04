
namespace OpenAuth.App.Request
{
    /// <summary>
    /// 延保卡续费请求
    /// </summary>
    public class RenewWarrantyReq
    {
        /// <summary>
        /// 原延保卡ID
        /// </summary>
        public string CardId { get; set; }

        /// <summary>
        /// 续费年限
        /// </summary>
        public int RenewYears { get; set; }

        /// <summary>
        /// 续费金额
        /// </summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// 用户姓名
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 联系电话
        /// </summary>
        public string Phone { get; set; }
    }
}