using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenAuth.App.Interface;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using OpenAuth.App.Warranty;
using System;
using System.Collections.Generic;
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

        public WarrantyController(WarrantyApp warrantyApp,IAuth auth)
        {
            _warrantyApp = warrantyApp;
            _auth = auth;
        }

        /// <summary>
        /// 创建延保支付订单
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<Response<WeChatPayResultResp>> CreatePayOrder([FromBody] CreateWarrantyPayOrderReq request)
        {
            var result = new Response<WeChatPayResultResp>();
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
        /// 查询订单支付状态
        /// </summary>
        /// <param name="orderNo">订单号</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<Response<WarrantyOrderResp>> QueryOrderStatus(string orderNo)
        {
            var result = new Response<WarrantyOrderResp>();
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
                var userId = GetWxUserId();
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
        /// <param name="cardId">延保卡卡号</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<Response<WarrantyCardResp>> CardDetail(string cardId)
        {
            var result = new Response<WarrantyCardResp>();
            try
            {
                var data = await _warrantyApp.GetCardDetailAsync(cardId);
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
        /// 获取当前登录用户的ID
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        private string GetWxUserId()
        {
            var session = _auth.GetCurrentSession();
            if (session?.UserId == null)
            {
                throw new Exception("用户未登录");
            }
            return session.UserId;
        }
    }
}