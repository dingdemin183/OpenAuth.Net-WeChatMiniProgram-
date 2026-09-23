using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Middleware
{
    /// <summary>
    /// 请求与返回中间件
    /// </summary>
    public class RequestResponseLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestResponseLoggingMiddleware> _log;

        // 日志中 request.body / response.body 的最大长度，超过则截断
        private const int MaxBodyLogLength = 2000;

        // 不记录 body 的内容类型（二进制/媒体类）
        private static readonly string[] SkipBodyContentTypes =
        {
            "image/", "video/", "audio/", "application/octet-stream",
            "application/pdf", "application/zip", "application/x-zip",
            "application/x-msdownload", "font/", "application/font"
        };

        // 不记录日志的路径关键字（静态资源等）
        private static readonly string[] SkipPathKeywords =
        {
            "/index", "/check", "/swagger", "/getsysdatas", "/load",
            "/uploads", "/profiler", "/favicon"
        };

        public RequestResponseLoggingMiddleware(RequestDelegate next, ILogger<RequestResponseLoggingMiddleware> log)
        {
            _next = next;
            _log = log;
        }

        public async Task Invoke(HttpContext context)
        {
            var path = context.Request.Path.ToString().ToLower();

            // 1. 静态资源 / 指定路径，直接跳过，不记录
            if (SkipPathKeywords.Any(k => path.Contains(k)))
            {
                await CatchNext(context);
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            var logData = new Dictionary<string, object>();
            var request = context.Request;

            logData["request.url"] = request.Path.ToString();
            logData["request.headers"] = request.Headers.ToDictionary(x => x.Key, v => string.Join(";", v.Value.ToList()));
            logData["request.method"] = request.Method;
            logData["request.executeStartTime"] = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            logData["traceIdentifier"] = context.TraceIdentifier;

            // 2. 记录请求 body
            try
            {
                if (HttpMethods.IsPost(request.Method))
                {
                    request.EnableBuffering();

                    var contentType = request.ContentType ?? "";
                    if (path.Contains("/upload") || contentType.Contains("multipart/form-data"))
                    {
                        // 文件上传只记文件名，不记内容
                        try
                        {
                            var files = string.Join(",", request.Form.Files.Select(f => f.FileName));
                            logData["request.body"] = $"收到上传文件:{files}";
                        }
                        catch
                        {
                            logData["request.body"] = "[文件信息解析失败]";
                        }
                    }
                    else
                    {
                        request.Body.Position = 0;
                        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
                        var content = await reader.ReadToEndAsync();
                        logData["request.body"] = Truncate(content, MaxBodyLogLength);
                        request.Body.Position = 0;
                    }
                }
                else if (HttpMethods.IsGet(request.Method))
                {
                    logData["request.body"] = Truncate(request.QueryString.Value ?? "", MaxBodyLogLength);
                }
            }
            catch (Exception ex)
            {
                logData["request.body"] = "[读取请求体失败] " + ex.Message;
            }

            // 3. 捕获响应，仅在需要时记录 body
            var originalBodyStream = context.Response.Body;
            using var responseBody = new MemoryStream();

            try
            {
                context.Response.Body = responseBody;
                await CatchNext(context);

                // 判断响应类型，决定是否记录 body
                var responseContentType = context.Response.ContentType ?? "";
                bool isBinaryResponse = SkipBodyContentTypes.Any(t =>
                    responseContentType.StartsWith(t, StringComparison.OrdinalIgnoreCase));

                if (!logData.ContainsKey("response.body"))
                {
                    if (isBinaryResponse)
                    {
                        // 二进制响应，只记大小和类型，绝不记内容
                        logData["response.body"] = $"[二进制响应已省略] contentType={responseContentType}, size={responseBody.Length} bytes";
                    }
                    else
                    {
                        var bodyText = await GetResponseBodyAsync(context.Response);
                        logData["response.body"] = Truncate(bodyText, MaxBodyLogLength);
                    }
                }

                logData["response.executeEndTime"] = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                logData["response.statusCode"] = context.Response.StatusCode;

                await responseBody.CopyToAsync(originalBodyStream);
            }
            finally
            {
                context.Response.Body = originalBodyStream;
            }

            // 4. 完成时记录
            stopwatch.Stop();
            logData["elapsedTime"] = stopwatch.ElapsedMilliseconds + "ms";
            try
            {
                var json = JsonHelper.Instance.Serialize(logData);
                _log.LogInformation(json);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "写请求日志失败");
            }
        }

        /// <summary>
        /// 截断字符串，避免日志过大
        /// </summary>
        private static string Truncate(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input)) return input;
            return input.Length <= maxLength ? input : input.Substring(0, maxLength) + $"...[truncated, total {input.Length}]";
        }

        private async Task CatchNext(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "系统错误日志,管道捕获");
                // 只在响应还未开始时才能写，否则会破坏响应
                if (!context.Response.HasStarted)
                {
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/json; charset=utf-8";
                    var result = new { code = 500, message = ex.Message ?? "系统错误,请稍后再试" };
                    await context.Response.WriteAsync(JsonHelper.Instance.Serialize(result));
                }
            }
        }

        private static async Task<string> GetResponseBodyAsync(HttpResponse response)
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
            var text = await reader.ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);
            return text;
        }
    }
}