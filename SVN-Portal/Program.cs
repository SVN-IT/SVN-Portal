using SVN_Portal.Services.Configurations;
using SVN_Portal.Services.Helpers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

AppConfig appConfig = builder.Configuration.GetSection("AppConfig").Get<AppConfig>();
DBConfiguration dBConfiguration = builder.Configuration.GetSection("DBConfiguration").Get<DBConfiguration>();
QCInfoConfig qCInfoConfig = builder.Configuration.GetSection("QCInfoConfig").Get<QCInfoConfig>();
OperInfoConfig operInfoConfig = builder.Configuration.GetSection("OperInfoConfig").Get<OperInfoConfig>();
APIConfiguration aPIConfiguration = builder.Configuration.GetSection("APIConfiguration").Get<APIConfiguration>();
TOASTLabelConfiguration labelConfiguration = builder.Configuration.GetSection("TOASTLabelConfiguration").Get<TOASTLabelConfiguration>();
dBConfiguration.ProductMode = appConfig.ProductMode;

builder.Services.AddSingleton(appConfig);
builder.Services.AddSingleton(dBConfiguration);
builder.Services.AddSingleton(qCInfoConfig);
builder.Services.AddSingleton(operInfoConfig);
builder.Services.AddSingleton(aPIConfiguration);
builder.Services.AddSingleton(labelConfiguration);
builder.Services.AddSingleton<ToolsHelper>();

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
    pattern: "{controller=Home}/{action=ProductionResult}/{id?}");

app.Run();
