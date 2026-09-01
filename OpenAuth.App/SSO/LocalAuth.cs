using Infrastructure;
using Infrastructure.Cache;
using Infrastructure.Domain;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OpenAuth.App.Interface;
using OpenAuth.Repository.Domain;
using SqlSugar;
using System;

namespace OpenAuth.App.SSO
{
    /// <summary>
    /// 使用本地登录。这个注入IAuth时，只需要OpenAuth.Mvc一个项目即可，无需webapi的支持
    /// </summary>
    public class LocalAuth : IAuth
    {
        private IHttpContextAccessor _httpContextAccessor;
        private IOptions<AppSetting> _appConfiguration;
        private SysLogApp _logApp;

        private AuthContextFactory _app;
        private LoginParse _loginParse;
        private ICacheContext _cacheContext;
        private readonly ISqlSugarClient _sugarClient;

        public LocalAuth(IHttpContextAccessor httpContextAccessor
            , AuthContextFactory app
            , LoginParse loginParse
            , ICacheContext cacheContext, IOptions<AppSetting> appConfiguration, SysLogApp logApp, ISqlSugarClient sugarClient)
        {
            _httpContextAccessor = httpContextAccessor;
            _app = app;
            _loginParse = loginParse;
            _cacheContext = cacheContext;
            _appConfiguration = appConfiguration;
            _logApp = logApp;
            _sugarClient = sugarClient;
        }

        /// <summary>
        /// 【新增】获取当前登录的微信小程序用户信息
        /// </summary>
        /// <returns>微信用户信息，如果不是微信登录则返回 null</returns>
        public WxUserInfo GetCurrentWxUserInfo()
        {
            try
            {
                var token = GetToken();
                if (string.IsNullOrEmpty(token)) return null;

                // 验证 Token 有效性
                var principal = JwtTokenHelper.ValidateToken(token, _appConfiguration.Value.JwtSecret);
                if (principal == null) return null;

                // 从 Token 提取 Account
                var account = JwtTokenHelper.GetAccount(token);
                if (string.IsNullOrEmpty(account)) return null;

                // 验证会话是否存在
                var sessionId = GetSessionIdFromToken(token);
                if (string.IsNullOrEmpty(sessionId)) return null;

                var session = _cacheContext.Get<UserAuthSession>(sessionId);
                if (session == null) return null;

                // 判断是否为微信小程序登录
                if (session.AppKey != "miniprogram") return null;

                // 从数据库查询完整的微信用户信息
                var wxUser = _sugarClient.Queryable<SysUserExternalAuth>()
                    .First(x => x.OpenId == account && x.Provider == "WeChatMiniProgram" && !x.IsDeleted);

                if (wxUser == null) return null;

                return new WxUserInfo
                {
                    OpenId = wxUser.OpenId,
                    UnionId = wxUser.UnionId,
                    NickName = wxUser.NickName,
                    SessionKey = wxUser.SessionKey,
                    AvatarUrl = wxUser.AvatarUrl, // 如果有此字段
                    CreateTime = wxUser.CreateTime,
                    // 从 Session 获取额外信息
                    LoginTime = session.CreateTime,
                    Token = token
                };
            }
            catch (Exception ex)
            {
                // 记录日志但不抛出，避免影响业务
                return null;
            }
        }

