using Castle.Core.Internal;
using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenAuth.App.Interface;
using OpenAuth.App.Repair;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using System;
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
        /// 获取当前登录用户的账号（小程序账号 openid）
        /// </summary>
        private string GetWxUserId()
        {
            var session = _auth.GetCurrentSession();
            return session.UserId;
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

        /// <summary>
        /// 分页查询报修列表（后台管理）
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        public async Task<TableResp<RepairOrderResp>> Query([FromBody] QueryRepairOrderListReq request)
        {
            try
            {
                return await _repairOrderApp.QueryAsync(request);
            }
            catch (Exception ex)
            {
                return new TableResp<RepairOrderResp>
                {
                    Code = 500,
                    Message = ex.Message,
                    Data = null,
                    Count = 0,
                    Page = request?.page ?? 1,
                    Limit = request?.limit ?? 20
                };
            }
        }

        /// <summary>
        /// 获取报修单详情
        /// </summary>
        /// <param name="id">报修单id</param>
        /// <returns></returns>
        [HttpGet]
        [AllowAnonymous]
        public async Task<Response<RepairOrderResp>> GetDetail(string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    return new Response<RepairOrderResp>
                    {
                        Code = 400,
                        Message = "报修单ID不能为空",
                        Data = null
                    };
                }

                var data = await _repairOrderApp.GetDetailAsync(id);

                if (data == null)
                {
                    return new Response<RepairOrderResp>
                    {
                        Code = 404,
                        Message = "报修单不存在",
                        Data = null
                    };
                }

                return new Response<RepairOrderResp>
                {
                    Code = 200,
                    Message = "操作成功",
                    Data = data
                };
            }
            catch (Exception ex)
            {
                return new Response<RepairOrderResp>
                {
                    Code = 500,
                    Message = $"获取报修单信息错误：{ex.Message}",
                    Data = null
                };
            }
        }

        /// <summary>
        /// 小程序用户提交报修
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        public async Task<Response<string>> Submit([FromBody] AddOrUpdateRepairOrderReq request)
        {
            try
            {
                // 测试版暂时将用户ID写死，后续需要从登录上下文中获取
                // 从当前登录上下文获取 UserId
                // var userId = GetWxUserId();
                // request.UserId = userId;

                var id = await _repairOrderApp.SubmitAsync(request);

                return new Response<string>
                {
                    Code = 200,
                    Message = "提交成功",
                    Data = id
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
        /// 后台管理员更新报修状态
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        public async Task<Response<bool>> UpdateStatus([FromBody] UpdateRepairStatusReq request)
        {
            try
            {
                if (request == null)
                {
                    return new Response<bool>
                    {
                        Code = 400,
                        Message = "请求参数不能为空",
                        Data = false
                    };
                }

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
        /// 获取当前用户的报修记录（小程序端"我的报修"）
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        public async Task<TableResp<RepairOrderResp>> MyRepairs([FromBody] QueryRepairOrderListReq request)
        {
            try
            {
                var userId = GetWxUserId();

                // 只查询当前用户的报修记录
                return await _repairOrderApp.QueryByUserAsync(userId, request);
            }
            catch (Exception ex)
            {
                return new TableResp<RepairOrderResp>
                {
                    Code = 500,
                    Message = ex.Message,
                    Data = null,
                    Count = 0,
                    Page = request?.page ?? 1,
                    Limit = request?.limit ?? 20
                };
            }
        }
    }
}