using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Toast.Tests;

/// <summary>Chạy app trong test với file SQLite riêng (thư mục tạm), không đụng toast-dev.db của máy dev.</summary>
public sealed class ToastFactory : WebApplicationFactory<Program>
{
    public string DbPath { get; } = Path.Combine(Path.GetTempPath(), $"toast-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:ToastSqlite", $"Data Source={DbPath};Pooling=False");
        builder.UseSetting("Toast:AdminUsers:0:UserName", "admin");
        builder.UseSetting("Toast:AdminUsers:0:Password", "test-pass");
        // PIN leader cố định cho test, không phụ thuộc appsettings.json ở máy (có thể đang tắt PIN)
        builder.UseSetting("Toast:RequireLeaderPin", "true");
        builder.UseSetting("Toast:LeaderPin", "1234");
        // Không phụ thuộc appsettings.json ở máy: trạm chỉ gửi khi đủ box, tắt tự kết thúc phiên (tác vụ nền không đụng phiên của test)
        builder.UseSetting("Toast:ScanSyncEvery", "0");
        builder.UseSetting("Toast:AutoEndInactiveHours", "0");
        // Khoá Data Protection của test ở thư mục tạm (không ghi vào thư mục project / ProgramData)
        builder.UseSetting("Toast:DataProtectionKeysDir", Path.Combine(Path.GetTempPath(), "toast-test-keys"));
    }

    // Đăng nhập, trả về client có cookie + antiforgery token lấy từ trang page
    public async Task<(HttpClient client, string token)> LoginAsync(string page = "/toast/admin/label-template")
    {
        var client = CreateClient();
        var loginPage = await client.GetStringAsync("/toast/admin/account/login");
        var login = await client.PostAsync("/toast/admin/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["userName"] = "admin",
            ["password"] = "test-pass",
            ["__RequestVerificationToken"] = Token(loginPage)
        }));
        login.EnsureSuccessStatusCode();
        return (client, Token(await client.GetStringAsync(page)));
    }

    public static string Token(string html) =>
        Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

    public static async Task<JsonElement> PostAsync(HttpClient client, string token, string url, object body)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        req.Headers.Add("RequestVerificationToken", token);
        var res = await client.SendAsync(req);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        File.Delete(DbPath);
        File.Delete(DbPath + ".schema");
        foreach (string backup in Directory.GetFiles(Path.GetDirectoryName(DbPath)!, Path.GetFileName(DbPath) + ".bak-*"))
            File.Delete(backup);
    }
}
