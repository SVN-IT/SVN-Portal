using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SVN_Portal.Services.Configurations;

namespace SVN_Portal.Areas.TwistAdmin.Controllers
{
    // Trang chủ + đăng nhập / đăng xuất khu quản trị Twist
    [Route("twist/admin")]
    public class AccountController : TwistAdminControllerBase
    {
        readonly IOptionsMonitor<TwistAdminAuthConfig> authConfig;

        public AccountController(IOptionsMonitor<TwistAdminAuthConfig> authConfig)
        {
            this.authConfig = authConfig;
        }

        // /twist/admin: hiện chỉ có 1 màn nên chuyển thẳng vào quản lý mẫu tem
        [HttpGet("")]
        public IActionResult Index()
        {
            return RedirectToAction(nameof(LabelTemplateController.Index), "LabelTemplate");
        }

        [AllowAnonymous]
        [HttpGet("login")]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            var auth = await HttpContext.AuthenticateAsync(TwistAdminAuthConfig.Scheme);
            if (auth.Succeeded)
            {
                return RedirectToAction(nameof(Index));
            }
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [AllowAnonymous]
        [HttpPost("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            var account = authConfig.CurrentValue.Accounts.FirstOrDefault(a =>
                !string.IsNullOrEmpty(a.Username)
                && string.Equals(a.Username, username?.Trim(), StringComparison.OrdinalIgnoreCase)
                && a.Password == password);

            if (account == null)
            {
                ViewData["ReturnUrl"] = returnUrl;
                ViewData["Username"] = username;
                ViewData["Error"] = "Sai tài khoản hoặc mật khẩu.";
                return View();
            }

            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, account.Username) }, TwistAdminAuthConfig.Scheme);
            await HttpContext.SignInAsync(TwistAdminAuthConfig.Scheme, new ClaimsPrincipal(identity));

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(TwistAdminAuthConfig.Scheme);
            return RedirectToAction(nameof(Login));
        }
    }
}
