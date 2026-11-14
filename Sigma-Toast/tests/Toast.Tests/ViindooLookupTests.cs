using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Toast.Web.Features.Viindoo;

namespace Toast.Tests;

/// <summary>Phân loại dự án theo nhóm sản phẩm + 2 màn tra cứu Viindoo (lệnh sản xuất, sản phẩm).</summary>
public class ViindooLookupTests(ToastFactory factory) : IClassFixture<ToastFactory>
{
    [Theory]
    [InlineData("Toast - F/G", "Toast", "F/G")]
    [InlineData("Sandford- WIP", "Sandford", "WIP")]
    [InlineData("Sakura- Raw MAT", "Sakura", "Raw MAT")]
    [InlineData("Walter Inbox - WIP", "Walter Inbox", "WIP")]
    [InlineData("Nguyên vật liệu TOAST", "TOAST", "Raw MAT")]
    [InlineData("Bán thành phẩm POP", "POP", "WIP")]
    [InlineData("Thành phẩm Injection Toast", "Injection Toast", "F/G")]
    [InlineData("Twist FG", "Twist", "F/G")]
    [InlineData("All", "All", "")]
    public void Category_name_splits_into_project_and_kind(string name, string project, string kind)
    {
        Assert.Equal((project, kind), ViindooCatalog.Split(name));
    }

    // Viindoo giả: 2 nhóm (Toast - F/G id 28, Walter - F/G id 33), 1 WO Toast (Dock), sản phẩm 924
    static string Respond(string model, JsonElement kwargs)
    {
        var domain = kwargs.GetProperty("domain");
        switch (model)
        {
            case "product.category":
                return """[{"id":28,"name":"Toast - F/G","complete_name":"Toast - F/G"},{"id":33,"name":"Walter - F/G","complete_name":"Walter - F/G"}]""";
            case "mrp.production" when domain.GetArrayLength() > 0 && domain[0].ValueKind == JsonValueKind.Array && domain[0][0].GetString() == "state":
                // Danh sách: lọc dự án Toast -> categ in [28]
                Assert.Contains(domain.EnumerateArray(), d => d.ValueKind == JsonValueKind.Array && d[0].GetString() == "product_id.categ_id" && d[2][0].GetInt32() == 28);
                return """[{"id":1,"name":"NM/MO/06347","state":"confirmed","product_id":[924,"[FG-TRO-CE-0001-00] FG DCT3 Dock"],"product_qty":300,"qty_produced":0,"date_deadline":false,"date_planned_start":false,"origin":"S00693","backorder_sequence":0}]""";
            case "product.product":
                return """[{"id":924,"default_code":"FG-TRO-CE-0001-00","name":"FG DCT3 Dock","categ_id":[28,"Toast - F/G"],"x_custcode":"HW0032","x_quantity_per_code":0,"tracking":"serial","uom_id":[27,"PCS"],"product_tmpl_id":[900,"x"],"active":true}]""";
            default:
                return "[]";
        }
    }

