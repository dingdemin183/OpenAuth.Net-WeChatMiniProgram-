using System;

namespace OpenAuth.App.Request
{
    /// <summary>
    /// 查询报修列表请求
    /// </summary>
    public class QueryRepairOrderListReq : PageReq
    {
        /// <summary>
        /// 状态筛选：0-待处理，1-处理中，2-已解决
        /// </summary>
        public int? Status { get; set; }

        /// <summary>
        /// 手机号模糊搜索
        /// </summary>
        public string Phone { get; set; }

        /// <summary>
        /// 用户姓名模糊搜索
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// 开始时间
        /// </summary>
        public DateTime? StartTime { get; set; }

        /// <summary>
        /// 结束时间
        /// </summary>
        public DateTime? EndTime { get; set; }
    }

    public class UpdateRepairOrderReq : AddRepairOrderReq
    {
        /// <summary>
        /// 报修单主键ID
        /// </summary>
        public string Id { get; set; }

    }

    /// <summary>
    /// 添加/更新报修请求
    /// </summary>
    public class AddRepairOrderReq
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
        /// 故障描述
        /// </summary>
        public string FaultDesc { get; set; }

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
        /// 省份
        /// </summary>
        public string Province { get; set; }


        /// <summary>
        /// 城市
        /// </summary>
        public string City { get; set; }

        /// <summary>
        /// 区/县
        /// </summary>
        public string Area { get; set; }

        /// <summary>
        /// 详细地址
        /// </summary>
        public string DetailAddress { get; set; }

    }

    /// <summary>
    /// 更新报修状态请求（后台管理员）
    /// </summary>
    public class UpdateRepairStatusReq
    {
        /// <summary>
        /// 报修单ID
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 状态：0-拒绝报修，1-同意报修
        /// </summary>
        public int Status { get; set; }

        /// <summary>
        /// 处理备注
        /// </summary>
        public string Remark { get; set; }
    }
}