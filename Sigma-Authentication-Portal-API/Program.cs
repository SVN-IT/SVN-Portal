using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Identity.Web;
using Sigma_Authentication_Portal_API.Models;
using Sigma_Authentication_Portal_API.Services;
using Sigma_Authentication_Portal_API.Tickets;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();

// Đăng nhập Microsoft qua OpenID Connect.
builder.Services
    .AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
    .AddMicrosoftIdentityWebApp(
        builder.Configuration.GetSection("AzureAd"));

builder.Services.AddAuthorization();

// Đăng ký danh sách Client App được phép truy cập.
builder.Services.AddSingleton<IClientAppRegistry, ClientAppRegistry>();

InternalAuth internalAuth = builder.Configuration.GetSection("InternalAuth").Get<InternalAuth>();
builder.Services.AddSingleton(internalAuth);

// HTTP client gọi API xác thực nội bộ.
builder.Services.AddHttpClient<IInternalAuthService, InternalAuthService>(
    (serviceProvider, client) =>
    {
        var configuration =
            serviceProvider.GetRequiredService<IConfiguration>();

        var baseUrl = configuration["InternalAuth:BaseUrl"];

        if (string.IsNullOrWhiteSpace(baseUrl) ||
            !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException(
                "InternalAuth:BaseUrl chưa được cấu hình hợp lệ.");
        }

        client.BaseAddress = baseUri;
        client.Timeout = TimeSpan.FromSeconds(30);
    });

// Ticket tạm thời dùng cho môi trường phát triển một instance.
builder.Services.AddSingleton<ILoginTicketStore, InMemoryLoginTicketStore>();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
