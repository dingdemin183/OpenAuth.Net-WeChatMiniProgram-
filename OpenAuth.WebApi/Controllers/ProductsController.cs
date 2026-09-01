// OpenAuth.WebApi/Controllers/ProductsController.cs

using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenAuth.App.ProductManager;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenAuth.WebApi.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "商品管理_Products")]
    public class ProductsController : ControllerBase
    {
        private readonly ProductApp _productApp;

        public ProductsController(ProductApp productApp)
        {
            _productApp = productApp;
        }

        #region 后台管理接口

        /// <summary>
        /// 后台管理-分页查询商品列表
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<TableResp<ProductAdminResp>> QueryAdmin([FromBody] QueryProductListReq req)
        {
            try
            {
                return await _productApp.QueryAdminAsync(req);
            }
            catch (Exception ex)
            {
                return new TableResp<ProductAdminResp>
                {
                    Code = 500,
                    Message = ex.Message,
                    Data = null,
                    Count = 0,
                    Page = req?.page ?? 1,
                    Limit = req?.limit ?? 20
                };
            }
        }

        /// <summary>
        /// 后台管理-添加商品
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<Response<string>> Add([FromBody] AddProductReq req)
        {
            try
            {
                var data = await _productApp.Add(req);
                return new Response<string>
                {
                    Code = 200,
                    Message = "添加商品成功",
                    Data = data
                };
            }
            catch (Exception ex)
            {
                return new Response<string>
                {
                    Code = 500,
                    Message = ex.Message,
                    Data = null
                };
            }
        }

        /// <summary>
        /// 后台管理-编辑商品
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<Response<string>> Update([FromBody] UpdateProductReq req)
        {
            try
            {
                var data = await _productApp.Update(req);
                return new Response<string>
                {
                    Code = 200,
                    Message = "编辑商品成功",
                    Data = data
                };
            }
            catch (Exception ex)
            {
                return new Response<string>
                {
                    Code = 500,
                    Message = ex.Message,
                    Data = null
                };
            }
        }

        /// <summary>
        /// 后台管理-删除商品
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<Response<bool>> Delete(string id)
        {
            try
            {
                await _productApp.Delete(id);
                return new Response<bool>
                {
                    Code = 200,
                    Message = "删除商品成功",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                return new Response<bool>
                {
                    Code = 500,
                    Message = ex.Message,
                    Data = false
                };
            }
        }

        /// <summary>
        /// 后台管理-下架商品
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<Response<bool>> OffShelf(string id)
        {
            try
            {
                await _productApp.OffShelf(id);
                return new Response<bool>
                {
                    Code = 200,
                    Message = "下架商品成功",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                return new Response<bool>
                {
                    Code = 500,
                    Message = ex.Message,
                    Data = false
                };
            }
        }

        /// <summary>
        /// 后台管理-上架商品
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<Response<bool>> OnShelf(string id)
        {
            try
            {
                await _productApp.OnShelf(id);
                return new Response<bool>
                {
                    Code = 200,
                    Message = "上架商品成功",
                    Data = true
                };
            }
            catch (Exception ex)
            {
                return new Response<bool>
                {
                    Code = 500,
                    Message = ex.Message,
                    Data = false
                };
            }
        }

        #endregion

        #region 小程序端接口（无需登录）

        /// <summary>
        /// 小程序端-获取所有上架商品
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<Response<List<ProductMiniProgramResp>>> GetListForMiniProgram()
        {
            try
            {
                var data = await _productApp.QueryForMiniProgramAsync();
                return new Response<List<ProductMiniProgramResp>>
                {
                    Code = 200,
                    Message = data != null && data.Count > 0 ? "操作成功" : "暂无上架商品",
                    Data = data ?? new List<ProductMiniProgramResp>()
                };
            }
            catch (Exception ex)
            {
                return new Response<List<ProductMiniProgramResp>>
                {
                    Code = 500,
                    Message = ex.Message,
                    Data = null
                };
            }
        }

        /// <summary>
        /// 后台管理-获取商品详情
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<Response<ProductMiniProgramResp>> GetDetailForMiniProgram(string id)
        {
            try
            {
                var data = await _productApp.GetForMiniProgramAsync(id);

                if (data == null)
                {
                    return new Response<ProductMiniProgramResp>
                    {
                        Code = 404,
                        Message = "商品不存在或已下架",
                        Data = null
                    };
                }

                return new Response<ProductMiniProgramResp>
                {
                    Code = 200,
                    Message = "操作成功",
                    Data = data
                };
            }
            catch (Exception ex)
            {
                return new Response<ProductMiniProgramResp>
                {
                    Code = 500,
                    Message = ex.Message,
                    Data = null
                };
            }
        }

        #endregion
    }
}