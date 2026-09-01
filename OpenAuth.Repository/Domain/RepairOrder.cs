using OpenAuth.Repository.Core;
using SqlSugar;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenAuth.Repository.Domain
{
    /// <summary>
    /// 报修单表
    /// </summary>
    [Table("repair_order")]
    [SugarTable("repair_order")]
    public class RepairOrder : StringEntity
    {
        /// <summary>
        /// 用户ID（关联sys_user_external_auth.Id）
        /// </summary>
        [SugarColumn(Length = 50)]
        public string UserId { get; set; }

        /// <summary>
        /// 姓名
        /// </summary>
        [SugarColumn(Length = 50)]
        public string UserName { get; set; }

        /// <summary>
        /// 手机号码
        /// </summary>
        [SugarColumn(Length = 20)]
        public string Phone { get; set; }

        /// <summary>
        /// 产品品牌
        /// </summary>
        [SugarColumn(Length = 50)]
        public string ProductBrand { get; set; }

        /// <summary>
        /// 产品类型
        /// </summary>
        [SugarColumn(Length = 50)]
        public string ProductType { get; set; }

        /// <summary>
        /// 产品型号
        /// </summary>
        [SugarColumn(Length = 100)]
        public string ProductModel { get; set; }

        /// <summary>
        /// 故障描述
        /// </summary>
        [SugarColumn(ColumnDataType = "TEXT")]
        public string FaultDesc { get; set; }

        /// <summary>
        /// 购买日期
        /// </summary>
        public DateTime? PurchaseDate { get; set; }

     
        [SugarColumn(Length = 500)]
        public string EnergyImage { get; set; }

        /// <summary>
        /// 整机照片URL
        /// </summary>
        [SugarColumn(Length = 500)]
        public string ProductImage { get; set; }

        /// <summary>
        /// 交易图片URL
        /// </summary>
        [SugarColumn(Length = 500)]
        public string TradeImage { get; set; }

        /// <summary>
        /// 省份
        /// </summary>
        [SugarColumn(Length = 50)]
        public string Province { get; set; }

        /// <summary>
        /// 省份编号
        /// </summary>
        public int? ProvinceId { get; set; }

        /// <summary>
        /// 城市
        /// </summary>
        [SugarColumn(Length = 50)]
        public string City { get; set; }

        /// <summary>
        /// 城市编号
        /// </summary>
        public int? CityId { get; set; }

        /// <summary>
        /// 区/县
        /// </summary>
        [SugarColumn(Length = 50)]
        public string District { get; set; }

        /// <summary>
        /// 区编号
        /// </summary>
        public int? DistrictId { get; set; }

        /// <summary>
        /// 详细地址
        /// </summary>
        [SugarColumn(Length = 200)]
        public string DetailAddress { get; set; }

        /// <summary>
        /// 状态：0-待处理，1-处理中，2-已解决，3-已关闭
        /// </summary>
        public int Status { get; set; } = 0;

        /// <summary>
        /// 处理备注
        /// </summary>
        [SugarColumn(ColumnDataType = "TEXT")]
        public string Remark { get; set; }

        /// <summary>
        /// 处理人ID
        /// </summary>
        [SugarColumn(Length = 50)]
        public string HandlerId { get; set; }

        /// <summary>
        /// 处理时间
        /// </summary>
        public DateTime? HandledTime { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;
        /// <summary>
        /// 更新时间
        /// </summary>
        public DateTime? UpdateTime { get; set; }

        /// <summary>
        /// 是否删除：0-未删除，1-已删除
        /// </summary>
        public bool IsDeleted { get; set; } = false;
    }
}