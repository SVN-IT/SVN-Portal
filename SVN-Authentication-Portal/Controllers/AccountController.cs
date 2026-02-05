using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SVN_Authentication_Portal.Models;
using SVN_Authentication_Portal.Services;
using System.Security.Claims;

namespace SVN_Authentication_Portal.Controllers
{
    public class AccountController : Controller
    {
        SVNSignInManager signInManager;
        AccountService accountService;
        public AccountController(SVNSignInManager signInManager, AccountService accountService)
        {
            this.signInManager = signInManager;
            this.accountService = accountService;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Login(string? ReturnUrl = null)
        {
            //Trong trường hợp đăng nhập bằng 
            if (User.Identity?.IsAuthenticated == true)
            {
                var accessToken = await HttpContext.GetTokenAsync("access_token");
                var idToken = await HttpContext.GetTokenAsync("id_token");

                var email = User.FindFirst("preferred_username")?.Value
                            ?? User.FindFirst(ClaimTypes.Email)?.Value;

                // TODO: xử lý login nội bộ ở đây
                LoginViewModel viewModel = new LoginViewModel();
                viewModel.UserName = email ?? "";
                viewModel.Pwd = "";
                var userInfo = await accountService.LoginByEmail(viewModel);
            }

            if (signInManager.IsSignedIn())
            {
                return RedirectToAction("Index", "Home");
            }
            else
            {
                ViewData["ReturnUrl"] = ReturnUrl;
                LoginViewModel model = new LoginViewModel();
                return View(model);
            }

        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? ReturnUrl = null)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            bool isLogin = await accountService.Login(model);
            //bool isLogin = true;

            if (isLogin)
            {
                if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                {
                    return Redirect(ReturnUrl);
                }
                else
                {
                    // Redirect to default page
                    return RedirectToAction("Index", "HomeV2");
                }
            }
            else
            {
                return View();
            }

        }

        public IActionResult LoginWithMicrosoft(LoginViewModel model, string? ReturnUrl = null)
        {

            return Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = "/Account/Login"
                },
                OpenIdConnectDefaults.AuthenticationScheme
            );

        }

        public async Task<IActionResult> Logout()
        {
            try
            {
                await signInManager.SignOutAsync();

                return RedirectToAction("Login", "Account");
            }
            catch
            {
                return RedirectToAction("Index", "HomeV2");
            }
        }
    }
}
