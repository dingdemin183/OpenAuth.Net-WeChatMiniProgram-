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

        /// <summary>
        /// 添加或编辑商品
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
            if(req.Price<0)
            {
                throw new Exception("商品价格错误");
            }
            try
            {
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
            catch (Exception ex)
            {
                throw new Exception("新增商品失败：" + ex.Message);
            }
       
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
            try
            {
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
                         .UpdateColumns(x => new { x.Name, x.Price, x.ImageUrl, x.Status, x.UpdateTime ,x.TaobaoLink})
                         .ExecuteCommandAsync()
                         .ConfigureAwait(false);
                return product.Id;
            }
            catch (Exception ex)
            {
                throw new Exception("编辑商品失败：" + ex.Message);
            }
        }


        /// <summary>
        /// 删除商品（软删除）
        /// </summary>
        /// <param name="id">商品id</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task Delete(string id)
        {
            //参数校验
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

                product.IsDeleted = true;
                product.UpdateTime = DateTime.Now;
                await  _db.Updateable(product)
                          .UpdateColumns(p => new { p.IsDeleted, p.UpdateTime })
                          .ExecuteCommandAsync()
                          .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw new Exception("删除商品失败：" + ex.Message);
            }

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
        /// 上架商品
        /// </summary>
        public async Task OnShelf(string id)
        {
            if(string.IsNullOrEmpty(id))
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
        /// 后台管理列表（分页查询）
        /// </summary>
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
        /// 小程序端商品列表（只返回上架商品）
        /// </summary>
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

        /// <summary>
        /// 根据Id获取商品详情
        /// </summary>
        public async Task<ProductMiniProgramResp> GetForMiniProgramAsync(string id)
        {
            if(string.IsNullOrEmpty(id))
            {
                throw new Exception("商品Id不能为空");
            }
            var result= await _db.Queryable<Product>()
                .Where(p => p.Id == id && !p.IsDeleted && p.Status == 1)
                .Select(p => new ProductMiniProgramResp
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    ImageUrl = p.ImageUrl,
                    TaobaoLink = p.TaobaoLink
                })
                .FirstAsync()
                .ConfigureAwait(false);
            return result;
        }
    }
}