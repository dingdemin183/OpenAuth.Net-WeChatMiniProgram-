// OpenAuth.App/SSO/WxMiniProgramService.cs

using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OpenAuth.App.SSO
{
    public class WxMiniProgramService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<AppSetting> _appConfiguration;
        private readonly ILogger<WxMiniProgramService> _logger;
        private readonly WxAccessTokenService _accessTokenService;

        public WxMiniProgramService(
            IHttpClientFactory httpClientFactory,
            IOptions<AppSetting> appConfiguration,
            ILogger<WxMiniProgramService> logger, 
            WxAccessTokenService accessTokenService)
        {
            _httpClientFactory = httpClientFactory;
            _appConfiguration = appConfiguration;
            _logger = logger;
            _accessTokenService = accessTokenService;
        }

        /// <summary>
        /// 用 code 换取 openid 和 session_key
        /// </summary>
        public async Task<WxJscode2SessionResponse> GetOpenIdAndSessionKeyAsync(string code)
        {
            var appId = _appConfiguration.Value.WeChatMiniProgram.AppId;
            var appSecret = _appConfiguration.Value.WeChatMiniProgram.AppSecret;

            if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(appSecret))
            {
                throw new Exception("微信小程序配置缺失，请检查 AppId 和 AppSecret");
            }

            var url = $"https://api.weixin.qq.com/sns/jscode2session" +
                      $"?appid={appId}" +
                      $"&secret={appSecret}" +
                      $"&js_code={code}" +
                      $"&grant_type=authorization_code";

            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            _logger.LogDebug($"微信接口返回: {json}");

            var result = JsonSerializer.Deserialize<WxJscode2SessionResponse>(json);

            if (result.ErrCode != 0 && result.ErrCode != default)
            {
                _logger.LogWarning($"微信接口返回: {json}");
            }

            return result;
        }

        /// <summary>
        /// 获取用户手机号
        /// </summary>
        public async Task<WxPhoneResponse> GetUserPhoneAsync(string code)
        {
            // 1. 获取 access_token
            var accessToken = await _accessTokenService.GetAccessTokenAsync();

            // 2. 调用微信手机号接口
            var url = $"https://api.weixin.qq.com/wxa/business/getuserphonenumber?access_token={accessToken}";

            var requestBody = new { code = code };
            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(requestBody),
                System.Text.Encoding.UTF8,
                "application/json");

            var client = _httpClientFactory.CreateClient();
            var response = await client.PostAsync(url, content);
            var json = await response.Content.ReadAsStringAsync();

            _logger.LogWarning($"获取手机号接口返回: {json}");

            var result = System.Text.Json.JsonSerializer.Deserialize<WxPhoneResponse>(json);

            if (result.ErrCode != 0)
            {
                _logger.LogWarning($" 获取手机号失败: {result.ErrCode} - {result.ErrMsg}");
            }

            return result;
        }
    }
}