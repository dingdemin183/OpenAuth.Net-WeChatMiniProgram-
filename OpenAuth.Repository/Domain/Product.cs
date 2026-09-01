using OpenAuth.Repository.Core;
using SqlSugar;
using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace OpenAuth.Repository.Domain
{
    /// <summary>
    /// 商品表
    /// </summary>
    [Table("product")]
    [SugarTable("product")]
    public class Product : StringEntity
    {
        /// <summary>
        /// 商品名称，如：冰箱BCD-258
        /// </summary>
        [SugarColumn(Length = 100)]
        public string Name { get; set; }

        /// <summary>
        /// 售价
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// 商品图片URL
        /// </summary>
        [SugarColumn(Length = 500)]
        public string ImageUrl { get; set; }

        /// <summary>
        /// 淘宝跳转链接
        /// </summary>
        [SugarColumn(Length = 500)]
        public string TaobaoLink { get; set; }

        /// <summary>
        /// 商品描述
        /// </summary>
        [SugarColumn(ColumnDataType = "TEXT")]
        public string Description { get; set; }

        /// <summary>
        /// 状态：0-下架，1-上架
        /// </summary>
        public int Status { get; set; } = 1;

        public DateTime CreateTime { get; set; } = DateTime.Now;
        public DateTime? UpdateTime { get; set; }

        /// <summary>
        /// 是否删除：0-未删除，1-已删除
        /// </summary>
        public bool IsDeleted { get; set; } = false;
    }
}