// OpenAuth.WebApi/Controllers/WeChatController.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenAuth.App.Interface;
using OpenAuth.App.SSO;
using System;
using System.Linq;
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

        /// <summary>
        /// 微信小程序一键登录（登录 + 获取手机号）
        /// </summary>
        /// <param name="request">请求参数，包含登录code和手机号code</param>
        /// <returns></returns>
        [HttpPost]
        [AllowAnonymous]
        public async Task<LoginResult> MiniProgramLoginWithPhone([FromBody] WxMiniProgramLoginWithPhoneRequest request)
        {
            var result = new LoginResult();
            try
            {
                var userIp = GetUserIp();
                result = await _loginParse.WxMiniProgramLoginWithPhoneAsync(request,userIp);
            }
            catch (Exception ex)
            {
                result.Code = 500;
                result.Message = ex.Message;
            }
            return result;
        }


        /// <summary>
        /// 获取用户最后登录的ip
        /// </summary>
        /// <returns></returns>
        private string GetUserIp()
        {
            var ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (string.IsNullOrEmpty(ip))
            {
                ip = HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault();
            }
            if (string.IsNullOrEmpty(ip))
            {
                ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            }

            // 如果是 IPv6 的本地地址，转换成 IPv4
            if (ip == "::1" || ip == "127.0.0.1")
            {
                ip = "127.0.0.1";
            }

            // 如果有多层代理，取第一个真实IP
            if (!string.IsNullOrEmpty(ip) && ip.Contains(','))
            {
                ip = ip.Split(',')[0].Trim();
            }

            return ip ?? "127.0.0.1";
        }
    }
}