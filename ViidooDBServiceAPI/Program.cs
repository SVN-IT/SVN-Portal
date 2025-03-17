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

builder.Services.AddSingleton(viindooDBConfig);
builder.Services.AddSingleton(sVNDBConfig);
builder.Services.AddSingleton<DBService>();
builder.Services.AddSingleton<OdooRpcDBService>();

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
