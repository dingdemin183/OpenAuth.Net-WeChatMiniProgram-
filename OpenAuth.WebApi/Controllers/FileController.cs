using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenAuth.App;
using OpenAuth.App.Response;
using System;
using System.Threading.Tasks;

namespace OpenAuth.WebApi.Controllers
{
    /// <summary>
    /// 文件上传接口
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "文件上传_产品图片上传")]
    public class FileController : ControllerBase
    {
        private readonly FileUploadApp _fileUploadApp;

        public FileController(FileUploadApp fileUploadApp)
        {
            _fileUploadApp = fileUploadApp;
        }

        /// <summary>
        /// 上传产品主图
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<Response<UploadFileResp>> UploadProductImage([FromForm] IFormFile file)
        {
            var result = new Response<UploadFileResp>();
            try
            {
                var url = await _fileUploadApp.UploadProductImage(file);
                result.Data = new UploadFileResp
                {
                    Url = url,
                    FileName = file?.FileName,
                    FileSize = file?.Length ?? 0
                };
                result.Message = "上传成功";
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.InnerException?.Message ?? ex.Message;
            }
            return result;
        }
    }

    /// <summary>
    /// 上传文件响应
    /// </summary>
    public class UploadFileResp
    {
        /// <summary>
        /// 文件地址
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// 文件名称
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 文件大小
        /// </summary>
        public long FileSize { get; set; }
    }
}