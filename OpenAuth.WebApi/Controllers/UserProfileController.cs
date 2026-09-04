using Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OpenAuth.App;
using OpenAuth.App.Request;
using OpenAuth.App.Response;
using OpenAuth.App.UserProfile;
using System;
using System.Threading.Tasks;

namespace OpenAuth.WebApi.Controllers
{
    /// <summary>
    /// 用户个人资料管理
    /// </summary>
    [Route("api/[controller]/[action]")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "用户资料_UserProfile")]
    public class UserProfileController : ControllerBase
    {
        private readonly UserProfileApp _userProfileApp;
        private readonly FileUploadApp _fileUploadApp;

        public UserProfileController(
            UserProfileApp userProfileApp,
            FileUploadApp fileUploadApp)
        {
            _userProfileApp = userProfileApp;
            _fileUploadApp = fileUploadApp;
        }

        /// <summary>
        /// 获取当前用户资料(小程序端 个人信息）
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<Response<UserProfileResp>> GetMyProfile()
        {
            var result = new Response<UserProfileResp>();
            try
            {
                var data = await _userProfileApp.GetMyProfileAsync();
                result.Data = data;
                result.Message = "获取成功";
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// 更新用户个人资料（头像+昵称）
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        [HttpPost]
        public async Task<Response<UserProfileResp>> UpdateProfile([FromBody] UpdateUserProfileReq request)
        {
            var result = new Response<UserProfileResp>();
            try
            {
                var data = await _userProfileApp.UpdateProfileAsync(request);
                result.Data = data;
                result.Message = "更新成功";
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
            }
            return result;
        }
    }
}