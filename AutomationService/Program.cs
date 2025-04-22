using AutomationService.Configurations;
using AutomationService.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

var logDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Logs");
Log.Logger = new LoggerConfiguration()
    .WriteTo.File(Path.Combine(logDirectory, "api-log-.txt"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14)
    .CreateLogger();
builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddControllersWithViews();

APIConfiguration aPIConfiguration = builder.Configuration.GetSection("APIConfiguration").Get<APIConfiguration>();

builder.Services.AddSingleton(aPIConfiguration);

builder.Services.AddSingleton<APIService>();

builder.Services.AddHostedService<TimedHostedService>();

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
