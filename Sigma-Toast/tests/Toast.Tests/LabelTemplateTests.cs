using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Toast.Tests;

/// <summary>Màn Mẫu tem: thêm / sửa / trùng / xoá / xem trước qua API, chạy trên SQLite tạm.</summary>
public class LabelTemplateTests(ToastFactory factory) : IClassFixture<ToastFactory>
{
    const string Zpl = "^XA^PW2362^LL3543^FO100,100^A0N,90,91^FD{PART_NUMBER}^FS^FO100,300^A0N,90,91^FD{PO}^FS^XZ";

    [Fact]
    public async Task Create_update_duplicate_delete()
    {
        var (client, token) = await LoginAsync();
        var body = new { defaultCode = "PN-1", type = "Box", operation = "Toast", size = "4x6", dpi = 600, zplData = Zpl, variables = Array.Empty<object>() };

        var created = await PostAsync(client, token, "/toast/admin/label-template/api", body);
        Assert.True(created.GetProperty("success").GetBoolean(), created.ToString());
        int id = created.GetProperty("id").GetInt32();

        var dup = await PostAsync(client, token, "/toast/admin/label-template/api", body);
        Assert.False(dup.GetProperty("success").GetBoolean());
        Assert.Contains("đã tồn tại", dup.GetProperty("error").GetString());

        var updated = await PostAsync(client, token, $"/toast/admin/label-template/api/{id}", body with { size = "4x4" });
        Assert.True(updated.GetProperty("success").GetBoolean(), updated.ToString());

        var one = await client.GetFromJsonAsync<JsonElement>($"/toast/admin/label-template/api/{id}");
        Assert.Equal("4x4", one.GetProperty("data").GetProperty("size").GetString());
        Assert.Equal(Zpl, one.GetProperty("data").GetProperty("zplData").GetString());

        var list = await client.GetFromJsonAsync<JsonElement>("/toast/admin/label-template/api");
        Assert.Contains(list.GetProperty("data").EnumerateArray(), r => r.GetProperty("id").GetInt32() == id);

        var deleted = await PostAsync(client, token, $"/toast/admin/label-template/api/{id}/delete", new { });
        Assert.True(deleted.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Invalid_size_is_rejected()
    {
        var (client, token) = await LoginAsync();
        var res = await PostAsync(client, token, "/toast/admin/label-template/api",
            new { defaultCode = "PN-2", type = "Box", operation = "Toast", size = "9x9", dpi = 600, zplData = Zpl });
        Assert.Equal("Size không hợp lệ.", res.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Variables_follow_zpl_and_are_saved()
    {
        var (client, token) = await LoginAsync();
        // Gửi thừa biến OLD (không có trong ZPL), viết thường part_number, PO để trống
        var res = await PostAsync(client, token, "/toast/admin/label-template/api", new
        {
            defaultCode = "PN-VAR", type = "Box", operation = "Toast", size = "4x6", dpi = 203, zplData = Zpl,
            variables = new[] { new { key = "OLD", value = "x" }, new { key = "part_number", value = " 7100406577 " }, new { key = "PO", value = "" } }
        });
        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());

        var one = await client.GetFromJsonAsync<JsonElement>($"/toast/admin/label-template/api/{res.GetProperty("id").GetInt32()}");
        var data = one.GetProperty("data");
        Assert.Equal(203, data.GetProperty("dpi").GetInt32());
        var vars = data.GetProperty("variables").EnumerateArray().Select(v => (v.GetProperty("key").GetString(), v.GetProperty("value").GetString())).ToList();
        Assert.Equal([("PART_NUMBER", "7100406577"), ("PO", "")], vars);
    }

    [Fact]
    public async Task Variable_value_with_zpl_control_char_is_rejected()
    {
        var (client, token) = await LoginAsync();
        var res = await PostAsync(client, token, "/toast/admin/label-template/api", new
        {
            defaultCode = "PN-BAD", type = "Box", operation = "Toast", size = "4x6", dpi = 600, zplData = Zpl,
            variables = new[] { new { key = "PO", value = "12^34" } }
        });
        Assert.False(res.GetProperty("success").GetBoolean());
        Assert.Contains("{PO}", res.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Locked_template_cannot_be_updated_or_deleted_until_unlocked()
    {
        var (client, token) = await LoginAsync();
        var body = new { defaultCode = "PN-LOCK", type = "Box", operation = "Toast", size = "4x6", dpi = 600, zplData = Zpl };
        int id = (await PostAsync(client, token, "/toast/admin/label-template/api", body)).GetProperty("id").GetInt32();

        Assert.True((await PostAsync(client, token, $"/toast/admin/label-template/api/{id}/lock", new { locked = true })).GetProperty("success").GetBoolean());
        var update = await PostAsync(client, token, $"/toast/admin/label-template/api/{id}", body with { size = "4x4" });
        Assert.False(update.GetProperty("success").GetBoolean());
        Assert.Contains("khoá", update.GetProperty("error").GetString());
        Assert.False((await PostAsync(client, token, $"/toast/admin/label-template/api/{id}/delete", new { })).GetProperty("success").GetBoolean());

        Assert.True((await PostAsync(client, token, $"/toast/admin/label-template/api/{id}/lock", new { locked = false })).GetProperty("success").GetBoolean());
        Assert.True((await PostAsync(client, token, $"/toast/admin/label-template/api/{id}", body with { size = "4x4" })).GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Preview_renders_png()
    {
        var (client, token) = await LoginAsync();
        var res = await PostAsync(client, token, "/toast/admin/label-template/api/preview", new { zplData = Zpl, size = "4x6", dpmm = 8, variables = new[] { new { key = "PART_NUMBER", value = "7100406577" } } });
        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());
        Assert.Equal(1, res.GetProperty("totalLabels").GetInt32());
    }

    [Fact]
    public void Preview_uses_embedded_fonts()
    {
        var fonts = Toast.Web.Features.LabelTemplate.ZplPreviewService.DrawerOptions.FontManager;
        Assert.Equal("TeX Gyre Heros Cn", fonts.FontLoader("0").FamilyName);
        Assert.Equal("DejaVu Sans Mono", fonts.FontLoader("D").FamilyName);
    }

    [Fact]
    public async Task Api_without_login_returns_401()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Requested-With", "XMLHttpRequest");
        var res = await client.GetAsync("/toast/admin/label-template/api");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    async Task<(HttpClient client, string token)> LoginAsync()
    {
        var result = await factory.LoginAsync();
        Assert.Contains("Danh sách mẫu tem", await result.client.GetStringAsync("/toast/admin/label-template"));
        return result;
    }

    static Task<JsonElement> PostAsync(HttpClient client, string token, string url, object body) =>
        ToastFactory.PostAsync(client, token, url, body);
}
