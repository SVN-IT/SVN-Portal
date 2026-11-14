using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Toast.Web.Features.Account;

[Route("toast/admin/account")]
public sealed class AccountController(IOptionsMonitor<AdminUsersOptions> users) : Controller
{
    [HttpGet("login")]
    public IActionResult Login(string? returnUrl) => View(model: returnUrl);

    [HttpPost("login"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string userName, string password, string? returnUrl)
    {
        // CurrentValue: sửa mật khẩu trong appsettings là có hiệu lực ngay, không cần restart app
        var user = users.CurrentValue.AdminUsers.FirstOrDefault(u =>
            string.Equals(u.UserName, userName?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (user is null || string.IsNullOrEmpty(user.Password) || user.Password != password)
        {
            ViewBag.Error = "Sai tài khoản hoặc mật khẩu.";
            return View(model: returnUrl);
        }
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, user.UserName)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(new ClaimsPrincipal(identity));
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/toast/admin");
    }

    [HttpPost("logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        return Redirect("/toast/admin/account/login");
    }
}
