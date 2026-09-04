using OpenAuth.App.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpenAuth.App.Request
{
    /// <summary>
    /// 添加/编辑商品请求
    /// </summary>
    public class AddProductReq
    {
        /// <summary>
        /// 商品名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 商品价格
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// 商品图片URL
        /// </summary>
        public string ImageUrl { get; set; }
        /// <summary>
        /// 淘宝跳转链接
        /// </summary>
        public string TaobaoLink { get; set; }
        /// <summary>
        /// 商品状态
        /// </summary>
        public int Status { get; set; } = 1;
    }

    public class UpdateProductReq : AddProductReq
    {
        /// <summary>
        /// 商品id
        /// </summary>
        public string Id { get; set; }
     
    }
    /// <summary>
    /// 批量删除商品请求
    /// </summary>
    public class DeleteProductReq
    {
        /// <summary>
        /// 商品id
        /// </summary>
        public string[] Ids { get; set; }
    }



    /// <summary>
    /// 查询商品列表请求
    /// </summary>
    public class QueryProductListReq : PageReq
    {
        /// <summary>
        /// 商品名称模糊搜索
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 商品状态筛选
        /// </summary>
        public int? Status { get; set; }
    }

   
}
