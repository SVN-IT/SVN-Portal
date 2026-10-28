using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Sigma_Tools.Controllers
{
    [AllowAnonymous]
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<AccountController> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
        }

        [AllowAnonymous]
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        // GET: /Account/Login
        [HttpPost]
        public IActionResult Login(bool isLogin = true)
        {
            int appId = _configuration.GetValue<int>("AppConfig:AppID");

            if (appId <= 0)
                return Problem("AppConfig:AppID chưa được cấu hình.");

            string? baseUrl = GetAuthenticationApiBaseUrl();

            if (string.IsNullOrWhiteSpace(baseUrl))
                return Problem("Chưa cấu hình Authentication API URL.");

            string loginPath = _configuration[
                "AuthenticationAPIConfig:MicrosoftLoginURL"]
                ?? "api/Account/microsoft/login";

            string loginUrl =
                $"{baseUrl.TrimEnd('/')}/{loginPath.TrimStart('/')}";

            string redirectUrl =
                $"{loginUrl}?appId={appId}";

            return Redirect(redirectUrl);
        }

        // GET: /Account/MicrosoftCallback?code=...
        [HttpGet]
        public async Task<IActionResult> MicrosoftCallback(
            [FromQuery] string? code,
            [FromQuery] string? error)
        {
            if (!string.IsNullOrWhiteSpace(error))
            {
                _logger.LogWarning(
                    "Microsoft SSO returned an error: {Error}", error);

                TempData["LoginError"] =
                    "Đăng nhập Microsoft không thành công.";

                return RedirectToAction(nameof(Login));
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                TempData["LoginError"] =
                    "Không nhận được mã đăng nhập từ Authentication API.";

                return RedirectToAction(nameof(Login));
            }

            int appId = _configuration.GetValue<int>("AppConfig:AppID");

            if (appId <= 0)
            {
                _logger.LogError("AppConfig:AppID is missing or invalid.");
                return Problem("Ứng dụng chưa được cấu hình AppID.");
            }

            string? baseUrl = GetAuthenticationApiBaseUrl();

            if (string.IsNullOrWhiteSpace(baseUrl))
                return Problem("Chưa cấu hình Authentication API URL.");

            string exchangePath = _configuration[
                "AuthenticationAPIConfig:MicrosoftExchangeURL"]
                ?? "api/Account/microsoft/exchange";

            string exchangeUrl =
                $"{baseUrl.TrimEnd('/')}/{exchangePath.TrimStart('/')}";

            try
            {
                var client = _httpClientFactory.CreateClient(
                    "AuthenticationAPI");

                using var response = await client.PostAsJsonAsync(
                    exchangeUrl,
                    new
                    {
                        AppId = appId,
                        Code = code
                    });

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "SSO code exchange failed with HTTP status {StatusCode}.",
                        (int)response.StatusCode);

                    TempData["LoginError"] =
                        "Không thể xác nhận phiên đăng nhập. Vui lòng thử lại.";

                    return RedirectToAction(nameof(Login));
                }

                string json = await response.Content.ReadAsStringAsync();

                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;

                // 1. Kiểm tra kết quả đăng nhập
                if (!TryGetPropertyIgnoreCase(root, "OK", out var okElement)
                    || okElement.ValueKind != JsonValueKind.True)
                {
                    TempData["LoginError"] =
                        "Đăng nhập thất bại hoặc dữ liệu trả về không hợp lệ.";

                    return RedirectToAction(nameof(Login));
                }

                // 2. Lấy Content -> JwtData -> User
                if (!TryGetPropertyIgnoreCase(root, "Content", out var content)
                    || !TryGetPropertyIgnoreCase(content, "JwtData", out var jwtData)
                    || !TryGetPropertyIgnoreCase(jwtData, "User", out var user))
                {
                    _logger.LogWarning(
                        "Login response does not contain Content.JwtData.User.");

                    TempData["LoginError"] =
                        "Không tìm thấy thông tin người dùng trong kết quả đăng nhập.";

                    return RedirectToAction(nameof(Login));
                }

                // 3. Đọc thông tin người dùng
                string userId = GetStringProperty(user, "ID") ?? "";
                string userName = GetStringProperty(user, "UserName") ?? "";
                string fullName = GetStringProperty(user, "FullName") ?? userName;
                string email = GetStringProperty(user, "EmailAddress") ?? "";
                string companyId = GetStringProperty(user, "CompanyID") ?? "";
                string department = GetStringProperty(user, "Department") ?? "";

                // Bắt buộc có định danh người dùng
                if (string.IsNullOrWhiteSpace(userId)
                    || string.IsNullOrWhiteSpace(userName))
                {
                    TempData["LoginError"] =
                        "Thông tin người dùng trả về không đầy đủ.";

                    return RedirectToAction(nameof(Login));
                }

                // 4. Tạo claims cơ bản
                var claims = new List<Claim>
                            {
                                new(ClaimTypes.NameIdentifier, userId),
                                new(ClaimTypes.Name, userName),
                                new("FullName", fullName),
                                new("CompanyID", companyId),
                                new("Department", department)
                            };

                if (!string.IsNullOrWhiteSpace(email))
                    claims.Add(new Claim(ClaimTypes.Email, email));

                // 5. Đưa Roles vào role claims nếu API trả về mảng chuỗi
                if (TryGetPropertyIgnoreCase(user, "Roles", out var roles))
                {
                    if (roles.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var role in roles.EnumerateArray())
                        {
                            if (role.ValueKind == JsonValueKind.String
                                && !string.IsNullOrWhiteSpace(role.GetString()))
                            {
                                claims.Add(new Claim(
                                    ClaimTypes.Role, role.GetString()!));
                            }
                        }
                    }
                    else if (roles.ValueKind == JsonValueKind.String)
                    {
                        string? role = roles.GetString();

                        if (!string.IsNullOrWhiteSpace(role))
                            claims.Add(new Claim(ClaimTypes.Role, role));
                    }
                }

                // 6. Lưu dữ liệu gốc để sử dụng trong các controller khác
                HttpContext.Session.SetString(
                    "LoginResult", root.GetRawText());

                HttpContext.Session.SetString(
                    "JwtData", jwtData.GetRawText());

                HttpContext.Session.SetString(
                    "UserInfo", user.GetRawText());

                if (TryGetPropertyIgnoreCase(user, "ObjectRights", out var objectRights))
                {
                    HttpContext.Session.SetString(
                        "ObjectRights", objectRights.GetRawText());
                }

                if (TryGetPropertyIgnoreCase(user, "StoreIDs", out var storeIds))
                {
                    HttpContext.Session.SetString(
                        "StoreIDs", storeIds.GetRawText());
                }

                // 7. Tạo cookie đăng nhập
                var identity = new ClaimsIdentity(
                    claims,
                    CookieAuthenticationDefaults.AuthenticationScheme);

                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties
                    {
                        IsPersistent = true,
                        AllowRefresh = true
                    });

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, "Error processing Microsoft SSO callback.");

                TempData["LoginError"] =
                    "Có lỗi khi xử lý đăng nhập. Vui lòng thử lại.";

                return RedirectToAction(nameof(Login));
            }
        }

        // POST: /Account/Logout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Remove("LoginResult");

            await HttpContext.SignOutAsync(
                CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction(nameof(Login));
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        private string? GetAuthenticationApiBaseUrl()
        {
            string? productMode =
                _configuration["AppConfig:ProductMode"];

            string key = string.Equals(
                productMode,
                "Prod",
                StringComparison.OrdinalIgnoreCase)
                    ? "AuthenticationAPIConfig:ProdBaseAPIUrl"
                    : "AuthenticationAPIConfig:DevBaseAPIUrl";

            return _configuration[key];
        }

        private static string? FindString(
            JsonElement element,
            string propertyName)
        {
            if (!TryFindProperty(element, propertyName, out var value))
                return null;

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.ToString(),
                _ => null
            };
        }

        private static bool TryFindProperty(
            JsonElement element,
            string propertyName,
            out JsonElement result)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(
                        property.Name,
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        result = property.Value;
                        return true;
                    }
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (TryFindProperty(
                        property.Value, propertyName, out result))
                    {
                        return true;
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    if (TryFindProperty(item, propertyName, out result))
                        return true;
                }
            }

            result = default;
            return false;
        }


        private static bool TryGetPropertyIgnoreCase(
            JsonElement element,
            string propertyName,
            out JsonElement result)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(
                        property.Name,
                        propertyName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        result = property.Value;
                        return true;
                    }
                }
            }

            result = default;
            return false;
        }

        private static string? GetStringProperty(
            JsonElement element,
            string propertyName)
        {
            if (!TryGetPropertyIgnoreCase(
                element, propertyName, out var value))
                return null;

            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.ToString(),
                _ => null
            };
        }

    }

}