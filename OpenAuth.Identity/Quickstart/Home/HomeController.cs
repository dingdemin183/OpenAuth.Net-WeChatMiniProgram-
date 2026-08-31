using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace OpenAuth.IdentityServer.Quickstart.Home
{
    [AllowAnonymous]
    public class HomeController : Controller
    {
        private readonly IHostEnvironment _environment;
        private readonly ILogger _logger;

        public HomeController(IHostEnvironment environment, ILogger<HomeController> logger)
        {
            _environment = environment;
            _logger = logger;
        }

        public IActionResult Index()
        {
            if (_environment.IsDevelopment())
            {
                return View();
            }

            _logger.LogInformation("Homepage is disabled in production. Returning 404.");
            return NotFound();
        }

        /// <summary>
        /// 显示错误页面
        /// </summary>
        public IActionResult Error(string errorId)
        {
            var vm = new ErrorViewModel
            {
                ErrorId = errorId
            };

            return View("Error", vm);
        }
    }
}