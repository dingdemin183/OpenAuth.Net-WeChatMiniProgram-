using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenAuth.App.AuditWarrantyOrder;
using OpenAuth.App.Interface;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using OpenAuth.App.Warranty;
using OpenAuth.App.WxPay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace OpenAuth.WebApi.Controllers
{
    /// <summary>
    /// 延保管理
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "延保管理_Warranty")]
    public class WarrantyController : ControllerBase
    {
        private readonly WarrantyApp _warrantyApp;
        private readonly IAuth _auth;
        private readonly WxPayService _wxPayService;

        public WarrantyController(WarrantyApp warrantyApp,IAuth auth,WxPayService wxPayService)
        {
            _warrantyApp = warrantyApp;
            _wxPayService = wxPayService;
            _auth = auth;
        }

       
        /// <summary>
        /// 后台管理 - 查询延保卡列表
        /// </summary>
        /// <param name="req">查询条件</param>
        /// <returns>分页延保卡数据</returns>
        [HttpPost]
        public async Task<TableResp<WarrantyCardResp>> QueryWarrantyCards([FromBody] QueryWarrantyCardsReq req)
        {
            return await _warrantyApp.QueryWarrantyCardsAsync(req);
        }

        /// <summary>
        /// 创建延保新订单并返回支付参数
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<Response<WeChatPayResp>> CreatePayOrder([FromBody] CreateWarrantyPayOrderReq request)
        {
            var result = new Response<WeChatPayResp>();
            try
            {
                var payResult = await _warrantyApp.CreatePayOrderAsync(request);
                result.Code = 200;
                result.Message = "订单创建成功";
                result.Data = payResult;
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// 延保订单审核（通过/拒绝，拒绝时自动退款）
        /// </summary>
        [HttpPost]
        public async Task<Response<bool>> AuditWarrantyOrder([FromBody] AuditWarrantyOrderReq request)
        {
            var result = new Response<bool>();
            try
            {
                result = await _warrantyApp.AuditWarrantyOrderAsync(request);
                result.Code = 200;
                result.Message = "订单创建成功";
                result.Data = result.Data;

            }
            catch (CommonException ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
            }
            return result;
        }


        /// <summary>
        /// 查询订单支付状态
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        [HttpGet]
        [ApiExplorerSettings(IgnoreApi = true)]
        public async Task<Response<WarrantyCardResp>> QueryOrderStatus(string orderNo)
        {
            var result = new Response<WarrantyCardResp>();
            try
            {
                var data = await _warrantyApp.QueryOrderStatusAsync(orderNo);
                result.Code = 200;
                result.Message = "查询成功";
                result.Data = data;
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// 获取当前用户的延保卡列表
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<Response<List<WarrantyCardResp>>> MyCards()
        {
            var result = new Response<List<WarrantyCardResp>>();
            try
            {
                // 从当前登录上下文获取用户ID
                //var userId = GetWxUserId();
                var userId=_auth.GetCurrentSession().UserId;
                var data = await _warrantyApp.GetUserCardsAsync(userId);
                result.Code = 200;
                result.Message = "查询成功";
                result.Data = data;
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// 获取延保卡详情
        /// </summary>
        /// <param name="orderNo">延保订单号</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<Response<WarrantyCardResp>> CardDetail(string orderNo)
        {
            var result = new Response<WarrantyCardResp>();
            try
            {
                var data = await _warrantyApp.GetCardDetailAsync(orderNo);
                result.Code = 200;
                result.Message = "查询成功";
                result.Data = data;
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
            }
            return result;
        }

  
    }
}