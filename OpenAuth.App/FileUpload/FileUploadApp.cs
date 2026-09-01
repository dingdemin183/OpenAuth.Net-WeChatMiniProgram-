using System;
using System.IO;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace OpenAuth.App
{
    public class FileUploadApp
    {
        private readonly IWebHostEnvironment _env;

        public FileUploadApp(IWebHostEnvironment env)
        {
            _env = env;
        }

        /// <summary>
        /// 上传产品主图
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        /// <exception cref="CommonException"></exception>
        public async Task<string> UploadProductImage(IFormFile file)
        {
            // 校验：文件是否为空
            if (file == null || file.Length == 0)
            {
                throw new CommonException("请选择要上传的图片");
            }

            // 校验文件大小（限制 1MB）
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

            // 按年/月/日 分目录存储
            var datePath = DateTime.Now.ToString("yyyy/MM/dd");
            var relativePath = Path.Combine("uploads/image", datePath);

            // 获取 WebRootPath，如果为 null 则使用当前目录的 wwwroot
            var webRootPath = _env.WebRootPath ?? Path.Combine(AppContext.BaseDirectory, "wwwroot");
            var absolutePath = Path.Combine(webRootPath, relativePath);

            // 确保目录存在
            if (!Directory.Exists(absolutePath))
            {
                Directory.CreateDirectory(absolutePath);
            }

            // 保存文件
            var fullPath = Path.Combine(absolutePath, fileName);
            using (var stream = new FileStream(fullPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // 返回访问 URL
            return $"/{relativePath.Replace("\\", "/")}/{fileName}";
        }
    }
}