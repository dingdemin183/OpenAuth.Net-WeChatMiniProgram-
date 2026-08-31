// OpenAuth.App/SSO/LoginParse.cs
// 注意：要删除原有的 Do 方法里对 SysUser 表的依赖，或者保留不动（不影响微信登录）

using Infrastructure;
using Infrastructure.Cache;
using Infrastructure.Helpers;
using Infrastructure.Utilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAuth.Repository;
using OpenAuth.Repository.Domain;
using SqlSugar;
using System.Threading.Tasks;
using System;

namespace OpenAuth.App.SSO
{
    public class LoginParse
    {
        private readonly ISqlSugarClient SugarClient;
        private readonly ICacheContext _cacheContext;
        private readonly IOptions<AppSetting> _appConfiguration;
        private readonly WxMiniProgramService _wxService;
        private readonly ILogger<LoginParse> _logger;

        public LoginParse(
            ICacheContext cacheContext,
            ISqlSugarClient client,
            IOptions<AppSetting> appConfiguration,
            WxMiniProgramService wxService,
            ILogger<LoginParse> logger)
        {
            _cacheContext = cacheContext;
            _appConfiguration = appConfiguration;
            _wxService = wxService;
            _logger = logger;
            SugarClient = client;
        }

        // ============ 原有的 Do 方法保持不变（账号密码登录） ============
        public LoginResult Do(PassportLoginRequest model)
        {
            var result = new LoginResult();
            try
            {
                model.Trim();

                SysUser sysUserInfo = null;
                if (model.Account == Define.SYSTEM_USERNAME)
                {
                    sysUserInfo = new SysUser
                    {
                        Id = Guid.Empty.ToString(),
                        Account = Define.SYSTEM_USERNAME,
                        Name = "超级管理员",
                        Password = Define.SYSTEM_USERPWD
                    };
                }
                else
                {
                    sysUserInfo = SugarClient.Queryable<SysUser>()
                        .First(u => u.Account == model.Account);
                }

                if (sysUserInfo == null)
                {
                    throw new Exception("用户不存在");
                }
                if (sysUserInfo.Password != model.Password)
                {
                    throw new Exception("密码错误");
                }
                if (sysUserInfo.Status != 0)
                {
                    throw new Exception("账号状态异常，可能已停用");
                }

                var sessionId = Guid.NewGuid().ToString("N");
                var expireDays = _appConfiguration.Value.JwtExpireDays;

                var currentSession = new UserAuthSession
                {
                    Account = model.Account,
                    Name = sysUserInfo.Name,
                    Token = sessionId,
                    AppKey = model.AppKey,
                    CreateTime = TimeHelper.Now
                };

                _cacheContext.Set(sessionId, currentSession, TimeHelper.Now.AddDays(expireDays));

                var jwtToken = JwtTokenHelper.GenerateToken(
                    model.Account,
                    sysUserInfo.Name,
                    model.AppKey,
                    sessionId,
                    _appConfiguration.Value.JwtSecret,
                    expireDays
                );

                result.Code = 200;
                result.Token = jwtToken;
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
            }

            return result;
        }

        // ============ 新增：微信小程序登录（只用 SysUserExternalAuth 表） ============
        public async Task<LoginResult> WxMiniProgramLoginAsync(WxMiniProgramLoginRequest model)
        {
            var result = new LoginResult();
            try
            {
                // 1. 调用微信接口换取 openid
                var wxResult = await _wxService.GetOpenIdAndSessionKeyAsync(model.Code);

     
                var openId = wxResult.OpenId;
                var sessionKey = wxResult.SessionKey;

                if (string.IsNullOrEmpty(openId))
                {
                    result.Code = 500;
                    result.Message = "获取微信 OpenId 失败";
                    return result;
                }

                // 2. 根据 openid 查找或创建用户（直接操作 SysUserExternalAuth 表）
                var userAuth = await GetOrCreateUserAuthAsync(openId, wxResult.UnionId, sessionKey);

                // 3. 生成 Session 和 JWT Token（复用原逻辑）
                var sessionId = Guid.NewGuid().ToString("N");
                var expireDays = _appConfiguration.Value.JwtExpireDays;

                var currentSession = new UserAuthSession
                {
                    Account = openId, // 用 openid 作为账号
                    Name = userAuth.NickName ?? $"微信用户_{openId.Substring(0, 6)}",
                    Token = sessionId,
                    AppKey = model.AppKey,
                    CreateTime = TimeHelper.Now
                };

                _cacheContext.Set(sessionId, currentSession, TimeHelper.Now.AddDays(expireDays));

                var jwtToken = JwtTokenHelper.GenerateToken(
                    openId, // 用 openid 作为账号
                    currentSession.Name,
                    model.AppKey,
                    sessionId,
                    _appConfiguration.Value.JwtSecret,
                    expireDays
                );

                result.Code = 200;
                result.Token = jwtToken;
                result.Message = "登录成功";
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
                _logger.LogError($"微信登录异常: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 根据 OpenId 查找或创建用户（只用 SysUserExternalAuth 表）
        /// </summary>
        private async Task<SysUserExternalAuth> GetOrCreateUserAuthAsync(
            string openId,
            string unionId,
            string sessionKey)
        {
            // 1. 查询是否存在
            var existing = SugarClient.Queryable<SysUserExternalAuth>()
                .First(x => x.Provider == "WeChatMiniProgram" && x.OpenId == openId && !x.IsDeleted);

            if (existing != null)
            {
                // 更新 session_key
                if (!string.IsNullOrEmpty(sessionKey))
                {
                    existing.SessionKey = sessionKey;
                    existing.UpdateTime = DateTime.Now;
                    SugarClient.Updateable(existing).ExecuteCommand();
                }
                return existing;
            }

            // 2. 不存在 → 自动注册新用户
            var newAuth = new SysUserExternalAuth
            {
                Id = Guid.NewGuid().ToString("N"),
                Provider = "WeChatMiniProgram",
                OpenId = openId,
                UnionId = unionId,
                SessionKey = sessionKey,
                NickName = $"微信用户_{openId.Substring(0, 6)}",
                CreateTime = DateTime.Now,
                IsDeleted = false
            };

            SugarClient.Insertable(newAuth).ExecuteCommand();

            return newAuth;
        }
    }
}