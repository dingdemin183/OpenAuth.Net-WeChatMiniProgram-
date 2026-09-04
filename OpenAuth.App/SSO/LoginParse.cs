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

        /// <summary>
        /// 账号密码登录
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 微信小程序一键登录（登录 + 获取手机号，一次完成）
        /// </summary>
        public async Task<LoginResult> WxMiniProgramLoginWithPhoneAsync(WxMiniProgramLoginWithPhoneRequest model, string userIp)
        {
            var result = new LoginResult();
            try
            {
                // 用 loginCode 换 openid 
                var wxSessionResult = await _wxService.GetOpenIdAndSessionKeyAsync(model.LoginCode);

                var openId = wxSessionResult.OpenId;
                var sessionKey = wxSessionResult.SessionKey;

                if (string.IsNullOrEmpty(openId))
                {
                    result.Code = 500;
                    result.Message = "获取微信 OpenId 失败";
                    return result;
                }

                // 用 phoneCode 换手机号
                var phoneResult = await _wxService.GetUserPhoneAsync(model.PhoneCode);

                if (phoneResult.ErrCode != 0)
                {
                    result.Code = 500;
                    result.Message = $"获取手机号失败: {phoneResult.ErrMsg}";
                    return result;
                }

                var phoneNumber = phoneResult.PhoneInfo?.PhoneNumber;
                //其他获取手机号方式
                phoneNumber = phoneResult.PhoneInfo.PurePhoneNumber;
                



                if (string.IsNullOrEmpty(phoneNumber))
                {


                    result.Code = 500;
                    result.Message = "获取手机号失败，返回数据为空";
                    return result;
                }

                //根据 openid 查找或创建用户，并保存手机号 
                var userAuth = await GetOrCreateUserAuthWithPhoneAsync(openId, phoneNumber, wxSessionResult.UnionId, sessionKey,userIp);

                // 生成 Session 和 JWT Token 
                var sessionId = Guid.NewGuid().ToString("N");
                var expireDays = _appConfiguration.Value.JwtExpireDays;

                var currentSession = new UserAuthSession
                {
                    Account = openId,
                    Name = userAuth.NickName ?? $"微信用户_{openId.Substring(0, 6)}",
                    UserId = userAuth.Id,
                    Token = sessionId,
                    AppKey = "miniprogram",
                    CreateTime = TimeHelper.Now
                };

                _cacheContext.Set(sessionId, currentSession, TimeHelper.Now.AddDays(expireDays));

                var jwtToken = JwtTokenHelper.GenerateToken(
                    openId,
                    currentSession.Name,
                    "miniprogram",
                    sessionId,
                    _appConfiguration.Value.JwtSecret,
                    expireDays
                );
                result.Code = 200;
                result.Token = jwtToken;
                result.Message = "登录成功";
                result.Data = phoneNumber;

                _logger.LogInformation($"微信一键登录成功: openId={openId}, phone={phoneNumber}");
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
                _logger.LogError($"微信一键登录异常: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// 根据 OpenId 查找或创建用户，并保存手机号
        /// </summary>
        private async Task<SysUserExternalAuth> GetOrCreateUserAuthWithPhoneAsync(
            string openId,
            string phoneNumber,
            string unionId,
            string sessionKey,
            string userIp)
        {
            // 查询是否存在
            var existing = SugarClient.Queryable<SysUserExternalAuth>()
                .First(x => x.Provider == "WeChatMiniProgram" && x.OpenId == openId && !x.IsDeleted);

            if (existing != null)
            {
                // 更新手机号和 session_key
                existing.UserPhone = phoneNumber;
                existing.SessionKey = sessionKey;
                existing.UpdateTime = DateTime.Now;
                existing.LastLoginIp = userIp;
                SugarClient.Updateable(existing).ExecuteCommand();
                return existing;
            }

            // 不存在则自动注册新用户
            var newAuth = new SysUserExternalAuth
            {
                Id = Guid.NewGuid().ToString("N"),
                Provider = "WeChatMiniProgram",
                OpenId = openId,
                UnionId = unionId,
                SessionKey = sessionKey,
                UserPhone = phoneNumber,  // 直接存手机号
                NickName = $"微信用户_{openId.Substring(0, 6)}",
                LastLoginIp=userIp,
                CreateTime = DateTime.Now,
                IsDeleted = false
            };

            await SugarClient.Insertable(newAuth).ExecuteCommandAsync().ConfigureAwait(false);

            return newAuth;
        }

       

        /// <summary>
        /// 更新用户手机号
        /// </summary>
        /// <param name="openId">openid</param>
        /// <param name="phoneNumber">手机号</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<bool> UpdateUserPhoneAsync(string openId, string phoneNumber)
        {
            if (string.IsNullOrEmpty(openId))
                throw new Exception("OpenId 不能为空");

            if (string.IsNullOrEmpty(phoneNumber))
                throw new Exception("手机号不能为空");

            var userAuth = SugarClient.Queryable<SysUserExternalAuth>()
                .First(x => x.OpenId == openId && !x.IsDeleted);

            if (userAuth == null)
                throw new Exception("用户不存在");

            userAuth.UserPhone = phoneNumber;
            userAuth.UpdateTime = DateTime.Now;

            var result = await SugarClient.Updateable(userAuth)
                .UpdateColumns(x => new { x.UserPhone, x.UpdateTime })
                .ExecuteCommandAsync()
                .ConfigureAwait(false);

            return result > 0;
        }
    }
}