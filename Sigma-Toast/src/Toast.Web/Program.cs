using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Toast.Web.Data;
using Toast.Web.Features.Account;
using Toast.Web.Features.LabelPrint;
using Toast.Web.Features.LabelTemplate;
using Toast.Web.Features.Package;
using Toast.Web.Features.Printer;
using Toast.Web.Features.Station;
using Toast.Web.Features.Viindoo;

var builder = WebApplication.CreateBuilder(args);
// Biến tự điền của mẫu tem để ở file riêng giống SVN-Portal (sửa không cần khởi động lại)
builder.Configuration.AddJsonFile("Configs/label-variables.json", optional: false, reloadOnChange: true);

builder.Services.Configure<AdminUsersOptions>(builder.Configuration.GetSection(AdminUsersOptions.Section));
builder.Services.Configure<LabelTemplateOptions>(builder.Configuration.GetSection(LabelTemplateOptions.Section));
builder.Services.AddScoped<PrinterLookup>();
builder.Services.AddScoped<PackageService>();
builder.Services.AddScoped<WorkOrderReader>();
builder.Services.AddScoped<Toast.Web.Features.Station.SessionService>();
builder.Services.AddHostedService<Toast.Web.Features.Station.SessionAutoEndService>();
builder.Services.AddSingleton<Toast.Web.Features.Station.ClientLogService>();
builder.Services.AddScoped<Toast.Web.Features.Station.StationLabelService>();
builder.Services.AddScoped<Toast.Web.Features.ProductionPush.ProductionPushService>();
builder.Services.AddMemoryCache();
builder.Services.Configure<PackageOptions>(builder.Configuration.GetSection(PackageOptions.Section));
builder.Services.Configure<StationOptions>(builder.Configuration.GetSection(StationOptions.Section));
builder.Services.Configure<LabelVariablesOptions>(builder.Configuration.GetSection(LabelVariablesOptions.Section));
// Đọc Viindoo trực tiếp bằng JSON-RPC (chỉ đọc). Handler giữ cookie session, không xoay vòng để khỏi mất phiên đăng nhập.
builder.Services.Configure<ViindooRpcOptions>(builder.Configuration.GetSection(ViindooRpcOptions.Section));
builder.Services.AddHttpClient(ViindooRpc.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(30))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { UseCookies = true, CookieContainer = new System.Net.CookieContainer() })
    .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
builder.Services.AddSingleton<ViindooRpc>();
// Danh sách nhân viên (mã SVN, tên, điện thoại) đọc từ Viindoo, nhớ RAM 1 giờ
builder.Services.AddSingleton<Toast.Web.Features.Station.EmployeeDirectory>();
// Tra cứu Viindoo: phân loại dự án theo nhóm sản phẩm (ViindooProjects trong appsettings.json)
builder.Services.Configure<ViindooProjectsOptions>(builder.Configuration.GetSection(ViindooProjectsOptions.Section));
builder.Services.AddSingleton<ViindooCatalog>();
builder.Services.AddScoped<Toast.Web.Features.WorkOrderLookup.WorkOrderLookupService>();
builder.Services.AddScoped<Toast.Web.Features.ProductLookup.ProductLookupService>();

// Máy dev chưa có DB: khai báo ConnectionStrings:ToastSqlite (appsettings.Development.json) thì dùng file SQLite,
// bảng tự tạo khi khởi động. Server thật dùng SQL Server, bảng tạo bằng script trong db/scripts/.
string? sqlitePath = builder.Configuration.GetConnectionString("ToastSqlite");
builder.Services.AddDbContext<ToastDbContext>(o =>
{
    if (!string.IsNullOrEmpty(sqlitePath))
        o.UseSqlite(sqlitePath);
    else
        o.UseSqlServer(builder.Configuration.GetConnectionString("Toast"),
            sql => sql.MigrationsHistoryTable("Toast__EFMigrationsHistory", "dbo"));
});

// Khoá Data Protection (mã chống giả mạo form + cookie đăng nhập admin): lưu cố định ngoài thư mục bản build, tên app cố định.
// Không cấu hình thì khoá chỉ nằm trong RAM / tách theo thư mục app -> restart hoặc deploy bản mới (thư mục mới) thì trang đang mở
// gửi lên bị 400 và admin bị đăng xuất. Toast:DataProtectionKeysDir đổi chỗ lưu; mặc định Windows = C:\ProgramData\SigmaToast\DataProtection-Keys
// (khoá mã hoá DPAPI của máy), máy khác (dev Linux) = <thư mục app>/keys.
string keysDir = builder.Configuration["Toast:DataProtectionKeysDir"] is { Length: > 0 } configuredKeys
    ? (Path.IsPathRooted(configuredKeys) ? configuredKeys : Path.Combine(builder.Environment.ContentRootPath, configuredKeys))
    : OperatingSystem.IsWindows()
        ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SigmaToast", "DataProtection-Keys")
        : Path.Combine(builder.Environment.ContentRootPath, "keys");
var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("SigmaToast")
    .PersistKeysToFileSystem(new DirectoryInfo(Path.GetFullPath(keysDir)));
if (OperatingSystem.IsWindows()) dataProtection.ProtectKeysWithDpapi(protectToLocalMachine: true);

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.Cookie.Name = "ToastAdmin";
        o.LoginPath = "/toast/admin/account/login";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        // Gọi API bằng fetch (X-Requested-With) mà hết phiên: trả 401 để JS tự chuyển về trang đăng nhập
        o.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Headers.XRequestedWith == "XMLHttpRequest")
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            else
                ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews();
// Feature folder: view nằm cạnh controller trong Features/<Tên>/, layout và partial dùng chung trong Shared/
builder.Services.Configure<RazorViewEngineOptions>(o =>
{
    o.ViewLocationFormats.Clear();
    o.ViewLocationFormats.Add("/Features/{1}/{0}.cshtml");
    o.ViewLocationFormats.Add("/Shared/{0}.cshtml");
});

var app = builder.Build();

if (!string.IsNullOrEmpty(sqlitePath))
    DevSqlite.EnsureSchema(app.Services, sqlitePath);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// /toast: màn công nhân (quét, in tự động). /toast/admin: quản trị. Vào / thì chuyển sang /toast.
app.MapControllers();
app.MapGet("/", () => Results.Redirect("/toast"));
app.MapGet("/error", () => Results.Problem("Lỗi hệ thống, xem log server."));

app.Run();

/// <summary>Cho WebApplicationFactory trong test.</summary>
public partial class Program;
