using Infrastructure;
using log4net.Core;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAuth.App.SSO;
using System;
using System.IO;
using System.Threading.Tasks;

namespace OpenAuth.App
{
    public class FileUploadApp
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<FileUploadApp> _logger;
        private readonly IConfiguration _configuration;

        public FileUploadApp(IWebHostEnvironment env, ILogger<FileUploadApp> logger, IConfiguration configuration)
        {
            _env = env;
            _logger = logger;
            _configuration = configuration;
        }

        ///// <summary>
        ///// 上传产品主图
        ///// </summary>
        ///// <param name="file"></param>
        ///// <returns></returns>
        ///// <exception cref="CommonException"></exception>
        //public async Task<string> UploadProductImage(IFormFile file)
        //{
        //    // 校验：文件是否为空
        //    if (file == null || file.Length == 0)
        //    {
        //        throw new CommonException("请选择要上传的图片");
        //    }

        //    // 校验文件大小（限制 1MB）
        //    var maxSize = 2 * 1024 * 1024; // 2MB
        //    if (file.Length > maxSize)
        //    {
        //        throw new CommonException($"图片大小不能超过 {maxSize / 1024 / 1024}MB,请处理后上传");
        //    }

        //    // 校验：文件格式
        //    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
        //    var extension = Path.GetExtension(file.FileName).ToLower();
        //    if (!Array.Exists(allowedExtensions, ext => ext == extension))
        //    {
        //        throw new CommonException("只允许上传图片格式（jpg, jpeg, png, gif, webp, bmp）");
        //    }

        //    // 生成文件名（GUID + 扩展名）
        //    var fileName = $"{Guid.NewGuid()}{extension}";

        //    // 按年/月/日 分目录存储
        //    var datePath = DateTime.Now.ToString("yyyy/MM/dd");
        //    var relativePath = Path.Combine("uploads/image", datePath);

        //    // 获取 WebRootPath，如果为 null 则使用当前目录的 wwwroot
        //    var webRootPath = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
        //    var absolutePath = Path.Combine(webRootPath, relativePath);

        //    // 确保目录存在
        //    if (!Directory.Exists(absolutePath))
        //    {
        //        Directory.CreateDirectory(absolutePath);
        //    }

        //    // 保存文件
        //    var fullPath = Path.Combine(absolutePath, fileName);
        //    using (var stream = new FileStream(fullPath, FileMode.Create))
        //    {
        //        await file.CopyToAsync(stream);
        //    }

        //    // 返回访问 URL
        //    return $"/{relativePath.Replace("\\", "/")}/{fileName}";
        //}
        public async Task<string> UploadProductImage(IFormFile file)
        {
            // 校验：文件是否为空
            if (file == null || file.Length == 0)
            {
                throw new CommonException("请选择要上传的图片");
            }

            // 校验文件大小（限制 2MB）
            var maxSize = 2 * 1024 * 1024; // 2MB
            if (file.Length > maxSize)
            {
                throw new CommonException($"图片大小不能超过 {maxSize / 1024 / 1024}MB,请处理后上传");
            }

            // 校验：文件格式
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (!Array.Exists(allowedExtensions, ext => ext == extension))
            {
                throw new CommonException("只允许上传图片格式（jpg, jpeg, png, gif, webp, bmp）");
            }

            // 生成文件名（GUID + 扩展名）
            var fileName = $"{Guid.NewGuid()}{extension}";

            var datePath = DateTime.Now.ToString("yyyy/MM/dd");
            var relativePath = Path.Combine("uploads/image", datePath);

            // 获取 ContentRootPath（程序运行目录）
            var contentRootPath = _env.ContentRootPath; // C:\publish\Deshuai

            // 构建 wwwroot 路径
            var webRootPath = Path.Combine(contentRootPath, "wwwroot");

            // ===== 关键：如果 wwwroot 不存在，自动创建 =====
            if (!Directory.Exists(webRootPath))
            {
                Directory.CreateDirectory(webRootPath);
                _logger.LogInformation("已创建 wwwroot 目录: {WebRootPath}", webRootPath);
            }

            var absolutePath = Path.Combine(webRootPath, relativePath);

            // 确保子目录存在
            if (!Directory.Exists(absolutePath))
            {
                Directory.CreateDirectory(absolutePath);
            }

            var fullPath = Path.Combine(absolutePath, fileName);

            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // ===== 修改这里：返回完整 URL =====
            // 从配置读取基础 URL
            var baseUrl = _configuration["AppSettings:BaseUrl"];

            // 如果没有配置，使用默认值（开发环境）
            if (string.IsNullOrEmpty(baseUrl))
            {
                baseUrl = "http://192.168.10.99:8099"; // 你的 IIS 地址
            }

            // 返回完整 URL
            return $"{baseUrl}/{relativePath.Replace("\\", "/")}/{fileName}";
        }
    }
}