using SVN_Portal.Services.Configurations;
using SVN_Portal.Services.Helpers;
using Serilog;
using SVN_Portal.DAL.DataPortal;
using SVN_Portal.Services.Util;

var builder = WebApplication.CreateBuilder(args);


//  "7100414428": [ "4064035145811", "EMEA-DCR03MB", "4" ],

//Log.Logger = new LoggerConfiguration()
//    .MinimumLevel.Information()
//    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning) // ASP.NET Core log >= Warning
//    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)    // System.* log >= Warning
//    .WriteTo.File(
//        Path.Combine(builder.Environment.WebRootPath, "Logs", "app.log"),
//        rollingInterval: RollingInterval.Day,
//        retainedFileCountLimit: 7
//    )
//    .CreateLogger();

//builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddHttpClient();
builder.Services.AddControllersWithViews();

AppConfig appConfig = builder.Configuration.GetSection("AppConfig").Get<AppConfig>();
DBConfiguration dBConfiguration = builder.Configuration.GetSection("DBConfiguration").Get<DBConfiguration>();
QCInfoConfig qCInfoConfig = builder.Configuration.GetSection("QCInfoConfig").Get<QCInfoConfig>();
OperInfoConfig operInfoConfig = builder.Configuration.GetSection("OperInfoConfig").Get<OperInfoConfig>();
APIConfiguration aPIConfiguration = builder.Configuration.GetSection("APIConfiguration").Get<APIConfiguration>();
TOASTLabelConfiguration labelConfiguration = builder.Configuration.GetSection("TOASTLabelConfiguration").Get<TOASTLabelConfiguration>();
dBConfiguration.ProductMode = appConfig.ProductMode;

var appSettingDataPortal = new SVN_AppSettingDataPortal(dBConfiguration.GetConnectionString());
string masterOperList = await appSettingDataPortal.GetMasterOperList();
if(!string.IsNullOrWhiteSpace(masterOperList))
{
    appConfig.MasterOperList = masterOperList;
}
//string strTimeChangeTabMainDashboard = await appSettingDataPortal.GetTimeChangeTabMainDashboard();
//if (!string.IsNullOrWhiteSpace(strTimeChangeTabMainDashboard) && int.TryParse(strTimeChangeTabMainDashboard, out int timeChangeTabMainDashboard))
//{
//    appConfig.timeChangeTabMainDashboard = timeChangeTabMainDashboard;
//}

builder.Services.AddSingleton(appConfig);
builder.Services.AddSingleton(dBConfiguration);
builder.Services.AddSingleton(qCInfoConfig);
builder.Services.AddSingleton(operInfoConfig);
builder.Services.AddSingleton(aPIConfiguration);
builder.Services.AddSingleton(labelConfiguration);
builder.Services.AddSingleton<ToolsHelper>();
builder.Services.AddSingleton<Pagination>();

// ===== Khu quản trị Twist (/twist/admin) - code ở Areas/TwistAdmin =====
// Sửa file trong Configs/ là có hiệu lực ngay, không cần restart. Thiếu file thì chỉ khu này bị ảnh hưởng.
builder.Configuration
    .AddJsonFile("Configs/label-template-options.json", optional: true, reloadOnChange: true)
    .AddJsonFile("Configs/twist-admin-accounts.json", optional: true, reloadOnChange: true)
    .AddJsonFile("Configs/label-variables.json", optional: true, reloadOnChange: true);
builder.Services.Configure<LabelTemplateOptions>(builder.Configuration.GetSection("LabelTemplateOptions"));
builder.Services.Configure<LabelVariablesConfig>(builder.Configuration.GetSection("LabelVariables"));
builder.Services.Configure<TwistAdminAuthConfig>(builder.Configuration.GetSection("TwistAdminAuth"));

// Không đặt default scheme: chỉ controller kế thừa TwistAdminControllerBase mới dùng cookie này
builder.Services.AddAuthentication()
    .AddCookie(TwistAdminAuthConfig.Scheme, o =>
    {
        o.Cookie.Name = ".SVN.TwistAdmin";
        o.Cookie.Path = "/twist/admin";
        o.Cookie.HttpOnly = true;
        o.LoginPath = "/twist/admin/login";
        o.AccessDeniedPath = "/twist/admin/login";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
        // Gọi API bằng fetch khi hết phiên: trả 401 thay vì redirect sang trang login
        o.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Tools}/{action=Index}/{id?}");

app.Run();
