using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using SVN_Authentication_Portal.Configurations;
using SVN_Authentication_Portal.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
AppConfig appConfig = builder.Configuration.GetSection("AppConfig").Get<AppConfig>();
AuthenticationAPIConfig authenticationAPIConfig = builder.Configuration.GetSection("AuthenticationAPIConfig").Get<AuthenticationAPIConfig>();

//builder.Services
//.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
//.AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
})
.AddMicrosoftIdentityWebApp(builder.Configuration.GetSection("AzureAd"));

authenticationAPIConfig.ProductMode = appConfig.ProductMode;

builder.Services.AddSingleton(appConfig);
builder.Services.AddSingleton(authenticationAPIConfig);
builder.Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
builder.Services.AddSingleton<SVNSignInManager>();
builder.Services.AddSingleton<AccountService>();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(10);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        await context.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);
        await next();
    });

    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
