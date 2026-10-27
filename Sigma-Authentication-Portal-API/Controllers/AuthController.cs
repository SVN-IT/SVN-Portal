using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sigma_Authentication_Portal_API.Models;
using Sigma_Authentication_Portal_API.Services;
using Sigma_Authentication_Portal_API.Tickets;
using System;

namespace Sigma_Authentication_Portal_API.Controllers
{
    [Route("api/auth")]
    public sealed class AuthController : Controller
    {
        private readonly IClientAppRegistry _clientApps;
        private readonly IInternalAuthService _internalAuth;
        private readonly ILoginTicketStore _tickets;

        public AuthController(
            IClientAppRegistry clientApps,
            IInternalAuthService internalAuth,
            ILoginTicketStore tickets)
        {
            _clientApps = clientApps;
            _internalAuth = internalAuth;
            _tickets = tickets;
        }

        [AllowAnonymous]
        [HttpPost("password/login")]
        public async Task<IActionResult> PasswordLogin(
                [FromBody] PasswordLoginRequest request,
                CancellationToken cancellationToken)
        {
            var clientApp = _clientApps.FindByAppId(request.AppId);

            if (clientApp is null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "AppId không hợp lệ."
                });
            }

            if (string.IsNullOrWhiteSpace(request.Username) ||
                string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Vui lòng nhập tài khoản và mật khẩu."
                });
            }

            var result = await _internalAuth.LoginWithPasswordAsync(
                request.Username,
                request.Password,
                cancellationToken);

            if (!result.Success ||
                string.IsNullOrWhiteSpace(result.AccessToken))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Tài khoản hoặc mật khẩu không hợp lệ."
                });
            }

            var code = _tickets.Create(clientApp.AppId, result);

            return Ok(new
            {
                success = true,
                redirectUrl = BuildResultUrl(
                    clientApp.CallbackUrl,
                    ("code", code))
            });
        }

        [AllowAnonymous]
        [HttpGet("microsoft/login")]
        public IActionResult MicrosoftLogin([FromQuery] string appId)
        {
            var clientApp = _clientApps.FindByAppId(appId);

            if (clientApp is null)
                return BadRequest("AppId không hợp lệ hoặc chưa đăng ký.");

            var properties = new AuthenticationProperties
            {
                // Middleware OIDC sẽ quay về endpoint này sau khi đăng nhập.
                RedirectUri = Url.Action(
                    nameof(MicrosoftComplete),
                    "Auth",
                    new { appId = clientApp.AppId })
            };

            return Challenge(
                properties,
                OpenIdConnectDefaults.AuthenticationScheme);
        }

        [Authorize]
        [HttpGet("microsoft/complete")]
        public async Task<IActionResult> MicrosoftComplete(
            [FromQuery] string appId,
            CancellationToken cancellationToken)
        {
            var clientApp = _clientApps.FindByAppId(appId);

            if (clientApp is null)
                return BadRequest("AppId không hợp lệ.");

            var result = await _internalAuth.LoginWithMicrosoftAsync(
                User, cancellationToken);

            if (!result.Success ||
                string.IsNullOrWhiteSpace(result.AccessToken))
            {
                // Chỉ chuyển mã lỗi chung, không đưa token hoặc dữ liệu
                // nhạy cảm vào URL.
                return Redirect(BuildResultUrl(
                    clientApp.CallbackUrl,
                    ("error", "login_failed")));
            }

            var code = _tickets.Create(clientApp.AppId, result);

            return Redirect(BuildResultUrl(
                clientApp.CallbackUrl,
                ("code", code)));
        }

        [AllowAnonymous]
        [HttpPost("exchange")]
        public IActionResult Exchange([FromBody] ExchangeRequest request)
        {
            var clientApp = _clientApps.FindByAppId(request.AppId);

            if (clientApp is null ||
                string.IsNullOrWhiteSpace(request.Code))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Thông tin đổi code không hợp lệ."
                });
            }

            if (!_tickets.TryRedeem(
                    request.Code,
                    clientApp.AppId,
                    out var result) ||
                result is null)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Code không hợp lệ, đã hết hạn hoặc đã được sử dụng."
                });
            }

            return Ok(new
            {
                success = true,
                accessToken = result.AccessToken,
                expiresIn = result.ExpiresIn,
                user = result.User,
                permissions = result.Permissions
            });
        }

        private static string BuildResultUrl(
            string callbackUrl,
            params (string Key, string Value)[] query)
        {
            var uri = new Uri(callbackUrl);
            var builder = new UriBuilder(uri);

            var existingQuery = Microsoft.AspNetCore.WebUtilities
                .QueryHelpers.ParseQuery(builder.Query)
                .ToDictionary(
                    x => x.Key,
                    x => x.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase);

            foreach (var item in query)
                existingQuery[item.Key] = item.Value;

            builder.Query = string.Join(
                "&",
                existingQuery.Select(x =>
                    $"{Uri.EscapeDataString(x.Key)}=" +
                    $"{Uri.EscapeDataString(x.Value)}"));

            return builder.Uri.ToString();
        }
    }
}
