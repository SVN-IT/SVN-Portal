using SVNShareLib;
using ViidooDBServiceAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

ViindooDBConfig viindooDBConfig = builder.Configuration.GetSection("ViindooDBConfig").Get<ViindooDBConfig>();
SVNDBConfig sVNDBConfig = builder.Configuration.GetSection("SVNDBConfig").Get<SVNDBConfig>();
APIConfig aPIConfig = builder.Configuration.GetSection("APIConfig").Get<APIConfig>();

builder.Services.AddSingleton(viindooDBConfig);
builder.Services.AddSingleton(sVNDBConfig);
builder.Services.AddSingleton(aPIConfig);
builder.Services.AddSingleton<ConvertDataService>();
builder.Services.AddSingleton<DBService>();
builder.Services.AddSingleton<OdooRpcDBService>();
builder.Services.AddSingleton<ViindooDataService>();
builder.Services.AddSingleton<JsonRpcDataService>();
builder.Services.AddSingleton<NewViindooDataService>();
builder.Services.AddSingleton<OdooAPIService>();



var app = builder.Build();

// --- ĐOẠN CODE CHÈN THÊM VÀO ĐÂY ---
using (var scope = app.Services.CreateScope())
{
    // 1. Lấy OdooAPIService ra từ DI Container
    var odooAPIService = scope.ServiceProvider.GetRequiredService<OdooAPIService>();

    // 2. Lấy đối tượng ViindooDBConfig đã đăng ký Singleton ra để chuẩn bị cập nhật thông tin
    var configInstance = scope.ServiceProvider.GetRequiredService<ViindooDBConfig>();

    try
    {
        // 3. Gọi hàm LoginAsync và hứng kết quả
        var processResult = await odooAPIService.LoginAsync();

        // 4. Gán thông tin đăng nhập từ kết quả trả về vào đối tượng configInstance
        // (Thay thế 'Token', 'SessionId' hoặc các thuộc tính bằng tên thuộc tính thực tế trong class ViindooDBConfig của bạn)
        if (processResult != null)
        {
            configInstance.SessionID = processResult.DataType; // Ví dụ: configInstance.SessionID = processResult.SessionId;
            configInstance.UserID = processResult.UserID; // Ví dụ: configInstance.UserID = processResult.UserId;
            // Ví dụ: configInstance.Token = processResult.Token;
            // Ví dụ: configInstance.IsLoggedIn = true;

            Console.WriteLine("Đăng nhập Odoo thành công và đã cập nhật thông tin vào ViindooDBConfig.");
        }
    }
    catch (Exception ex)
    {
        // Xử lý lỗi nếu gọi hàm login thất bại lúc khởi động
        Console.WriteLine($"Lỗi khi đăng nhập Odoo: {ex.Message}");
    }
}
// -----------------------------------

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
