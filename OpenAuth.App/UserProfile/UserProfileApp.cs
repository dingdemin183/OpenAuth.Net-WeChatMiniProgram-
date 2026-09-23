using Castle.Core.Logging;
using Infrastructure;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<UserProfileApp> _logger;

        public UserProfileApp(
            ISqlSugarClient db,
            IAuth auth,
            WxSecurityService wxSecurityService,
            WxAccessTokenService wxAccessTokenService,
            ILogger<UserProfileApp> logger)
        {
            _db = db;
            _auth = auth;
            _wxSecurityService = wxSecurityService;
            _accessTokenService = wxAccessTokenService;
            _logger = logger;
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
                .FirstAsync(x => x.Id == userId && x.IsDeleted == false)
                .ConfigureAwait(false);

            if (userAuth == null)
            {
                throw new CommonException("用户不存在");
            }

            var hasNickNameUpdate = false;
            var hasAvatarUpdate = false;

            // 更新昵称
            if (!string.IsNullOrEmpty(request.NickName))
            {
                userAuth.NickName = request.NickName;
                hasNickNameUpdate = true;
            }

            // 更新头像
            if (!string.IsNullOrEmpty(request.AvatarUrl))
            {
                userAuth.AvatarUrl = request.AvatarUrl;
                hasAvatarUpdate = true;
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
                }

                // 只把数据库写操作包在事务里
                await _db.Ado.BeginTranAsync();
                try
                {
                    await _db.Updateable(userAuth)
                        .UpdateColumns(updateColumns.ToArray())
                        .ExecuteCommandAsync()
                        .ConfigureAwait(false);

                    await _db.Ado.CommitTranAsync();
                }
                catch
                {
                    await _db.Ado.RollbackTranAsync();
                    throw;
                }
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
                .FirstAsync(x => x.Id == userId && x.IsDeleted == false)
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
                    // 保留原始异常信息，便于排查微信检测服务故障
                    _logger.LogError(ex, "头像检测服务异常：openId={OpenId}", openId);
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

                // 只把数据库写操作包在事务里
                await _db.Ado.BeginTranAsync();
                try
                {
                    await _db.Updateable(userAuth)
                        .UpdateColumns(updateColumns.ToArray())
                        .ExecuteCommandAsync()
                        .ConfigureAwait(false);

                    await _db.Ado.CommitTranAsync();
                }
                catch
                {
                    await _db.Ado.RollbackTranAsync();
                    throw;
                }
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
            // 参数校验：traceId 为外部回调输入，必须非空，否则会误命中 AvatarTraceId 为空/默认值的记录
            if (string.IsNullOrWhiteSpace(traceId))
            {
                _logger.LogWarning("微信媒体审核回调缺少 traceId，已忽略");
                return;
            }

            // 根据 trace_id 查找用户
            var userAuth = await _db.Queryable<SysUserExternalAuth>()
                .FirstAsync(x => x.AvatarTraceId == traceId && x.IsDeleted == false)
                .ConfigureAwait(false);

            if (userAuth == null)
            {
                _logger.LogWarning("微信媒体审核回调未匹配到用户：traceId={TraceId}", traceId);
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

            // 只把数据库写操作包在事务里
            await _db.Ado.BeginTranAsync();
            try
            {
                await _db.Updateable(userAuth)
                    .UpdateColumns(x => new {
                        x.AvatarUrl,
                        x.AvatarAuditStatus,
                        x.UpdateTime
                    })
                    .ExecuteCommandAsync()
                    .ConfigureAwait(false);

                await _db.Ado.CommitTranAsync();
            }
            catch
            {
                await _db.Ado.RollbackTranAsync();
                throw;
            }
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
                .FirstAsync(x => x.Id == userId && x.IsDeleted == false)
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