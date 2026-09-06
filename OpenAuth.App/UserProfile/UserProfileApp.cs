using Castle.Core.Logging;
using Infrastructure;
using OpenAuth.App.Interface;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using OpenAuth.App.SSO;
using OpenAuth.App.WxPay;
using OpenAuth.Repository.Domain;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OpenAuth.App.UserProfile
{
    /// <summary>
    /// 用户个人资料管理
    /// </summary>
    public class UserProfileApp
    {
        private readonly ISqlSugarClient _db;
        private readonly IAuth _auth;
        private readonly WxSecurityService _wxSecurityService;
        private readonly WxAccessTokenService _accessTokenService;
       

        public UserProfileApp(
            ISqlSugarClient db,
            IAuth auth,
            WxSecurityService wxSecurityService,
            WxAccessTokenService wxAccessTokenService)
        {
            _db = db;
            _auth = auth;
            _wxSecurityService = wxSecurityService;
            _accessTokenService = wxAccessTokenService;
        }

        /// <summary>
        /// 同步更新用户个人资料（头像+昵称）- 使用同步检测
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        /// <CommonException cref="CommonException"></CommonException>
        public async Task<UserProfileResp> UpdateProfileSyncAsync(UpdateUserProfileReq request)
        {
            if (request == null)
                throw new CommonException("请求参数不能为空");

            // 获取当前登录用户
            var session = _auth.GetCurrentSession();

            if (session?.UserId == null)
            {
                throw new CommonException("用户未登录");
            }

            var userId = session.UserId;

            // 查询用户第三方认证信息
            var userAuth = await _db.Queryable<SysUserExternalAuth>()
                .FirstAsync(x => x.Id == userId && !x.IsDeleted)
                .ConfigureAwait(false);

            if (userAuth == null)
            {
                throw new CommonException("用户不存在");
            }

            var hasNickNameUpdate = false;
            var hasAvatarUpdate = false;

            // 检测昵称（同步检测）
            if (!string.IsNullOrEmpty(request.NickName))
            {
                // userAuth.OpenId = "oH5Zc3TxmUx8_IlHtAOTT3JLVlwg";  //写一个正确的openid做测试
                var isTextSafe = await _wxSecurityService.CheckTextSecurityAsync(userAuth.OpenId, request.NickName);
                if (!isTextSafe)
                {
                    throw new CommonException("昵称包含违规内容，请修改");
                }

                userAuth.NickName = request.NickName;
                hasNickNameUpdate = true;
            }

            // 同步检测头像
            if (!string.IsNullOrEmpty(request.AvatarUrl))
            {
                try
                {
                    // 同步检测图片是否安全
                    var isImageSafe = await _wxSecurityService.CheckImageSecuritySyncAsync(request.AvatarUrl);

                    if (!isImageSafe)
                    {
                        throw new CommonException("头像包含违规内容，请更换");
                    }

                    // 检测通过，直接更新头像
                    userAuth.AvatarUrl = request.AvatarUrl;
                    // 如果有待审核的头像，清理掉
                    userAuth.PendingAvatarUrl = null;
                    userAuth.AvatarAuditStatus = "pass";
                    userAuth.AvatarTraceId = null;
                    hasAvatarUpdate = true;
                }
                catch (Exception ex) when (ex is CommonException)
                {
                    throw; // 重新抛出业务异常
                }
                catch (Exception ex)
                {
                    
                    throw new CommonException("头像检测服务异常，请稍后重试");
                }
            }

            // 一次性保存所有更新
            if (hasNickNameUpdate || hasAvatarUpdate)
            {
                userAuth.UpdateTime = DateTime.Now;

                // 构建需要更新的字段列表
                var updateColumns = new List<string> { "UpdateTime" };

                if (hasNickNameUpdate)
                {
                    updateColumns.Add("NickName");
                }

                if (hasAvatarUpdate)
                {
                    updateColumns.Add("AvatarUrl");
                    updateColumns.Add("PendingAvatarUrl");
                    updateColumns.Add("AvatarAuditStatus");
                    updateColumns.Add("AvatarTraceId");
                }

                await _db.Updateable(userAuth)
                    .UpdateColumns(updateColumns.ToArray())
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);
            }

            // 构建返回结果
            var resp = new UserProfileResp
            {
                Id = userAuth.Id,
                OpenId = userAuth.OpenId,
                NickName = userAuth.NickName,
                AvatarUrl = userAuth.AvatarUrl,
                UserPhone = userAuth.UserPhone,
                PendingAvatarUrl = userAuth.PendingAvatarUrl,
                AvatarAuditStatus = userAuth.AvatarAuditStatus
            };

            // 根据更新情况返回不同的消息
            if (hasAvatarUpdate && hasNickNameUpdate)
            {
                resp.Message = "昵称和头像更新成功";
            }
            else if (hasAvatarUpdate)
            {
                resp.Message = "头像更新成功";
            }
            else if (hasNickNameUpdate)
            {
                resp.Message = "昵称更新成功";
            }
            else
            {
                resp.Message = "没有需要更新的内容";
            }

            return resp;
        }



        /// <summary>
        /// 更新用户个人资料（头像+昵称）（异步校验图片）
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        /// <CommonException cref="CommonException"></CommonException>
        public async Task<UserProfileResp> UpdateProfileAsync(UpdateUserProfileReq request)
        {
            if (request == null)
                throw new CommonException("请求参数不能为空");

            // 获取当前登录用户
            var session = _auth.GetCurrentSession();

            if (session?.UserId == null)
            {
                throw new CommonException("用户未登录");
            }

            var userId = session.UserId;

            // 查询用户第三方认证信息
            var userAuth = await _db.Queryable<SysUserExternalAuth>()
                .FirstAsync(x => x.Id == userId && !x.IsDeleted)
                .ConfigureAwait(false);

            if (userAuth == null)
            {
                throw new CommonException("用户不存在");
            }

            var openId = userAuth.OpenId;
            var hasNickNameUpdate = false;
            var hasAvatarPending = false;

            //  检测昵称（同步检测，立即生效）
            if (!string.IsNullOrEmpty(request.NickName))
            {
                var isTextSafe = await _wxSecurityService.CheckTextSecurityAsync(openId, request.NickName);
                if (!isTextSafe)
                {
                    throw new CommonException("昵称包含违规内容，请修改");
                }

                userAuth.NickName = request.NickName;
                hasNickNameUpdate = true;
            }

            //  检测头像（异步检测）
            if (!string.IsNullOrEmpty(request.AvatarUrl))
            {
                try
                {
                    // 提交图片检测任务
                    var traceId = await _wxSecurityService.CheckImageSecurityAsync(openId, request.AvatarUrl);

                    // 保存待审核信息到用户表
                    userAuth.PendingAvatarUrl = request.AvatarUrl;
                    userAuth.AvatarAuditStatus = "pending";
                    userAuth.AvatarTraceId = traceId;
                    userAuth.UpdateTime = DateTime.Now;
                    hasAvatarPending = true;
                }
                catch (Exception ex)
                {
                    throw new CommonException("头像检测服务异常，请稍后重试");
                }
            }

            // 一次性保存所有更新
            if (hasNickNameUpdate || hasAvatarPending)
            {
                userAuth.UpdateTime = DateTime.Now;

                // 构建需要更新的字段列表
                var updateColumns = new List<string> { "UpdateTime" };

                if (hasNickNameUpdate)
                {
                    updateColumns.Add("NickName");
                }

                if (hasAvatarPending)
                {
                    updateColumns.Add("PendingAvatarUrl");
                    updateColumns.Add("AvatarAuditStatus");
                    updateColumns.Add("AvatarTraceId");
                }

                await _db.Updateable(userAuth)
                    .UpdateColumns(updateColumns.ToArray())
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);
            }

            // 构建返回结果
            var resp = new UserProfileResp
            {
                Id = userAuth.Id,
                OpenId = userAuth.OpenId,
                NickName = userAuth.NickName,
                AvatarUrl = userAuth.AvatarUrl,
                UserPhone = userAuth.UserPhone,
                PendingAvatarUrl = userAuth.PendingAvatarUrl,
                AvatarAuditStatus = userAuth.AvatarAuditStatus
            };

            // 根据状态返回不同的消息
            if (hasAvatarPending && hasNickNameUpdate)
            {
                resp.Message = "昵称已更新，头像正在审核中，请稍后查看";
            }
            else if (hasAvatarPending)
            {
                resp.Message = "头像正在审核中，请稍后查看";
            }
            else if (hasNickNameUpdate)
            {
                resp.Message = "昵称更新成功";
            }
            else
            {
                resp.Message = "没有需要更新的内容";
            }

            return resp;
        }

        /// <summary>
        /// 处理微信异步审核回调
        /// </summary>
        public async Task HandleMediaCheckResultAsync(string traceId, string suggest)
        {
            // 根据 trace_id 查找用户
            var userAuth = await _db.Queryable<SysUserExternalAuth>()
                .FirstAsync(x => x.AvatarTraceId == traceId && !x.IsDeleted)
                .ConfigureAwait(false);

            if (userAuth == null)
            {
                // 记录日志：找不到对应的用户
                return;
            }

            if (suggest == "pass")
            {
                // 审核通过：将待审核头像设为正式头像
                userAuth.AvatarUrl = userAuth.PendingAvatarUrl;
                userAuth.AvatarAuditStatus = "pass";
            }
            else
            {
                // 审核不通过
                userAuth.AvatarAuditStatus = "reject";
                // 可选：保留原头像不变，不清除 PendingAvatarUrl 用于展示
            }

            userAuth.UpdateTime = DateTime.Now;

            await _db.Updateable(userAuth)
                .UpdateColumns(x => new {
                    x.AvatarUrl,
                    x.AvatarAuditStatus,
                    x.UpdateTime
                })
                .ExecuteCommandAsync()
                .ConfigureAwait(false);
        }

        /// <summary>
        /// 获取当前用户资料
        /// </summary>
        /// <returns></returns>
        /// <CommonException cref="CommonException"></CommonException>
        public async Task<UserProfileResp> GetMyProfileAsync()
        {
            var session = _auth.GetCurrentSession();
            if (session?.UserId == null)
            {
                throw new CommonException("用户未登录");
            }

            var userId = session?.UserId;

            var userAuth = await _db.Queryable<SysUserExternalAuth>()
                .FirstAsync(x => x.Id == userId && !x.IsDeleted)
                .ConfigureAwait(false);

            if (userAuth == null)
            {
                throw new CommonException("用户不存在");
            }

            return new UserProfileResp
            {
                Id = userAuth.Id,
                OpenId = userAuth.OpenId,
                NickName = userAuth.NickName,
                AvatarUrl = userAuth.AvatarUrl,
                UserPhone = userAuth.UserPhone,
                PendingAvatarUrl = userAuth.PendingAvatarUrl,
                AvatarAuditStatus = userAuth.AvatarAuditStatus
            };
        }
    }
}