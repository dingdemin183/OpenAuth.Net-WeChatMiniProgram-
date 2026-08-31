using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenAuth.App;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenAuth.IdentityServer.Controllers
{
    /// <summary>
    /// 处理 OpenID Connect Userinfo 请求
    /// </summary>
    public class UserinfoController : Controller
    {
        private readonly UserManagerApp _userManager;

        public UserinfoController(UserManagerApp userManager)
        {
            _userManager = userManager;
        }

        /// <summary>
        /// 处理 /connect/userinfo 请求，返回已认证用户的个人信息
        /// </summary>
        [HttpGet("~/connect/userinfo")]
        [HttpPost("~/connect/userinfo")]
        public async Task<IActionResult> Userinfo()
        {
            var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            if (!result.Succeeded)
            {
                return Challenge(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "访问令牌无效或已过期。"
                    }));
            }

            var userId = result.Principal.GetClaim(Claims.Subject);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidToken,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "令牌中缺少用户标识。"
                    }));
            }

            // 构建 userinfo 响应
            var claims = new Dictionary<string, object>
            {
                [Claims.Subject] = userId
            };

            // 如果请求了 profile scope，返回用户名称信息
            if (result.Principal.HasScope(Scopes.Profile))
            {
                var user = GetUser(userId);
                if (user != null)
                {
                    claims[Claims.Name] = user.Name ?? user.Account;
                    claims[Claims.PreferredUsername] = user.Account;
                }
            }

            return Ok(claims);
        }

        private OpenAuth.Repository.Domain.SysUser GetUser(string id)
        {
            if (id == Define.SYSTEM_USERNAME)
            {
                return new OpenAuth.Repository.Domain.SysUser
                {
                    Account = Define.SYSTEM_USERNAME,
                    Id = Define.SYSTEM_USERNAME,
                    Name = Define.SYSTEM_USERNAME
                };
            }

            return _userManager.Get(id);
        }
    }
}
