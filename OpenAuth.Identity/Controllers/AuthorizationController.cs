using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenAuth.App;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace OpenAuth.IdentityServer.Controllers
{
    /// <summary>
    /// 处理 OpenID Connect 授权请求
    /// </summary>
    public class AuthorizationController : Controller
    {
        private readonly UserManagerApp _userManager;

        public AuthorizationController(UserManagerApp userManager)
        {
            _userManager = userManager;
        }

        /// <summary>
        /// 处理授权端点请求 /connect/authorize
        /// </summary>
        [HttpGet("~/connect/authorize")]
        [HttpPost("~/connect/authorize")]
        public async Task<IActionResult> Authorize()
        {
            var request = HttpContext.GetOpenIddictServerRequest()
                ?? throw new InvalidOperationException("无法获取 OpenID Connect 请求。");

            // 尝试获取已登录用户
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!result.Succeeded)
            {
                // 未登录 → 重定向到登录页面
                return Challenge(
                    authenticationSchemes: new[] { CookieAuthenticationDefaults.AuthenticationScheme },
                    properties: new AuthenticationProperties
                    {
                        RedirectUri = Request.PathBase + Request.Path + QueryString.Create(
                            Request.HasFormContentType ? Request.Form.ToList() : Request.Query.ToList())
                    });
            }

            // 已登录 → 从 Cookie 中获取用户信息
            var userId = result.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge(
                    authenticationSchemes: new[] { CookieAuthenticationDefaults.AuthenticationScheme },
                    properties: new AuthenticationProperties
                    {
                        RedirectUri = Request.PathBase + Request.Path + QueryString.Create(
                            Request.HasFormContentType ? Request.Form.ToList() : Request.Query.ToList())
                    });
            }

            // 加载用户信息
            var user = GetUser(userId);
            if (user == null)
            {
                return Forbid(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            }

            // 构建 Claims
            var claims = new List<Claim>
            {
                new Claim(Claims.Subject, user.Id),
                new Claim(Claims.Name, user.Name ?? user.Account),
                new Claim(ClaimTypes.Name, user.Account),
            };

            var identity = new ClaimsIdentity(claims, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            // 设置请求的 scopes
            principal.SetScopes(request.GetScopes());

            // 设置 access_token 的 audience（对应 WebApi 的 options.Audience）
            principal.SetResources("openauthapi");

            // 设置 Claims 目标（决定哪些 Claims 出现在 access_token / id_token 中）
            foreach (var claim in principal.Claims)
            {
                claim.SetDestinations(GetDestinations(claim, principal));
            }

            // 签发授权码
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        /// <summary>
        /// 处理登出端点请求 /connect/logout
        /// </summary>
        [HttpGet("~/connect/logout")]
        [HttpPost("~/connect/logout")]
        public async Task<IActionResult> Logout()
        {
            // 清除本地 Cookie
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // 通知 OpenIddict 登出完成
            return SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
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
                    Name = Define.SYSTEM_USERNAME
                };
            }

            return _userManager.Get(id);
        }

        /// <summary>
        /// 确定 Claim 的目标（access_token / id_token）
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
