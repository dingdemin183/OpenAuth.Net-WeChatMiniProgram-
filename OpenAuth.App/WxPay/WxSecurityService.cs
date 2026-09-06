using Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAuth.App.SSO;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
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
        private readonly ILogger<WxSecurityService> _logger;
        private string _cachedAccessToken;
        private DateTime _tokenExpireTime;
        private readonly WxAccessTokenService _wxAccessTokenService;

        public WxSecurityService(
            IOptions<AppSetting> appConfiguration,
            IHttpClientFactory httpClientFactory,
            ILogger<WxSecurityService> logger,
            WxAccessTokenService wxAccessTokenService)
        {
            _appConfiguration = appConfiguration;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _wxAccessTokenService = wxAccessTokenService;
        }

        /// <summary>
        /// 检测文本内容是否违规（昵称、评论等）- 同步接口
        /// </summary>
        /// <param name="openId">用户openid（需近两小时访问过小程序）</param>
        /// <param name="content">要检测的文本内容</param>
        /// <param name="scene">场景：1-资料；2-评论；3-论坛；4-社交日志</param>
        /// <returns>true-安全，false-违规</returns>
        public async Task<bool> CheckTextSecurityAsync(string openId, string content, int scene = 1)
        {
            if (string.IsNullOrEmpty(content))
                return true;

            try
            {
                var accessToken = await _wxAccessTokenService.GetAccessTokenAsync();
                var url = $"https://api.weixin.qq.com/wxa/msg_sec_check?access_token={accessToken}";

                var requestBody = new
                {
                    openid = openId,
                    scene = scene,
                    version = 2,
                    content = content
                };

                var json = JsonSerializer.Serialize(requestBody);
                var contentData = new StringContent(json, Encoding.UTF8, "application/json");

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var response = await client.PostAsync(url, contentData);
                var resultJson = await response.Content.ReadAsStringAsync();

                _logger.LogDebug("文本检测响应: {Response}", resultJson);

                using var doc = JsonDocument.Parse(resultJson);
                var errCode = doc.RootElement.GetProperty("errcode").GetInt32();

                // 0-安全，87014-违规
                if (errCode == 0) return true;
                if (errCode == 87014) return false;

                // 其他错误码，记录日志并默认拒绝
                _logger.LogWarning("文本检测异常，错误码: {ErrCode}, 响应: {Response}", errCode, resultJson);
                throw new Exception($"文本检测失败，错误码: {errCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "文本安全检测异常，openId: {OpenId}, content: {Content}", openId, content);
                // 默认拒绝，保证安全
                throw new CommonException("内容安全检测服务异常，请稍后重试");
            }
        }

        /// <summary>
        /// 异步检测图片是否违规（头像、商品图等）
        /// </summary>
        /// <param name="openId">用户openid（需近两小时访问过小程序）</param>
        /// <param name="imageUrl">图片URL（需保证微信服务器可访问）</param>
        /// <param name="scene">场景：1-资料；2-评论；3-论坛；4-社交日志</param>
        /// <returns>trace_id（用于关联异步审核结果）</returns>
        public async Task<string> CheckImageSecurityAsync(string openId, string imageUrl, int scene = 1)
        {
            if (string.IsNullOrEmpty(imageUrl))
                throw new ArgumentException("图片URL不能为空", nameof(imageUrl));

            try
            {
                // var accessToken = await GetAccessTokenAsync();
                var accessToken = await _wxAccessTokenService.GetAccessTokenAsync();
                var url = $"https://api.weixin.qq.com/wxa/media_check_async?access_token={accessToken}";

                var requestBody = new
                {
                    openid = openId,
                    scene = scene,
                    version = 2,
                    media_url = imageUrl,
                    media_type = 2 // 2-图片
                };

                var json = JsonSerializer.Serialize(requestBody);
                var contentData = new StringContent(json, Encoding.UTF8, "application/json");

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(10);
                var response = await client.PostAsync(url, contentData);
                var resultJson = await response.Content.ReadAsStringAsync();

                _logger.LogDebug("图片检测请求响应: {Response}", resultJson);

                using var doc = JsonDocument.Parse(resultJson);
                var errCode = doc.RootElement.GetProperty("errcode").GetInt32();

                if (errCode == 0)
                {
                    var traceId = doc.RootElement.GetProperty("trace_id").GetString();
                    _logger.LogInformation("图片检测已提交，trace_id: {TraceId}, openId: {OpenId}, url: {Url}",
                        traceId, openId, imageUrl);
                    return traceId;
                }

                // 处理特定错误码
                if (errCode == 61010)
                {
                    throw new CommonException("用户未在近两小时内访问小程序，请重新登录后重试");
                }
                if (errCode == 44991)
                {
                    throw new CommonException("检测服务繁忙，请稍后重试");
                }
                if (errCode == 45009)
                {
                    throw new CommonException("今日检测次数已达上限，请明天再试");
                }

                _logger.LogWarning("图片检测提交失败，错误码: {ErrCode}, 响应: {Response}", errCode, resultJson);
                throw new Exception($"图片检测提交失败，错误码: {errCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "图片安全检测异常，openId: {OpenId}, imageUrl: {Url}", openId, imageUrl);
                throw;
            }
        }
        /// <summary>
        /// 同步检测图片是否违规（imgSecCheck，不需要openId）
        /// </summary>
        /// <param name="imageUrl">图片URL（微信服务器需能访问）</param>
        /// <returns>true-安全，false-违规</returns>
        public async Task<bool> CheckImageSecuritySyncAsync(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
                throw new ArgumentException("图片URL不能为空", nameof(imageUrl));

            try
            {
                var accessToken = await _wxAccessTokenService.GetAccessTokenAsync();
                var url = $"https://api.weixin.qq.com/wxa/img_sec_check?access_token={accessToken}";

                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                // 下载图片
                byte[] imageBytes;
                try
                {
                    imageBytes = await client.GetByteArrayAsync(imageUrl);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "下载图片失败，url: {Url}", imageUrl);
                    throw new CommonException("图片下载失败，请检查图片链接是否有效");
                }

                if (imageBytes.Length == 0)
                {
                    throw new CommonException("图片内容为空");
                }

                if (imageBytes.Length > 1024 * 1024)
                {
                    throw new CommonException("图片大小超过1MB限制，请压缩后重试");
                }

                // 手动构造 multipart 请求体
                var boundary = "----WebKitFormBoundary" + DateTime.Now.Ticks.ToString("x");
                var ext = Path.GetExtension(imageUrl).ToLowerInvariant();
                var mimeType = ext switch
                {
                    ".png" => "image/png",
                    ".gif" => "image/gif",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    _ => "image/jpeg"
                };

                // 构建 multipart 数据
                using var stream = new MemoryStream();
                var writer = new StreamWriter(stream, Encoding.UTF8);

                // 写入边界和头部
                await writer.WriteAsync($"--{boundary}\r\n");
                await writer.WriteAsync($"Content-Disposition: form-data; name=\"media\"; filename=\"image.jpg\"\r\n");
                await writer.WriteAsync($"Content-Type: {mimeType}\r\n");
                await writer.WriteAsync("\r\n");
                await writer.FlushAsync();

                // 写入图片数据
                await stream.WriteAsync(imageBytes, 0, imageBytes.Length);
                await stream.FlushAsync();

                // 写入结束边界
                await writer.WriteAsync($"\r\n--{boundary}--\r\n");
                await writer.FlushAsync();

                stream.Seek(0, SeekOrigin.Begin);
                var content = new ByteArrayContent(stream.ToArray());
                content.Headers.ContentType = new MediaTypeHeaderValue("multipart/form-data")
                {
                    Parameters = { new NameValueHeaderValue("boundary", boundary) }
                };

                var response = await client.PostAsync(url, content);
                var resultJson = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("图片同步检测响应: {Response}", resultJson);

                using var doc = JsonDocument.Parse(resultJson);
                var errCode = doc.RootElement.GetProperty("errcode").GetInt32();

                if (errCode == 0) return true;
                if (errCode == 87014) return false;

                _logger.LogWarning("图片同步检测异常，错误码: {ErrCode}, 响应: {Response}", errCode, resultJson);
                throw new Exception($"图片检测失败，错误码: {errCode}");
            }
            catch (Exception ex) when (ex is not CommonException)
            {
                _logger.LogError(ex, "图片同步安全检测异常，imageUrl: {Url}", imageUrl);
                throw new CommonException("图片安全检测服务异常，请稍后重试");
            }
        }

    }
}