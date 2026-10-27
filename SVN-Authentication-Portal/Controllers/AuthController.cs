
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SVN_Authentication_Portal.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : Controller
{
    //private readonly ICompanyAuthenticationService _companyAuth;
    //private readonly ILoginTicketStore _ticketStore;
    //private readonly IClientAppRegistry _clientApps;

    public AuthController()
        //ICompanyAuthenticationService companyAuth,
        //ILoginTicketStore ticketStore,
        //IClientAppRegistry clientApps)
    {
        //_companyAuth = companyAuth;
        //_ticketStore = ticketStore;
        //_clientApps = clientApps;
    }

    // Bước 1: Client mở trình duyệt đến endpoint này.
    // GET /api/auth/microsoft/login?appId=SVN001
    [AllowAnonymous]
    [HttpGet("microsoft/login")]
    public IActionResult LoginWithMicrosoft(
        [FromQuery] string appId)
    {
        if (string.IsNullOrWhiteSpace(appId))
            return BadRequest(new { message = "AppId is required." });

        // Chỉ chấp nhận ứng dụng đã đăng ký.
        //var clientApp = _clientApps.Find(appId);
        var clientApp = 1;

        if (clientApp == 0)
            return BadRequest(new { message = "Invalid AppId." });

        var properties = new AuthenticationProperties
        {
            // Middleware xử lý /signin-oidc trước.
            // Sau đó mới chuyển đến callback nghiệp vụ.
            RedirectUri = Url.Action(
                nameof(MicrosoftCallback),
                "Auth",
                new { appId },
                Request.Scheme)
        };

        // Giữ AppId trong authentication state đã được middleware bảo vệ.
        properties.Items["AppId"] = appId;

        return Challenge(
            properties,
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    // Bước 2: Chạy sau khi OpenID Connect middleware
    // xác thực Microsoft thành công.
    [Authorize]
    [HttpGet("microsoft/callback")]
    public async Task<IActionResult> MicrosoftCallback(
        [FromQuery] string appId)
    {
        //var clientApp = _clientApps.Find(appId);
        var clientApp = 1;

        if (clientApp == 0)
            return BadRequest(new { message = "Invalid AppId." });

        var microsoftOid = User.FindFirstValue("oid");
        var tenantId = User.FindFirstValue("tid");
        var email = User.FindFirstValue("preferred_username");
        var displayName = User.FindFirstValue("name");

        if (string.IsNullOrWhiteSpace(microsoftOid) ||
            string.IsNullOrWhiteSpace(tenantId))
        {
            return Unauthorized(new
            {
                message = "Missing Microsoft identity claims."
            });
        }

        try
        {
            // Bước 3: Đăng nhập hệ thống công ty.
            // Service cần ánh xạ Microsoft identity sang tài khoản nội bộ.
            //var companyResult = await _companyAuth.LoginWithMicrosoftAsync(
            //    appId,
            //    tenantId,
            //    microsoftOid,
            //    email,
            //    displayName);

            bool Success = true;
            //if (!companyResult.Success)
            if (Success)
            {
                return Redirect(
                    BuildClientResultUrl(
                        "https://ds.sigmaworldwide.io/sigmatools/Oracle/POInbound#",
                        "login_failed"));
            }

            // Bước 4: Lưu kết quả phía server và tạo mã dùng một lần.
            // Ticket chỉ được đổi một lần và phải có thời hạn ngắn.
            //var ticket = await _ticketStore.CreateAsync(
            //    appId,
            //    companyResult,
            //    TimeSpan.FromMinutes(1));
            var ticket = "abc";

            // Bước 5: Chuyển về callback đã đăng ký của client.
            return Redirect(
                BuildClientResultUrl(
                    "https://ds.sigmaworldwide.io/sigmatools/Oracle/POInbound#",
                    ticket));
        }
        catch (Exception)
        {
            // Không trả exception, token hoặc dữ liệu nhạy cảm ra URL.
            return Redirect(
                BuildClientResultUrl(
                    "https://ds.sigmaworldwide.io/sigmatools/Oracle/POInbound#",
                    "login_failed"));
        }
    }

    // Bước 6: Client gửi ticket đến endpoint này để đổi kết quả.
    [AllowAnonymous]
    [HttpPost("exchange")]
    public async Task<IActionResult> Exchange(
        [FromBody] ExchangeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AppId) ||
            string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new
            {
                message = "AppId and Code are required."
            });
        }

        //var clientApp = _clientApps.Find(request.AppId);
        var clientApp = 1;

        if (clientApp != 1)
            return Unauthorized();

        // Store phải kiểm tra AppId, hạn sử dụng và tiêu thụ ticket
        // theo cách atomic để không thể đổi lại lần thứ hai.
        //var result = await _ticketStore.RedeemAsync(
        //    request.AppId,
        //    request.Code);
        var result = true;
        if (result)
        {
            return Unauthorized(new
            {
                success = false,
                message = "Invalid or expired login code."
            });
        }

        return Ok(new
        {
            success = true,
            appId = request.AppId,
            //accessToken = result.AccessToken,
            //expiresIn = result.ExpiresIn,
            //user = result.User,
            //permissions = result.Permissions
        });
    }

    private static string BuildClientResultUrl(
        string callbackUrl,
        string result)
    {
        var separator = callbackUrl.Contains('?') ? "&" : "?";

        return $"{callbackUrl}{separator}code=" +
            Uri.EscapeDataString(result);
    }
}

public sealed class ExchangeRequest
{
    public string AppId { get; set; } = "";
    public string Code { get; set; } = "";
}
