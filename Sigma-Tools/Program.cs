
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Sigma_Tools.Services.Authorizations;
using Sigma_Tools.Services.Configurations;

var builder = WebApplication.CreateBuilder(args);

// 1. MVC
builder.Services.AddControllersWithViews();

// 2. Load configuration
AppConfig appConfig = builder.Configuration
    .GetSection("AppConfig")
    .Get<AppConfig>()
    ?? throw new InvalidOperationException("Missing AppConfig configuration.");

DBConfiguration dbConfiguration = builder.Configuration
    .GetSection("DBConfiguration")
    .Get<DBConfiguration>()
    ?? throw new InvalidOperationException("Missing DBConfiguration configuration.");

dbConfiguration.ProductMode = appConfig.ProductMode;

builder.Services.AddSingleton(appConfig);
builder.Services.AddSingleton(dbConfiguration);

// 3. HTTP client: gọi Authentication API
builder.Services.AddHttpClient("AuthenticationAPI", client =>
{
    // Base URL sẽ được cấu hình ở bước tiếp theo
    // dựa trên appsettings.json của Sigma-Tools.
    client.Timeout = TimeSpan.FromSeconds(30);
});

// 4. Session: lưu thông tin đăng nhập phía server
builder.Services.AddDistributedMemoryCache();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IPermissionService, PermissionService>();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.Name = ".SigmaTools.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// 5. Cookie Authentication
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = ".SigmaTools.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";

        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;

        // Kiểm tra Session khi xác thực cookie
        options.Events.OnValidatePrincipal = async context =>
        {
            var session = context.HttpContext.Session;

            string? loginResult =
                session.GetString("LoginResult");

            string? accessToken =
                session.GetString("JwtData");

            string? userInfo =
                session.GetString("UserInfo");

            // Cookie còn hiệu lực nhưng Session không còn dữ liệu đăng nhập
            if (loginResult != "True" ||
                string.IsNullOrEmpty(accessToken) ||
                string.IsNullOrEmpty(userInfo))
            {
                context.RejectPrincipal();

                await context.HttpContext.SignOutAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// 6. HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Giữ nguyên cấu hình PathBase của dự án
app.UsePathBase(appConfig.LocationPath);

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Session trước khi xử lý controller
app.UseSession();

// Xác thực trước khi kiểm tra quyền
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
