using SVN_Portal.Services.Configurations;
using SVN_Portal.Services.Helpers;
using Serilog;
using SVN_Portal.DAL.DataPortal;
using SVN_Portal.Services.Util;

var builder = WebApplication.CreateBuilder(args);

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

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=ProductionResultV1}/{id?}");

app.Run();
