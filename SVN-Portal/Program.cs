using SVN_Portal.Services.Configurations;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

AppConfig appConfig = builder.Configuration.GetSection("AppConfig").Get<AppConfig>();
DBConfiguration dBConfiguration = builder.Configuration.GetSection("DBConfiguration").Get<DBConfiguration>();
QCInfoConfig qCInfoConfig = builder.Configuration.GetSection("QCInfoConfig").Get<QCInfoConfig>();
dBConfiguration.ProductMode = appConfig.ProductMode;

builder.Services.AddSingleton(appConfig);
builder.Services.AddSingleton(dBConfiguration);
builder.Services.AddSingleton(qCInfoConfig);

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
