using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Toast.Web.Data;

namespace Toast.Tests;

/// <summary>Màn máy in /toast/admin/printer: thêm / sửa / xoá, mã không trùng, máy đã in box không xoá được, máy ngừng dùng bị ẩn.</summary>
public class PrinterTests(ToastFactory factory) : IClassFixture<ToastFactory>
{
    const string Api = "/toast/admin/printer/api";

    [Fact]
    public async Task Crud_with_rules()
    {
        var (client, token) = await factory.LoginAsync("/toast/admin/printer");

        var created = await ToastFactory.PostAsync(client, token, Api,
            new { code = " l1-box ", name = "Zebra line 1", ip = "192.168.1.50", port = 9100, dpi = 203, location = "Line 1", isActive = true });
        Assert.True(created.GetProperty("success").GetBoolean(), created.ToString());
        int id = created.GetProperty("id").GetInt32();

        var one = (await client.GetFromJsonAsync<JsonElement>($"{Api}/{id}")).GetProperty("data");
        Assert.Equal("L1-BOX", one.GetProperty("code").GetString()); // viết hoa, bỏ khoảng trắng
        Assert.Equal(203, one.GetProperty("dpi").GetInt32());

        var dup = await ToastFactory.PostAsync(client, token, Api, new { code = "L1-BOX", name = "Khác", ip = "192.168.1.51", port = 9100 });
        Assert.Contains("đã dùng", dup.GetProperty("error").GetString());

        var badIp = await ToastFactory.PostAsync(client, token, Api, new { code = "X1", name = "X", ip = "192.168.1", port = 9100 });
        Assert.Contains("IP", badIp.GetProperty("error").GetString());
        var badDpi = await ToastFactory.PostAsync(client, token, Api, new { code = "X2", name = "X", ip = "10.0.0.1", port = 9100, dpi = 150 });
        Assert.Contains("DPI", badDpi.GetProperty("error").GetString());

        var updated = await ToastFactory.PostAsync(client, token, $"{Api}/{id}",
            new { code = "L1-BOX", name = "Zebra line 1", ip = "192.168.1.60", port = 6101, dpi = (int?)null, location = "", isActive = true });
        Assert.True(updated.GetProperty("success").GetBoolean(), updated.ToString());
        one = (await client.GetFromJsonAsync<JsonElement>($"{Api}/{id}")).GetProperty("data");
        Assert.Equal("192.168.1.60", one.GetProperty("ip").GetString());
        Assert.Equal(JsonValueKind.Null, one.GetProperty("location").ValueKind);

        Assert.True((await ToastFactory.PostAsync(client, token, $"{Api}/{id}/delete", new { })).GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Printer_used_by_box_cannot_be_deleted_and_inactive_is_hidden()
    {
        var (client, token) = await factory.LoginAsync("/toast/admin/printer");
        int id = (await ToastFactory.PostAsync(client, token, Api, new { code = "USED-1", name = "Đã dùng", ip = "127.0.0.1", port = 9100, isActive = true }))
            .GetProperty("id").GetInt32();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
            db.Packages.Add(new Toast.Web.Features.Package.Package
            {
                WoName = "NM/MO/PRN01", ProductId = 1, DefaultCode = "PN", PackageNo = 1, Qty = 1, Capacity = 1,
                Status = "Closed", PrinterId = id, CreatedAt = DateTime.Now
            });
            await db.SaveChangesAsync();
        }

        var delete = await ToastFactory.PostAsync(client, token, $"{Api}/{id}/delete", new { });
        Assert.Contains("Ngừng dùng", delete.GetProperty("error").GetString());

        // Ngừng dùng: không còn ở danh sách máy in của trạm quét
        await ToastFactory.PostAsync(client, token, $"{Api}/{id}", new { code = "USED-1", name = "Đã dùng", ip = "127.0.0.1", port = 9100, isActive = false });
        var station = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/toast/api/printers");
        Assert.DoesNotContain(station.GetProperty("printers").EnumerateArray(), p => p.GetProperty("id").GetInt32() == id);
    }
}
