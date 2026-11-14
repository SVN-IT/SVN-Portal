using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Toast.Web.Features.Viindoo;

namespace Toast.Tests;

/// <summary>Màn In tem: biến gợi ý, chặn in khi thiếu biến, gửi TCP tới máy in giả, đọc WO.</summary>
public class LabelPrintTests(ToastFactory factory) : IClassFixture<ToastFactory>
{
    const string Zpl = "^XA^FO50,50^A0N,40,40^FD{PART_NUMBER}^FS^FO50,120^FD{PO}^FS^FO50,190^FD{DATE}^FS^XZ";

    async Task<(HttpClient client, string token, int id)> CreateTemplateAsync(string defaultCode)
    {
        var (client, token) = await factory.LoginAsync();
        var res = await ToastFactory.PostAsync(client, token, "/toast/admin/label-template/api", new
        {
            defaultCode, type = "Box", operation = "Toast", size = "4x6", dpi = 203, zplData = Zpl,
            variables = new[] { new { key = "PART_NUMBER", value = "7100406577" } }
        });
        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());
        return (client, token, res.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Print_page_lists_active_printers_only()
    {
        var (client, token) = await factory.LoginAsync("/toast/admin/printer");
        var on = await ToastFactory.PostAsync(client, token, "/toast/admin/printer/api", new { code = "LP-ON", name = "On", ip = "127.0.0.1", port = 9100, isActive = true });
        var off = await ToastFactory.PostAsync(client, token, "/toast/admin/printer/api", new { code = "LP-OFF", name = "Off", ip = "127.0.0.1", port = 9100, isActive = false });

        var html = await client.GetStringAsync("/toast/admin/print");
        Assert.Contains($"value=\"{on.GetProperty("id").GetInt32()}\"", html);
        Assert.DoesNotContain($"value=\"{off.GetProperty("id").GetInt32()}\"", html);
    }

    [Fact]
    public async Task Template_search_ignores_case()
    {
        var (client, _, id) = await CreateTemplateAsync("HW-SEARCH-01");
        foreach (string q in new[] { "hw-search", "HW-SEARCH", "Search-01" })
        {
            var res = await client.GetFromJsonAsync<JsonElement>("/toast/admin/print/api/templates?q=" + Uri.EscapeDataString(q));
            Assert.Contains(res.GetProperty("templates").EnumerateArray(), t => t.GetProperty("id").GetInt32() == id);
        }
    }

    [Fact]
    public async Task Resolve_uses_saved_value_then_system_then_wo()
    {
        var (client, token, id) = await CreateTemplateAsync("PN-RES");
        var res = await ToastFactory.PostAsync(client, token, "/toast/admin/print/api/resolve", new { templateId = id });
        var vars = res.GetProperty("variables").EnumerateArray().ToDictionary(v => v.GetProperty("key").GetString()!);

        Assert.Equal("template", vars["PART_NUMBER"].GetProperty("source").GetString());
        Assert.Equal("7100406577", vars["PART_NUMBER"].GetProperty("value").GetString());
        Assert.Equal("system", vars["DATE"].GetProperty("source").GetString());
        Assert.Equal(DateTime.Now.ToString("dd/MM/yyyy"), vars["DATE"].GetProperty("value").GetString());
        Assert.Equal("wo", vars["PO"].GetProperty("source").GetString()); // PO tự lấy từ đơn bán của WO; in không theo WO thì trống
        Assert.Equal("", vars["PO"].GetProperty("value").GetString());
    }

    [Fact]
    public async Task Render_blocks_missing_variable_and_fills_when_complete()
    {
        var (client, token, id) = await CreateTemplateAsync("PN-RENDER");

        var missing = await ToastFactory.PostAsync(client, token, "/toast/admin/print/api/render",
            new { templateId = id, values = new Dictionary<string, string> { ["PART_NUMBER"] = "7100406577", ["DATE"] = "30/10/2026" } });
        Assert.False(missing.GetProperty("success").GetBoolean());
        Assert.Contains("{PO}", missing.GetProperty("error").GetString());

        var ok = await ToastFactory.PostAsync(client, token, "/toast/admin/print/api/render",
            new { templateId = id, values = new Dictionary<string, string> { ["PART_NUMBER"] = "7100406577", ["po"] = "123456", ["DATE"] = "30/10/2026" } });
        Assert.True(ok.GetProperty("success").GetBoolean(), ok.ToString());
        Assert.Equal("^XA^FO50,50^A0N,40,40^FD7100406577^FS^FO50,120^FD123456^FS^FO50,190^FD30/10/2026^FS^XZ", ok.GetProperty("zpl").GetString());
    }

    [Fact]
    public async Task Print_sends_filled_zpl_to_tcp_printer()
    {
        var (client, token, id) = await CreateTemplateAsync("PN-TCP");

        // Máy in giả: TcpListener cục bộ, nhận hết dữ liệu gửi tới
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = Task.Run(async () =>
        {
            using var conn = await listener.AcceptTcpClientAsync();
            using var ms = new MemoryStream();
            await conn.GetStream().CopyToAsync(ms);
            return Encoding.UTF8.GetString(ms.ToArray());
        });

        var res = await ToastFactory.PostAsync(client, token, "/toast/admin/print/api/print", new
        {
            templateId = id, ip = "127.0.0.1", port, copies = 2,
            values = new Dictionary<string, string> { ["PART_NUMBER"] = "7100406577", ["PO"] = "123456", ["DATE"] = "30/10/2026" }
        });
        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());

        string data = await received.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
        Assert.Equal(2, data.Split("^XA").Length - 1);
        Assert.Contains("^FD123456^FS", data);
    }

