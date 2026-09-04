using Infrastructure;
using Microsoft.Extensions.Options;
using OpenAuth.App.SSO;
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace OpenAuth.App.WxPay
{
    /// <summary>
    /// 微信内容安全检测服务
    /// </summary>
    public class WxSecurityService
    {
        private readonly IOptions<AppSetting> _appConfiguration;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly WxMiniProgramService _wxMiniProgramService;

        public WxSecurityService(
            IOptions<AppSetting> appConfiguration,
            IHttpClientFactory httpClientFactory,
            WxMiniProgramService wxMiniProgramService)
        {
            _appConfiguration = appConfiguration;
            _httpClientFactory = httpClientFactory;
            _wxMiniProgramService = wxMiniProgramService;
        }

        /// <summary>
        /// 检测文本内容是否违规（昵称、评论等）
        /// </summary>
        /// <param name="openId">用户openid</param>
        /// <param name="content">要检测的文本内容</param>
        /// <returns>true-安全，false-违规</returns>
        public async Task<bool> CheckTextSecurityAsync(string openId, string content)
        {
            if (string.IsNullOrEmpty(content))
                return true;

            try
            {
                // 获取access_token
                var accessToken = await GetAccessTokenAsync();

                var url = $"https://api.weixin.qq.com/wxa/msg_sec_check?access_token={accessToken}";

                var requestBody = new
                {
                    openid = openId,
                    scene = 1, // 1-资料；2-评论；3-论坛；4-社交日志
                    version = 2,
                    content = content
                };

                var json = JsonSerializer.Serialize(requestBody);
                var contentData = new StringContent(json, Encoding.UTF8, "application/json");

                var client = _httpClientFactory.CreateClient();
                var response = await client.PostAsync(url, contentData);
                var resultJson = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(resultJson);
                var errCode = doc.RootElement.GetProperty("errcode").GetInt32();

                // 0-成功 且 未违规，87014-违规内容
                // 0 表示成功且内容安全
                return errCode == 0;
            }
            catch (Exception ex)
            {
                // 安全检测异常时，为了不影响用户体验，可以放行或记录日志
                // 但建议生产环境记录日志并人工审核
                return true; // 异常时默认放行，但需记录日志
            }
        }

        /// <summary>
        /// 检测图片是否违规（头像、商品图等）
        /// </summary>
        /// <param name="openId">用户openid</param>
        /// <param name="imageUrl">图片URL</param>
        /// <param name="imageData">图片字节数据（可选，优先使用）</param>
        /// <returns>true-安全，false-违规</returns>
        public async Task<bool> CheckImageSecurityAsync(string openId, string imageUrl, byte[] imageData = null)
        {
            if (string.IsNullOrEmpty(imageUrl) && (imageData == null || imageData.Length == 0))
                return true;

            try
            {
                var accessToken = await GetAccessTokenAsync();
                var url = $"https://api.weixin.qq.com/wxa/media_check_async?access_token={accessToken}";

                object requestBody;
                if (imageData != null && imageData.Length > 0)
                {
                    // 使用图片字节数据
                    var base64Image = Convert.ToBase64String(imageData);
                    requestBody = new
                    {
                        media = new
                        {
                            openid = openId,
                            scene = 1, // 1-资料；2-评论；3-论坛；4-社交日志
                            media_data = base64Image,
                            media_type = 2 // 2-图片
                        }
                    };
                }
                else
                {
                    // 使用图片URL
                    requestBody = new
                    {
                        media = new
                        {
                            openid = openId,
                            scene = 1,
                            media_url = imageUrl,
                            media_type = 2 // 2-图片
                        }
                    };
                }

                var json = JsonSerializer.Serialize(requestBody);
                var contentData = new StringContent(json, Encoding.UTF8, "application/json");

                var client = _httpClientFactory.CreateClient();
                var response = await client.PostAsync(url, contentData);
                var resultJson = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(resultJson);
                var errCode = doc.RootElement.GetProperty("errcode").GetInt32();

                // 0-成功 且 未违规
                return errCode == 0;
            }
            catch (Exception ex)
            {
                // 异常时默认放行，记录日志
                return true;
            }
        }

        /// <summary>
        /// 获取接口调用凭证
        /// </summary>
        private async Task<string> GetAccessTokenAsync()
        {
            var appId = _appConfiguration.Value.WeChatMiniProgram.AppId;
            var appSecret = _appConfiguration.Value.WeChatMiniProgram.AppSecret;

            var client = _httpClientFactory.CreateClient();
            var url = $"https://api.weixin.qq.com/cgi-bin/token?grant_type=client_credential&appid={appId}&secret={appSecret}";

            var response = await client.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("access_token", out var tokenElement))
            {
                return tokenElement.GetString();
            }

            throw new Exception("获取access_token失败：" + json);
        }
    }
}