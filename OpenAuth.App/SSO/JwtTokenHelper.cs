using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OpenAuth.App.SSO
{
    /// <summary>
    /// JWT Token辅助类，用于本地认证模式下的Token生成和验证
    /// </summary>
    public static class JwtTokenHelper
    {
        /// <summary>
        /// 生成JWT Token
        /// </summary>
        /// <param name="account">用户账号</param>
        /// <param name="name">用户名称</param>
        /// <param name="appKey">应用标识</param>
        /// <param name="sessionId">会话ID，用作缓存Key</param>
        /// <param name="secret">JWT签名密钥</param>
        /// <param name="expireDays">过期天数</param>
        /// <returns>JWT Token字符串</returns>
        public static string GenerateToken(string account, string name, string appKey, string sessionId, string secret, int expireDays = 10)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(EnsureKeyLength(secret)));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, account),
                new Claim(JwtRegisteredClaimNames.Jti, sessionId),
                new Claim("name", name ?? string.Empty),
                new Claim("app_key", appKey ?? string.Empty),
            };

            var token = new JwtSecurityToken(
                issuer: "OpenAuth",
                audience: "OpenAuth",
                claims: claims,
                expires: DateTime.UtcNow.AddDays(expireDays),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// 验证JWT Token并返回ClaimsPrincipal
        /// </summary>
        /// <param name="token">JWT Token字符串</param>
        /// <param name="secret">JWT签名密钥</param>
        /// <returns>验证成功返回ClaimsPrincipal，失败返回null</returns>
        public static ClaimsPrincipal ValidateToken(string token, string secret)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(EnsureKeyLength(secret)));

            try
            {
                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = key,
                    ValidateIssuer = true,
                    ValidIssuer = "OpenAuth",
                    ValidateAudience = true,
                    ValidAudience = "OpenAuth",
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5)
                };

                var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                return principal;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 从JWT Token中提取会话ID（jti）
        /// </summary>
        public static string GetSessionId(string token)
        {
            var principal = ReadTokenWithoutValidation(token);
            return principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
        }

        /// <summary>
        /// 从JWT Token中提取用户账号（sub）
        /// </summary>
        public static string GetAccount(string token)
        {
            var principal = ReadTokenWithoutValidation(token);
            return principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                ?? principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        }

        /// <summary>
        /// 不验证签名，仅读取Token中的Claims（用于快速提取信息）
        /// </summary>
        private static ClaimsPrincipal ReadTokenWithoutValidation(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                if (!tokenHandler.CanReadToken(token))
                    return null;

                var jwtToken = tokenHandler.ReadJwtToken(token);
                var identity = new ClaimsIdentity(jwtToken.Claims);
                return new ClaimsPrincipal(identity);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 确保密钥长度至少为32字节（256位），不足时进行填充
        /// </summary>
        private static string EnsureKeyLength(string secret)
        {
            if (string.IsNullOrEmpty(secret))
                secret = "openauth_default_jwt_secret_key_2024";

            // HMAC-SHA256要求密钥至少128位(16字节)，建议256位(32字节)
            while (secret.Length < 32)
            {
                secret += secret;
            }
            return secret.Substring(0, Math.Max(32, secret.Length));
        }
    }
}
