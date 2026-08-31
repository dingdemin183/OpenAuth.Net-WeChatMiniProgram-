// OpenAuth.WebApi/Controllers/WeChatController.cs

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenAuth.App.SSO;
using System.Threading.Tasks;
using System;

namespace OpenAuth.WebApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [AllowAnonymous]
    public class WeChatController : ControllerBase
    {
        private readonly LoginParse _loginParse;

        public WeChatController(LoginParse loginParse)
        {
            _loginParse = loginParse;
        }

        [HttpPost("MiniProgramLogin")]
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
    }
}