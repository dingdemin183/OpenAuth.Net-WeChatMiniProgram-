using System;
using System.Text.Json;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace OpenAuth.WebApi.Middleware
{
    /// <summary>
    /// 全局异常处理中间件
    /// 捕获所有未被 Controller 和 Filter 处理的异常
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // 执行下一个中间件（最终会到达 Controller）
                await _next(context);
            }
            catch (Exception ex)
            {
                // ========== 在这里统一处理所有未捕获的异常 ==========

                var code = 500;
                var message = "服务器内部错误，请稍后重试";

                // 如果是我们自定义的业务异常，使用指定的错误码和消息
                if (ex is CommonException commonEx)
                {
                    code = commonEx.Code;
                    message = commonEx.Message;
                }
                else
                {
                    // 记录系统异常日志（方便排查问题）
                    _logger.LogError(ex, "全局异常捕获 - {Path}", context.Request.Path);
                }

                // 返回统一的 JSON 格式响应
                context.Response.StatusCode = 200; // 业务状态码通过 JSON 返回
                context.Response.ContentType = "application/json";

                var response = new Response
                {
                    Code = code,
                    Message = message
                };

                var json = JsonSerializer.Serialize(response);
                await context.Response.WriteAsync(json);
            }
        }
    }
}