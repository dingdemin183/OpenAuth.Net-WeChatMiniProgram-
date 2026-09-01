// OpenAuth.App/SSO/WxAccessTokenService.cs

using Infrastructure;
using Infrastructure.Cache;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenAuth.App.SSO
{
    public class WxAccessTokenService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<AppSetting> _appConfiguration;
        private readonly ICacheContext _cacheContext;
        private readonly ILogger<WxAccessTokenService> _logger;

        private const string CACHE_KEY = "wechat_access_token";

        public WxAccessTokenService(
            IHttpClientFactory httpClientFactory,
            IOptions<AppSetting> appConfiguration,
            ICacheContext cacheContext,
            ILogger<WxAccessTokenService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _appConfiguration = appConfiguration;
            _cacheContext = cacheContext;
            _logger = logger;
        }

        /// <summary>
        /// 获取微信 AccessToken（自动缓存，2小时有效期）
        /// </summary>
        public async Task<string> GetAccessTokenAsync()
        {
            // 先尝试从缓存获取
            var cached = _cacheContext.Get<string>(CACHE_KEY);
            if (!string.IsNullOrEmpty(cached))
            {
                return cached;
            }

            // 缓存没有，调用微信接口获取
            var appId = _appConfiguration.Value.WeChatMiniProgram.AppId;
            var appSecret = _appConfiguration.Value.WeChatMiniProgram.AppSecret;

            var url = $"https://api.weixin.qq.com/cgi-bin/token" +
                      $"?grant_type=client_credential" +
                      $"&appid={appId}" +
                      $"&secret={appSecret}";

            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            _logger.LogWarning($" AccessToken 接口返回: {json}");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("errcode", out var errCode))
            {
                var msg = root.GetProperty("errmsg").GetString();
                throw new Exception($"获取 AccessToken 失败: {errCode.GetInt32()} - {msg}");
            }

            var accessToken = root.GetProperty("access_token").GetString();
            var expiresIn = root.GetProperty("expires_in").GetInt32(); // 默认 7200 秒

            //  存入缓存（提前5分钟过期，防止边界情况）
            var expireSeconds = expiresIn - 300;
            _cacheContext.Set(CACHE_KEY, accessToken, DateTime.Now.AddSeconds(expireSeconds));

            return accessToken;
        }
    }
}