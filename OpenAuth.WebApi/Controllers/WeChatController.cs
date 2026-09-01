// OpenAuth.WebApi/Controllers/WeChatController.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenAuth.App.Interface;
using OpenAuth.App.SSO;
using System;
using System.Threading.Tasks;

namespace OpenAuth.WebApi.Controllers
{
    [Route("api/[controller]/[action]")]
    [ApiController]
    [ApiExplorerSettings(GroupName = "微信小程序登录及手机号获取_WeChat")]
    public class WeChatController : ControllerBase
    {
        private readonly LoginParse _loginParse;
        private readonly WxMiniProgramService _wxService;
        private readonly IAuth _auth;


        public WeChatController(LoginParse loginParse, WxMiniProgramService wxService, IAuth auth)
        {
            _loginParse = loginParse;
            _wxService = wxService;
            _auth = auth;
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<LoginResult> MiniProgramLogin([FromBody] WxMiniProgramLoginRequest request)
        {
            var result = new LoginResult();
            try
            {
                result = await _loginParse.WxMiniProgramLoginAsync(request);
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
            }
            return result;
        }

        /// <summary>
        /// 获取用户手机号
        /// </summary>
        /// <param name="request">请求参数</param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> GetPhoneNumber([FromBody] WxPhoneRequest request)
        {
            try
            {
                //  调用微信接口获取手机号
                var wxResult = await _wxService.GetUserPhoneAsync(request.Code);

                if (wxResult.ErrCode != 0)
                {
                    return Ok(new
                    {
                        code = wxResult.ErrCode,
                        message = wxResult.ErrMsg
                    });
                }

                var phoneNumber = wxResult.PhoneInfo?.PurePhoneNumber ?? wxResult.PhoneInfo?.PhoneNumber;

                if (string.IsNullOrEmpty(phoneNumber))
                {
                    return Ok(new
                    {
                        code = 500,
                        message = "获取手机号失败"
                    });
                }

                // 获取当前登录用户的 OpenId
                var session = _auth.GetCurrentSession();

                if (string.IsNullOrEmpty(session.Account))
                {
                    return Ok(new
                    {
                        code = 401,
                        message = "请先登录"
                    });
                }

                // 更新用户手机号
                var result = await _loginParse.UpdateUserPhoneAsync(session.Account, phoneNumber);

                return Ok(new
                {
                    code = 0,
                    message = "手机号更新成功",
                    data = new
                    {
                        phoneNumber = phoneNumber
                    }
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    code = 500,
                    message = ex.Message
                });
            }
        }
    }
}