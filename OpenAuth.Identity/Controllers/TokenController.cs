using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using OpenAuth.App;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenAuth.IdentityServer.Controllers
{
    /// <summary>
    /// 处理 OpenID Connect Token 请求
    /// </summary>
    public class TokenController : Controller
    {
        private readonly UserManagerApp _userManager;

        public TokenController(UserManagerApp userManager)
        {
            _userManager = userManager;
        }

        /// <summary>
        /// 处理令牌端点请求 /connect/token
        /// </summary>
        [HttpPost("~/connect/token")]
        public async Task<IActionResult> Exchange()
        {
            var request = HttpContext.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("无法获取 OpenID Connect 请求。");

            if (request.IsAuthorizationCodeGrantType())
            {
                // 从授权码中还原 ClaimsPrincipal
                var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
                if (!result.Succeeded)
                {
                    return Forbid(
                        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                        properties: new AuthenticationProperties(new Dictionary<string, string>
                        {
                            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "授权码无效或已过期。"
                        }));
                }

                var principal = result.Principal;

                // 可选：补充最新的用户信息到 Claims
                var userId = principal.GetClaim(Claims.Subject);
                if (!string.IsNullOrEmpty(userId))
                {
                    var user = GetUser(userId);
                    if (user == null || user.Status != 0)
                    {
                        return Forbid(
                            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                            properties: new AuthenticationProperties(new Dictionary<string, string>
                            {
                                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "用户不存在或已被禁用。"
                            }));
                    }
                }

                // 设置 Claims 目标
                foreach (var claim in principal.Claims)
                {
                    claim.SetDestinations(GetDestinations(claim, principal));
                }

                // 确保 access_token 包含正确的 audience
                principal.SetResources("openauthapi");

                return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            throw new InvalidOperationException("不支持的授权类型。");
        }

        /// <summary>
        /// 根据用户ID获取用户
        /// </summary>
        private OpenAuth.Repository.Domain.SysUser GetUser(string id)
        {
            if (id == Define.SYSTEM_USERNAME)
            {
                return new OpenAuth.Repository.Domain.SysUser
                {
                    Account = Define.SYSTEM_USERNAME,
                    Id = Define.SYSTEM_USERNAME,
                    Name = Define.SYSTEM_USERNAME,
                    Status = 0
                };
            }

            return _userManager.Get(id);
        }

        /// <summary>
        /// 确定 Claim 的目标
        /// </summary>
        private static IEnumerable<string> GetDestinations(Claim claim, ClaimsPrincipal principal)
        {
            switch (claim.Type)
            {
                case Claims.Name:
                    yield return Destinations.AccessToken;
                    if (principal.HasScope(Scopes.Profile))
                        yield return Destinations.IdentityToken;
                    yield break;

                case ClaimTypes.Name:
                    yield return Destinations.AccessToken;
                    if (principal.HasScope(Scopes.Profile))
                        yield return Destinations.IdentityToken;
                    yield break;

                case Claims.Subject:
                    yield return Destinations.AccessToken;
                    yield return Destinations.IdentityToken;
                    yield break;

                default:
                    yield return Destinations.AccessToken;
                    yield break;
            }
        }
    }
}