    [Fact]
    public async Task Print_to_closed_port_reports_error()
    {
        var (client, token, id) = await CreateTemplateAsync("PN-OFF");
        // Lấy 1 port trống rồi đóng lại: không có máy in nào nghe ở đó
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        var res = await ToastFactory.PostAsync(client, token, "/toast/admin/print/api/print", new
        {
            templateId = id, ip = "127.0.0.1", port, copies = 1,
            values = new Dictionary<string, string> { ["PART_NUMBER"] = "x", ["PO"] = "1", ["DATE"] = "d" }
        });
        Assert.False(res.GetProperty("success").GetBoolean());
        Assert.Contains("Không kết nối được máy in", res.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Work_order_lookup_reads_mrp_production()
    {
        string? askedName = null;
        using var app = ViindooStub.Use(factory, (model, kwargs) =>
        {
            Assert.Equal("mrp.production", model);
            askedName = ViindooStub.DomainValue(kwargs);
            return "[" + ViindooStub.WoRow("NM/MO/06246", 1428, "7100406577", qty: 8448.0m, origin: "") + "]";
        });
        var rpc = app.Services.GetRequiredService<ViindooRpc>();

        var (wo, error) = await rpc.GetWorkOrderAsync("06246");

        Assert.Null(error);
        Assert.Equal("NM/MO/06246", askedName); // thêm tiền tố, tìm đúng tên
        Assert.Equal("1428", wo!["product_id"]);
        Assert.Equal("7100406577", wo["product_code"]);
        Assert.Equal("Sản phẩm 1428", wo["product_name"]);
        Assert.Equal("8448", wo["product_qty"]);
        Assert.Equal("", wo["origin"]);
    }
    [Fact]
    public async Task Wo_lookup_adds_sale_order_data_and_allows_done_wo()
    {
        // WO đã hoàn thành (màn In tem vẫn mở được), origin = đơn bán có 1 PO / 1 Lot, sản phẩm có mã khách
        using var app = ViindooStub.Use(factory, (model, kwargs) => model switch
        {
            "mrp.production" => "[" + ViindooStub.WoRow(ViindooStub.DomainValue(kwargs), 4601, "LP-4601", state: "done", origin: "S04601") + "]",
            "product.product" => """[{"x_custcode":"HW0151","x_quantity_per_code":10}]""",
            "sale.order" => """[{"name":"S04601","x_ERP_PO":"PO9","x_LotID":"0001-0-0626","order_line":[100]}]""",
            "sale.order.line" => """[{"id":100,"product_id":[4601,"x"]}]""",
            _ => "[]"
        });
        var admin = app.CreateClient();
        var loginPage = await admin.GetStringAsync("/toast/admin/account/login");
        await admin.PostAsync("/toast/admin/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["userName"] = "admin", ["password"] = "test-pass", ["__RequestVerificationToken"] = ToastFactory.Token(loginPage)
        }));
        string token = ToastFactory.Token(await admin.GetStringAsync("/toast/admin/print"));
        var tpl = await ToastFactory.PostAsync(admin, token, "/toast/admin/label-template/api", new
        {
            defaultCode = "LP-4601", type = "Box", operation = "Toast", size = "4x6", dpi = 203, zplData = Zpl,
            variables = new[] { new { key = "PART_NUMBER", value = "" } }
        });
        Assert.True(tpl.GetProperty("success").GetBoolean(), tpl.ToString());

        var res = await admin.GetFromJsonAsync<JsonElement>("/toast/admin/print/api/wo?wo=4601");

        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());
        var wo = res.GetProperty("wo");
        Assert.Equal("NM/MO/4601", wo.GetProperty("name").GetString());
        Assert.Equal("PO9", wo.GetProperty("po").GetString());
        Assert.Equal("0001-0-0626", wo.GetProperty("lot_id").GetString());
        Assert.Equal("HW0151", wo.GetProperty("customer_part_number").GetString());
        Assert.Contains(res.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains("đã hoàn thành"));
        Assert.Single(res.GetProperty("templates").EnumerateArray());

        // Mẫu chưa có giá trị -> biến lấy từ WO
        var resolved = await ToastFactory.PostAsync(admin, token, "/toast/admin/print/api/resolve",
            new { templateId = tpl.GetProperty("id").GetInt32(), wo = JsonSerializer.Deserialize<Dictionary<string, string>>(wo.GetRawText()) });
        var vars = resolved.GetProperty("variables").EnumerateArray().ToDictionary(v => v.GetProperty("key").GetString()!, v => v.GetProperty("value").GetString());
        Assert.Equal("HW0151", vars["PART_NUMBER"]);
        Assert.Equal("PO9", vars["PO"]);
    }
}