        /// <summary>
        /// 【新增】获取当前登录用户信息（统一接口，支持所有登录方式）
        /// </summary>
        public CurrentUserInfo GetCurrentUserInfo()
        {
            try
            {
                var token = GetToken();
                if (string.IsNullOrEmpty(token)) return null;

                var principal = JwtTokenHelper.ValidateToken(token, _appConfiguration.Value.JwtSecret);
                if (principal == null) return null;

                var account = JwtTokenHelper.GetAccount(token);
                if (string.IsNullOrEmpty(account)) return null;

                var sessionId = GetSessionIdFromToken(token);
                if (string.IsNullOrEmpty(sessionId)) return null;

                var session = _cacheContext.Get<UserAuthSession>(sessionId);
                if (session == null) return null;

                var userInfo = new CurrentUserInfo
                {
                    Account = account,
                    Name = session.Name,
                    AppKey = session.AppKey,
                    LoginTime = session.CreateTime,
                    Token = token
                };

                // 如果是微信登录，补充微信信息
                if (session.AppKey == "miniprogram")
                {
                    var wxUser = _sugarClient.Queryable<SysUserExternalAuth>()
                        .First(x => x.OpenId == account && x.Provider == "WeChatMiniProgram" && !x.IsDeleted);

                    if (wxUser != null)
                    {
                        userInfo.OpenId = wxUser.OpenId;
                        userInfo.UnionId = wxUser.UnionId;
                        userInfo.NickName = wxUser.NickName;
                        userInfo.AvatarUrl = wxUser.AvatarUrl;
                        userInfo.LoginProvider = "WeChatMiniProgram";
                    }
                }
                else
                {
                    userInfo.LoginProvider = "Local";
                    // 如果是本地登录，可以查 SysUser 表
                    var sysUser = _sugarClient.Queryable<SysUser>()
                        .First(x => x.Account == account);
                    if (sysUser != null)
                    {
                        userInfo.Name = sysUser.Name;
                        userInfo.UserId = sysUser.Id;
                    }
                }

                return userInfo;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 【新增】直接从缓存获取 Session（不查数据库，性能最好）
        /// </summary>
        public UserAuthSession GetCurrentSession()
        {
            try
            {
                var token = GetToken();
                if (string.IsNullOrEmpty(token)) return null;

                var sessionId = GetSessionIdFromToken(token);
                if (string.IsNullOrEmpty(sessionId)) return null;

                return _cacheContext.Get<UserAuthSession>(sessionId);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 如果是Identity，则返回信息为用户账号
        /// 如果是本地认证，则返回JWT Token字符串
        /// </summary>
        /// <returns></returns>
        private string GetToken()
        {
            if (_appConfiguration.Value.IsIdentityAuth)
            {
                return _httpContextAccessor.HttpContext.User.Identity.Name;
            }
            string token = _httpContextAccessor.HttpContext.Request.Query[Define.TOKEN_NAME];
            if (!String.IsNullOrEmpty(token)) return token;

            token = _httpContextAccessor.HttpContext.Request.Headers[Define.TOKEN_NAME];
            if (!String.IsNullOrEmpty(token)) return token;

            var cookie = _httpContextAccessor.HttpContext.Request.Cookies[Define.TOKEN_NAME];
            return cookie ?? String.Empty;
        }

        /// <summary>
        /// 从JWT Token中提取会话ID（jti），用于缓存查找
        /// </summary>
        private string GetSessionIdFromToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            return JwtTokenHelper.GetSessionId(token);
        }

        public bool CheckLogin(string token = "", string otherInfo = "")
        {
            if (_appConfiguration.Value.IsIdentityAuth)
            {
                return !string.IsNullOrEmpty(_httpContextAccessor.HttpContext.User.Identity.Name);
            }

            if (string.IsNullOrEmpty(token))
            {
                token = GetToken();
            }

            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            try
            {
                // 验证JWT Token签名和有效期
                var principal = JwtTokenHelper.ValidateToken(token, _appConfiguration.Value.JwtSecret);
                if (principal == null)
                {
                    return false;
                }

                // 检查会话是否在缓存中（确保未被登出失效）
                var sessionId = GetSessionIdFromToken(token);
                if (string.IsNullOrEmpty(sessionId))
                {
                    return false;
                }

                var result = _cacheContext.Get<UserAuthSession>(sessionId) != null;
                return result;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        /// <summary>
        /// 获取当前登录的用户信息
        /// <para>通过URL中的Token参数或Cookie中的Token</para>
        /// </summary>
        /// <param name="account">The account.</param>
        /// <returns>LoginUserVM.</returns>
        public AuthStrategyContext GetCurrentUser()
        {
            if (_appConfiguration.Value.IsIdentityAuth)
            {
                return _app.GetAuthStrategyContext(GetToken());
            }
            AuthStrategyContext context = null;
            var token = GetToken();
            
            // 从JWT Token中直接提取用户账号
            var account = JwtTokenHelper.GetAccount(token);
            if (!string.IsNullOrEmpty(account))
            {
                // 验证会话是否有效（未被登出）
                var sessionId = GetSessionIdFromToken(token);
                if (!string.IsNullOrEmpty(sessionId))
                {
                    var session = _cacheContext.Get<UserAuthSession>(sessionId);
                    if (session != null)
                    {
                        context = _app.GetAuthStrategyContext(account);
                    }
                }
            }
            return context;
        }

        /// <summary>
        /// 获取当前登录的用户名
        /// <para>通过JWT Token中的claims直接提取用户账号</para>
        /// </summary>
        /// <param name="otherInfo">The account.</param>
        /// <returns>System.String.</returns>
        public string GetUserName(string otherInfo = "")
        {
            if (_appConfiguration.Value.IsIdentityAuth)
            {
                return _httpContextAccessor.HttpContext.User.Identity.Name;
            }

            var token = GetToken();
            // 从JWT Token中提取用户账号
            var account = JwtTokenHelper.GetAccount(token);
            if (!string.IsNullOrEmpty(account))
            {
                // 验证会话是否有效
                var sessionId = GetSessionIdFromToken(token);
                if (!string.IsNullOrEmpty(sessionId))
                {
                    var session = _cacheContext.Get<UserAuthSession>(sessionId);
                    if (session != null)
                    {
                        return account;
                    }
                }
            }

            return "";
        }

        /// <summary>
        /// 登录接口
        /// </summary>
        /// <param name="appKey">应用程序key.</param>
        /// <param name="username">用户名</param>
        /// <param name="pwd">密码</param>
        /// <returns>System.String.</returns>
        public LoginResult Login(string appKey, string username, string pwd)
        {
            if (_appConfiguration.Value.IsIdentityAuth)
            {
                return new LoginResult
                {
                    Code = 500,
                    Message = "接口启动了OAuth认证,暂时不能使用该方式登录"
                };
            }

            var result = _loginParse.Do(new PassportLoginRequest
            {
                AppKey = appKey,
                Account = username,
                Password = pwd
            });

            var log = new SysLog
            {
                Content = $"用户登录,结果：{result.Message}",
                Result = result.Code == 200 ? 0 : 1,
                CreateId = username,
                CreateName = username,
                TypeName = "登录日志"
            };
            _logApp.Add(log);

            return result;
        }

        /// <summary>
        /// 注销，如果是Identity登录，需要在controller处理注销逻辑
        /// </summary>
        public bool Logout()
        {
            var token = GetToken();
            if (String.IsNullOrEmpty(token)) return true;

            try
            {
                // 从JWT Token中提取会话ID，删除缓存
                var sessionId = GetSessionIdFromToken(token);
                if (!string.IsNullOrEmpty(sessionId))
                {
                    _cacheContext.Remove(sessionId);
                }
                return true;
            }
            catch
            {
                return false;
            }

            
        }
    }
}