using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenAuth.App;
using OpenAuth.Repository.Domain;

namespace OpenAuth.IdentityServer.Quickstart.Account
{
    /// <summary>
    /// 处理用户登录/登出流程
    /// </summary>
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly UserManagerApp _userManager;

        public AccountController(UserManagerApp userManager)
        {
            _userManager = userManager;
        }

        /// <summary>
        /// 显示登录页面
        /// </summary>
        [HttpGet]
        public IActionResult Login(string returnUrl)
        {
            var vm = new LoginViewModel
            {
                ReturnUrl = returnUrl,
                EnableLocalLogin = true,
                AllowRememberLogin = AccountOptions.AllowRememberLogin
            };
            return View(vm);
        }

        /// <summary>
        /// 处理登录表单提交
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginInputModel model, string button)
        {
            if (button != "login")
            {
                // 用户取消登录
                if (!string.IsNullOrEmpty(model.ReturnUrl))
                {
                    return Redirect(model.ReturnUrl);
                }
                return Redirect("~/");
            }

            if (ModelState.IsValid)
            {
                SysUser sysUser;
                if (model.Username == Define.SYSTEM_USERNAME && model.Password == Define.SYSTEM_USERPWD)
                {
                    sysUser = new SysUser
                    {
                        Account = Define.SYSTEM_USERNAME,
                        Password = Define.SYSTEM_USERPWD,
                        Id = Define.SYSTEM_USERNAME,
                        Name = Define.SYSTEM_USERNAME,
                        Status = 0
                    };
                }
                else
                {
                    sysUser = _userManager.GetByAccount(model.Username);
                }

                if (sysUser != null && sysUser.Password == model.Password)
                {
                    if (sysUser.Status != 0)
                    {
                        ModelState.AddModelError(string.Empty, "用户状态异常，无法登录");
                        return View(BuildLoginViewModel(model));
                    }

                    // 构建 Cookie Claims
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, sysUser.Id),
                        new Claim(ClaimTypes.Name, sysUser.Account),
                    };

                    var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var principal = new ClaimsPrincipal(identity);

                    var props = new AuthenticationProperties
                    {
                        IsPersistent = model.RememberLogin,
                    };

                    // 写入 Cookie
                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        principal,
                        props);

                    // 登录成功后重定向
                    if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                    {
                        return Redirect(model.ReturnUrl);
                    }

                    return Redirect("~/");
                }

                ModelState.AddModelError(string.Empty, AccountOptions.InvalidCredentialsErrorMessage);
            }

            // 登录失败，重新显示表单
            return View(BuildLoginViewModel(model));
        }

        /// <summary>
        /// 显示登出页面
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Logout(string logoutId)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return View("LoggedOut", new LoggedOutViewModel
            {
                AutomaticRedirectAfterSignOut = true,
                PostLogoutRedirectUri = "/"
            });
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private LoginViewModel BuildLoginViewModel(LoginInputModel model)
        {
            return new LoginViewModel
            {
                Username = model.Username,
                RememberLogin = model.RememberLogin,
                ReturnUrl = model.ReturnUrl,
                EnableLocalLogin = true,
                AllowRememberLogin = AccountOptions.AllowRememberLogin
            };
        }
    }
}
