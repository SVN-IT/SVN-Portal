using Sigma_Dashboard.Services;
using Sigma_Dashboard.Services.Configurations;
using Sigma_Dashboard.Services.Helpers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

//Add Configuration
AppConfig appConfig = builder.Configuration.GetSection("AppConfig").Get<AppConfig>();
DBConfiguration dBConfiguration = builder.Configuration.GetSection("DBConfiguration").Get<DBConfiguration>();
dBConfiguration.ProductMode = appConfig.ProductMode;

builder.Services.AddSingleton(appConfig);
builder.Services.AddSingleton(dBConfiguration);

//Add Services
builder.Services.AddSingleton<AppSettingServices>();
builder.Services.AddSingleton<SectionTimeServices>();

//Add Helper
builder.Services.AddSingleton<HomeControllerHelper>();

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
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
