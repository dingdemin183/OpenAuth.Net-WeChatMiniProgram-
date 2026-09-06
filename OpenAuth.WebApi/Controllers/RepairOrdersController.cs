using Castle.Core.Internal;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenAuth.App.Interface;
using OpenAuth.App.Repair;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenAuth.WebApi.Controllers
{

    /// <summary>
    /// 报修单管理
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "报修单管理_RepairOrders")]
    public class RepairOrdersController : ControllerBase
    {
        private readonly RepairOrderApp _repairOrderApp;
        private readonly IAuth _auth;

        public RepairOrdersController(
            RepairOrderApp repairOrderApp,
            IAuth auth)
        {
            _repairOrderApp = repairOrderApp;
            _auth = auth;
        }

        /// <summary>
        /// 获取当前登录用户ID(后台账号 UserId )
        /// </summary>
        private string GetCurrentUserId()
        {
            var context = _auth.GetCurrentUser();
            if (context?.User == null)
            {
                throw new Exception("用户未登录");
            }
            return context.User.Id;
        }

        #region 后台管理
        /// <summary>
        /// 分页查询报修列表（后台管理）
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<TableResp<RepairOrderResp>> Query([FromBody] QueryRepairOrderListReq request)
        {
            return await _repairOrderApp.QueryAsync(request);
        }

        /// <summary>
        ///管理员审核处理报修-后台管理（同意=1、拒绝=0）
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<Response<bool>> UpdateStatus([FromBody] UpdateRepairStatusReq request)
        {
            // 从当前登录上下文获取处理人ID
            var handlerId = GetCurrentUserId();

            await _repairOrderApp.UpdateStatusAsync(request, handlerId);

            return new Response<bool>
            {
                Code = 200,
                Message = "更新成功",
                Data = true
            };
        }

        #endregion 后台管理
        #region 小程序端

        /// <summary>
        /// 获取报修单详情
        /// </summary>
        /// <param name="id">报修单id</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<Response<RepairOrderResp>> GetDetail(string id)
        {
            var data = await _repairOrderApp.GetDetailAsync(id);

            return new Response<RepairOrderResp>
            {
                Code = 200,
                Message = "操作成功",
                Data = data
            };
        }

        /// <summary>
        /// 获取报修单总数 -小程序查看当前已有多少人报修
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<Response<int>> GetCount()
        {
            var count = await _repairOrderApp.GetRepairOrderCountAsync();
            return new Response<int>
            {
                Code = 200,
                Message = "查询成功",
                Data = count
            };
        }

        /// <summary>
        /// 用户提交报修-小程序端
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<Response<string>> SubmitRepair([FromBody] AddRepairOrderReq request)
        {
            var id = await _repairOrderApp.SubmitAsync(request);

            return new Response<string>
            {
                Code = 200,
                Message = "提交成功",
                Data = id
            };
        }

        ///// <summary>
        ///// 小程序用户更新报修 -小程序端
        ///// </summary>
        ///// <param name="request">请求参数</param>
        ///// <returns></returns>
        //[HttpPost]
        //public async Task<Response<string>> UpdateRepair([FromBody] UpdateRepairOrderReq request)
        //{
        //    var id = await _repairOrderApp.UpdateAsync(request);

        //    return new Response<string>
        //    {
        //        Code = 200,
        //        Message = "提交成功",
        //        Data = id
        //    };
        //}

        /// <summary>
        /// 获取当前用户的报修记录（小程序端"我的报修"）
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        [HttpGet]
        public async Task<Response<List<RepairOrderResp>>> MyRepairs()
        {
            // 查询当前用户的报修记录
            var list=await _repairOrderApp.QueryByUserAsync();
            return new Response<List<RepairOrderResp>>
            {
                Data = list,
                Message = "查询成功",
                Code = 200,


            };



        }

        #endregion
    }
}