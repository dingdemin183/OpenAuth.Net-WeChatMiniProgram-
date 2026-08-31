using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace OpenAuth.IdentityServer.Handlers
{
    /// <summary>
    /// 降级模式下的自定义授权请求验证 handler
    /// 验证 client_id 和 redirect_uri 是否合法
    /// </summary>
    public class ValidateAuthorizationRequestHandler : IOpenIddictServerHandler<ValidateAuthorizationRequestContext>
    {
        /// <summary>
        /// 已注册的客户端配置
        /// </summary>
        private static readonly Dictionary<string, ClientConfig> Clients = new(StringComparer.OrdinalIgnoreCase)
        {
            ["OpenAuth.WebApi"] = new ClientConfig
            {
                RedirectUris = new[]
                {
                    "http://localhost:52789",
                    "http://localhost:52789/swagger/oauth2-redirect.html",
                    "http://localhost:1803",
                    "http://localhost:1803/callback",
                    "http://localhost:1803/oidc-callback",
                },
                AllowedScopes = new[] { "openid", "profile", "openauthapi" }
            },
            ["OpenAuth.Pro"] = new ClientConfig
            {
                RedirectUris = new[]
                {
                    "http://localhost:1803",
                    "http://localhost:1803/callback",
                    "http://localhost:1803/oidc-callback",
                    "http://localhost:1803/oidc-callback.html",
                },
                AllowedScopes = new[] { "openid", "profile", "openauthapi" }
            },
            ["OpenAuth.Mvc"] = new ClientConfig
            {
                RedirectUris = new[]
                {
                    "http://localhost:5001",
                    "http://localhost:5001/signin-oidc",
                },
                AllowedScopes = new[] { "openid", "profile", "openauthapi" }
            }
        };

        public ValueTask HandleAsync(ValidateAuthorizationRequestContext context)
        {
            // 验证 client_id
            if (string.IsNullOrEmpty(context.ClientId) || !Clients.ContainsKey(context.ClientId))
            {
                context.Reject(
                    error: OpenIddict.Abstractions.OpenIddictConstants.Errors.InvalidClient,
                    description: "未注册的客户端。");
                return default;
            }

            var client = Clients[context.ClientId];

            // 验证 redirect_uri（如果提供了）
            if (!string.IsNullOrEmpty(context.RedirectUri))
            {
                var isValidRedirectUri = client.RedirectUris.Any(uri =>
                    context.RedirectUri.StartsWith(uri, StringComparison.OrdinalIgnoreCase));

                if (!isValidRedirectUri)
                {
                    context.Reject(
                        error: OpenIddict.Abstractions.OpenIddictConstants.Errors.InvalidClient,
                        description: "无效的 redirect_uri。");
                    return default;
                }
            }

            return default;
        }

        private class ClientConfig
        {
            public string[] RedirectUris { get; set; } = Array.Empty<string>();
            public string[] AllowedScopes { get; set; } = Array.Empty<string>();
        }
    }

    /// <summary>
    /// 降级模式下的自定义 Token 请求验证 handler
    /// </summary>
    public class ValidateTokenRequestHandler : IOpenIddictServerHandler<ValidateTokenRequestContext>
    {
        public ValueTask HandleAsync(ValidateTokenRequestContext context)
        {
            // 在降级模式下，授权码流程的 token 请求不需要 client_secret（公开客户端）
            // 只需验证 client_id 是否已注册
            if (!string.IsNullOrEmpty(context.ClientId))
            {
                var knownClients = new[] { "OpenAuth.WebApi", "OpenAuth.Pro", "OpenAuth.Mvc" };
                if (!knownClients.Contains(context.ClientId, StringComparer.OrdinalIgnoreCase))
                {
                    context.Reject(
                        error: OpenIddict.Abstractions.OpenIddictConstants.Errors.InvalidClient,
                        description: "未注册的客户端。");
                    return default;
                }
            }

            return default;
        }
    }

    /// <summary>
    /// 降级模式下的自定义 Logout 请求验证 handler
    /// </summary>
    public class ValidateLogoutRequestHandler : IOpenIddictServerHandler<ValidateLogoutRequestContext>
    {
        public ValueTask HandleAsync(ValidateLogoutRequestContext context)
        {
            // 允许所有登出请求（可根据需要验证 post_logout_redirect_uri）
            return default;
        }
    }
}
