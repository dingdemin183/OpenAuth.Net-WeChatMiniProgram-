// OpenAuth.App/ProductManager/ProductApp.cs

using Azure.Core;
using Infrastructure;
using OpenAuth.App.Interface;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using OpenAuth.Repository.Domain;
using OpenAuth.Repository.Interface;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenAuth.App.ProductManager
{
    public class ProductApp : SqlSugarBaseApp<Product>
    {
        private readonly ISqlSugarClient _db;

        public ProductApp(ISqlSugarClient db ,IAuth auth ) : base(db,auth)
        {
            _db = db;
        }

        #region 后台商品管理

        /// <summary>
        /// 后台添加商品
        /// </summary>
        /// <param name="req">请求参数</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<string> Add(AddProductReq req)
        {
            if(req == null)
            {
                throw new Exception("请求参数不能为空");
            }
            if (string.IsNullOrEmpty(req.Name))
            {
                throw new Exception("商品名称不能为空");
            }
            if(string.IsNullOrEmpty(req.ImageUrl))
            {
                throw new Exception("商品图片不能为空");
            }
            if(string.IsNullOrEmpty(req.TaobaoLink))
            {
                throw new Exception("商品淘宝链接不能为空");
            }
            if (!Uri.IsWellFormedUriString(req.TaobaoLink, UriKind.Absolute))
                throw new CommonException("淘宝链接格式不正确");
            if (req.Price<0)
            {
                throw new Exception("商品价格错误");
            }

            var product = new Product
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = req.Name,
                Price = req.Price,
                ImageUrl = req.ImageUrl,
                TaobaoLink = req.TaobaoLink,
                Status = req.Status,
                CreateTime = DateTime.Now,
                IsDeleted = false
            };
            await _db.Insertable(product)
                .ExecuteCommandAsync()
                .ConfigureAwait(false);
            return product.Id;

        }

        /// <summary>
        /// 更新商品信息
        /// </summary>
        /// <param name="req">请求参数</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<string> Update(UpdateProductReq req)
        {

            if (req == null)
            {
                throw new Exception("请求参数不能为空");
            }
            if (string.IsNullOrEmpty(req.Name))
            {
                throw new Exception("商品名称不能为空");
            }
            if (string.IsNullOrEmpty(req.ImageUrl))
            {
                throw new Exception("商品图片不能为空");
            }
            if (string.IsNullOrEmpty(req.TaobaoLink))
            {
                throw new Exception("商品淘宝链接不能为空");
            }

            if (req.Price < 0)
            {
                throw new Exception("商品价格错误");
            }
            // 编辑
            var product = await _db.Queryable<Product>()
                             .Where(u => u.Id == req.Id && !u.IsDeleted)
                             .FirstAsync()
                             .ConfigureAwait(false);

            if (product == null)
                throw new Exception("商品不存在");

            product.Name = req.Name;
            product.Price = req.Price;
            product.ImageUrl = req.ImageUrl;
            product.TaobaoLink = req.TaobaoLink;
            product.Status = req.Status;
            product.UpdateTime = DateTime.Now;

            await _db.Updateable(product)
                     .UpdateColumns(x => new { x.Name, x.Price, x.ImageUrl, x.Status, x.UpdateTime, x.TaobaoLink })
                     .ExecuteCommandAsync()
                     .ConfigureAwait(false);
            return product.Id;
        }


        /// <summary>
        /// 批量删除商品（软删除）
        /// </summary>
        /// <param name="id">商品id</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task Delete(DeleteProductReq req)
        {
            // 参数校验
            if (req.Ids == null || req.Ids.Length == 0)
                throw new CommonException("商品Id不能为空");

            // 批量查询
            var products = await _db.Queryable<Product>()
                                    .Where(u => req.Ids.Contains(u.Id) && !u.IsDeleted)
                                    .ToListAsync()
                                    .ConfigureAwait(false);

            if (products == null || products.Count == 0)
                throw new CommonException("商品不存在或已删除");

            // 批量更新
            var updateTime = DateTime.Now;
            foreach (var product in products)
            {
                product.IsDeleted = true;
                product.UpdateTime = updateTime;
            }
            
            await _db.Updateable(products)
                     .UpdateColumns(p => new { p.IsDeleted, p.UpdateTime })
                     .ExecuteCommandAsync()
                     .ConfigureAwait(false);
        }

        /// <summary>
        /// 下架商品
        /// </summary>
        public async Task OffShelf(string id)
        {
            if(string.IsNullOrEmpty(id))
            {
                throw new Exception("商品Id不能为空");
            }
            try
            {
                var product = await _db.Queryable<Product>()
                                       .Where(u => u.Id == id && !u.IsDeleted)
                                       .FirstAsync()
                                       .ConfigureAwait(false);
                if (product == null)
                    throw new Exception("商品不存在");

                product.Status = 0;
                product.UpdateTime = DateTime.Now;
                await  _db.Updateable(product)
                          .UpdateColumns(p => new { p.Status, p.UpdateTime })
                          .ExecuteCommandAsync()
                          .ConfigureAwait(false);
         
            }
            catch (Exception ex)
            {
                throw new Exception("下架商品失败：" + ex.Message);
            }

        }

        /// <summary>
        /// 管理员上架商品
        /// </summary>
        /// <param name="id">商品id</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task OnShelf(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new Exception("商品Id不能为空");
            }
          
            var product = await _db.Queryable<Product>()
                                .Where(u => u.Id == id && !u.IsDeleted)
                                .FirstAsync()
                                .ConfigureAwait(false);
            if (product == null)
                throw new Exception("商品不存在");

            product.Status = 1;
            product.UpdateTime = DateTime.Now;
            await  _db.Updateable(product)
                     .UpdateColumns(p => new { p.Status, p.UpdateTime })
                     .ExecuteCommandAsync()
                     .ConfigureAwait(false);
           
        }

        /// <summary>
        ///  后台分页查询所有商品列表
        /// </summary>
        /// <param name="req"></param>
        /// <returns></returns>
        public async Task<TableResp<ProductAdminResp>> QueryAdminAsync(QueryProductListReq req)
        {
            var query = _db.Queryable<Product>()
                .Where(p => !p.IsDeleted)
                .WhereIF(!string.IsNullOrEmpty(req.Name), p => p.Name.Contains(req.Name))
                .WhereIF(req.Status.HasValue, p => p.Status == req.Status.Value);

            var total = await query.CountAsync();
            var list = await query.OrderByDescending(p => p.CreateTime)
                                  .Skip((req.page - 1) * req.limit)
                                  .Take(req.limit)
                                  .ToListAsync();

            var data = list.Select(x => new ProductAdminResp
            {
                Id = x.Id,
                Name = x.Name,
                Price = x.Price,
                ImageUrl = x.ImageUrl,
                TaobaoLink = x.TaobaoLink,
                Status = x.Status
            }).ToList();

            return new TableResp<ProductAdminResp>
            {
                Data = data,
                Count = total,
                Page = req.page,
                Limit = req.limit
            };
        }

        /// <summary>
        /// 根据Id获取商品详情
        /// </summary>
        /// <param name="id">商品id</param>
        /// <returns>商品详细信息</returns>
        /// <exception cref="Exception"></exception>
        public async Task<ProductAdminResp> GetForMiniProgramAsync(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new Exception("商品Id不能为空");
            }
            var result = await _db.Queryable<Product>()
                .Where(p => p.Id == id && !p.IsDeleted && p.Status == 1)
                .Select(p => new ProductAdminResp
                {
                    Id = p.Id, 
                    Name = p.Name,
                    Price = p.Price,
                    ImageUrl = p.ImageUrl,
                    TaobaoLink = p.TaobaoLink,
                    Status = p.Status
                    
                })
                .FirstAsync()
                .ConfigureAwait(false);
            return result;
        }
        #endregion 后台管理

        #region 小程序 商品

        /// <summary>
        ///  小程序端商品列表（只返回上架商品）
        /// </summary>
        /// <returns></returns>
        public async Task<List<ProductMiniProgramResp>> QueryForMiniProgramAsync()
        {
            var result= await _db.Queryable<Product>()
                .Where(p => !p.IsDeleted && p.Status == 1)
                .OrderByDescending(p => p.CreateTime)
                .Select(p => new ProductMiniProgramResp
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    ImageUrl = p.ImageUrl,
                    TaobaoLink = p.TaobaoLink,
                })
                .ToListAsync()
                .ConfigureAwait(false);
            return result;
        }

       
        #endregion 小程序
    }
}