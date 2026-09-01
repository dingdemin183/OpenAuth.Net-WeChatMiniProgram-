using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAuth.App.Response
{
    /// <summary>
    /// 小程序商品响应
    /// </summary>
    public class ProductMiniProgramResp
    {
        /// <summary>
        /// 商品id
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 商品名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 商品价格
        /// </summary>
        public decimal Price { get; set; }
        /// <summary>
        /// 商品图片url
        /// </summary>
        public string ImageUrl { get; set; }

        /// <summary>
        /// 淘宝链接
        /// </summary>
        public string TaobaoLink { get; set; }

    }

    /// <summary>
    /// 小程序商品响应
    /// </summary>
    public class ProductAdminResp
    {
        /// <summary>
        /// 商品id
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// 商品名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 商品价格
        /// </summary>
        public decimal Price { get; set; }
        /// <summary>
        /// 商品图片url
        /// </summary>
        public string ImageUrl { get; set; }

        /// <summary>
        /// 淘宝链接
        /// </summary>
        public string TaobaoLink { get; set; }

        /// <summary>
        /// 状态：0-下架，1-上架
        /// </summary>
        public int Status { get; set; } 

    }


}