    async Task<HttpClient> LoginAsync(WebApplicationFactory<Program> app)
    {
        var client = app.CreateClient();
        var loginPage = await client.GetStringAsync("/toast/admin/account/login");
        await client.PostAsync("/toast/admin/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["userName"] = "admin", ["password"] = "test-pass", ["__RequestVerificationToken"] = ToastFactory.Token(loginPage)
        }));
        return client;
    }

    [Fact]
    public async Task Work_order_list_filters_by_project_and_classifies()
    {
        using var app = ViindooStub.Use(factory, Respond);
        var client = await LoginAsync(app);

        var res = await client.GetFromJsonAsync<JsonElement>("/toast/admin/work-order/api?project=Toast");

        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());
        var item = res.GetProperty("data").GetProperty("items")[0];
        Assert.Equal("NM/MO/06347", item.GetProperty("name").GetString());
        var c = item.GetProperty("class");
        Assert.Equal("Toast", c.GetProperty("project").GetString());
        Assert.Equal("F/G", c.GetProperty("kind").GetString());
        Assert.Equal("Dock", c.GetProperty("subType").GetString());
        Assert.Equal("Toast, Inc.", c.GetProperty("customer").GetString());

        var projects = await client.GetFromJsonAsync<JsonElement>("/toast/admin/work-order/api/projects");
        Assert.Equal(["Toast", "Walter"], projects.GetProperty("projects").EnumerateArray().Select(p => p.GetProperty("name").GetString()).ToArray());
    }

    [Fact]
    public async Task Child_work_order_shows_parent_and_sale_order()
    {
        // NM/MO/04696 (bán thành phẩm) có origin = lệnh cha NM/MO/04695, lệnh cha có origin = lệnh ông NM/MO/04690 -> S00650
        using var app = ViindooStub.Use(factory, (model, kwargs) =>
        {
            var domain = kwargs.GetProperty("domain");
            // Lần lệnh cha: ["|", name in [...], procurement_group_id.name in [...]]. Lệnh cha 04695 đã tách backorder: chỉ còn 04695-001
            if (model == "mrp.production" && domain[0].ValueKind == JsonValueKind.String)
            {
                var names = domain[1][2].EnumerateArray().Select(x => x.GetString()).ToList();
                return JsonSerializer.Serialize(names.Select(n => n == "NM/MO/04695"
                    ? new { name = "NM/MO/04695-001", origin = "NM/MO/04690", procurement_group_id = new object[] { 1, "NM/MO/04695" } }
                    : new { name = n!, origin = "S00650", procurement_group_id = new object[] { 2, n! } }));
            }
            if (model == "mrp.production")
                return """[{"id":2,"name":"NM/MO/04696","state":"confirmed","product_id":[925,"[WSS03-00290] DCT3 Dock Assembly"],"product_qty":300,"qty_produced":0,"date_deadline":false,"date_planned_start":false,"origin":"NM/MO/04695","backorder_sequence":0}]""";
            return Respond(model, kwargs);
        });
        var client = await LoginAsync(app);

        var item = (await client.GetFromJsonAsync<JsonElement>("/toast/admin/work-order/api")).GetProperty("data").GetProperty("items")[0];

        Assert.Equal("NM/MO/04695", item.GetProperty("parentWo").GetString());
        Assert.Equal("S00650", item.GetProperty("saleOrder").GetString());
    }

    // Nhóm NM/MO/03975 đã tách backorder: -001..-261 done, -262 đang mở (allDone = true thì không còn lệnh mở)
    static Func<string, JsonElement, string> BackorderResponder(bool allDone) => (model, kwargs) =>
    {
        var domain = kwargs.GetProperty("domain");
        string field = domain[0][0].GetString()!;
        string WoRow(string name, string state) => ViindooStub.WoRow(name, 1341, "FG-TRO-CE-0007-00", state);
        if (model != "mrp.production") return "[]";
        if (field == "name") return domain[0][2].GetString() == "NM/MO/03975-262" ? "[" + WoRow("NM/MO/03975-262", "confirmed") + "]" : "[]";
        // procurement_group_id.name = NM/MO/03975 (+ state not in done/cancel nếu chỉ lấy lệnh mở)
        bool onlyOpen = domain.GetArrayLength() > 1;
        if (onlyOpen) return allDone ? "[]" : "[" + WoRow("NM/MO/03975-262", "confirmed") + "]";
        return "[" + string.Join(",", Enumerable.Range(1, 261).Reverse().Select(i => WoRow($"NM/MO/03975-{i:000}", "done"))) + "]";
    };

    [Fact]
    public async Task Base_name_of_split_work_order_opens_latest_open_backorder()
    {
        using var app = ViindooStub.Use(factory, BackorderResponder(allDone: false));
        var rpc = app.Services.GetRequiredService<ViindooRpc>();

        var (wo, error) = await rpc.GetWorkOrderAsync("03975");

        Assert.Null(error);
        Assert.Equal("NM/MO/03975-262", wo!["name"]);
        Assert.Equal("NM/MO/03975", wo["requested_name"]);
        // Gõ đúng tên lệnh thì vẫn ra đúng lệnh đó
        var (exact, _) = await rpc.GetWorkOrderAsync("03975-262");
        Assert.Equal("NM/MO/03975-262", exact!["requested_name"]);
    }

    [Fact]
    public async Task Base_name_with_all_backorders_done_is_reported()
    {
        using var app = ViindooStub.Use(factory, BackorderResponder(allDone: true));
        var rpc = app.Services.GetRequiredService<ViindooRpc>();

        var (wo, error) = await rpc.GetWorkOrderAsync("03975");
        Assert.Null(wo);
        Assert.Equal("Lệnh NM/MO/03975 đã hoàn thành hết (261 lệnh, mới nhất NM/MO/03975-261).", error);

        // Màn chỉ xem (allowClosed): lấy lệnh mới nhất
        var (latest, _) = await rpc.GetWorkOrderAsync("03975", allowClosed: true);
        Assert.Equal("NM/MO/03975-261", latest!["name"]);
    }

    [Theory]
    [InlineData("/toast/admin/work-order/api?sort=qty&dir=desc", "mrp.production", "product_qty desc, id desc")]
    [InlineData("/toast/admin/work-order/api?sort=deadline", "mrp.production", "date_deadline asc, id desc")]
    [InlineData("/toast/admin/work-order/api?sort=evil;drop", "mrp.production", "id desc")] // cột lạ: bỏ qua
    [InlineData("/toast/admin/viindoo-product/api?sort=customerCode&dir=desc", "product.product", "x_custcode desc, id")]
    [InlineData("/toast/admin/viindoo-product/api", "product.product", "default_code, id")]
    public async Task List_sort_is_sent_to_viindoo(string url, string model, string order)
    {
        string? sent = null;
        using var app = ViindooStub.Use(factory, (m, kwargs) =>
        {
            if (m == model && kwargs.TryGetProperty("order", out var o)) sent = o.GetString();
            return m == "product.category" ? Respond(m, kwargs) : "[]";
        });
        var client = await LoginAsync(app);

        var res = await client.GetFromJsonAsync<JsonElement>(url);

        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());
        Assert.Equal(order, sent);
    }

    [Fact]
    public async Task Product_detail_shows_class_and_toast_status()
    {
        using var app = ViindooStub.Use(factory, Respond);
        var client = await LoginAsync(app);

        var res = await client.GetFromJsonAsync<JsonElement>("/toast/admin/viindoo-product/api/924");

        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());
        var d = res.GetProperty("detail");
        Assert.Equal("HW0032", d.GetProperty("product").GetProperty("customerCode").GetString());
        Assert.Equal("Dock", d.GetProperty("product").GetProperty("class").GetProperty("subType").GetString());
        Assert.Equal(JsonValueKind.Null, d.GetProperty("toast").GetProperty("product").ValueKind); // chưa có quy cách
        Assert.Equal(JsonValueKind.Null, d.GetProperty("bom").ValueKind);
    }

    [Theory]
    [InlineData("/toast/admin/work-order/api")]
    [InlineData("/toast/admin/viindoo-product/api")]
    public async Task Lookup_requires_admin_login(string url)
    {
        using var app = ViindooStub.Use(factory, Respond);
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        Assert.False((await client.GetAsync(url)).IsSuccessStatusCode);
    }
}
