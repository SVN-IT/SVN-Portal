using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Toast.Web.Features.Viindoo;

namespace Toast.Tests;

/// <summary>Màn Sản phẩm (Toast_Product): CRUD, kiểm tra dữ liệu, lấy từ Viindoo qua HTTP giả.</summary>
public class ProductTests(ToastFactory factory) : IClassFixture<ToastFactory>
{
    const string Api = "/toast/admin/product/api";

    static object Body(int viindooId, string part, string barcode = "068060649386", int pcs = 4,
        string? outer = "Carton", int? boxPerOuter = 10, int? vqty = 4) => new
    {
        productId = viindooId, defaultCode = part, name = "Tên " + part, barcode,
        pcsPerBox = pcs, outerType = outer, boxPerOuter, viindooQtyPerCode = vqty
    };

    [Fact]
    public async Task Create_update_delete()
    {
        var (client, token) = await factory.LoginAsync("/toast/admin/product");
        var created = await ToastFactory.PostAsync(client, token, Api, Body(951, "FG-TRO-CE-0002-00"));
        Assert.True(created.GetProperty("success").GetBoolean(), created.ToString());
        int id = created.GetProperty("id").GetInt32();

        var updated = await ToastFactory.PostAsync(client, token, $"{Api}/{id}", Body(951, "FG-TRO-CE-0002-00", pcs: 6, outer: "Plate", boxPerOuter: 40));
        Assert.True(updated.GetProperty("success").GetBoolean(), updated.ToString());

        var one = (await client.GetFromJsonAsync<JsonElement>($"{Api}/{id}")).GetProperty("data");
        Assert.Equal(6, one.GetProperty("pcsPerBox").GetInt32());
        Assert.Equal("Plate", one.GetProperty("outerType").GetString());
        Assert.Equal(40, one.GetProperty("boxPerOuter").GetInt32());

        Assert.True((await ToastFactory.PostAsync(client, token, $"{Api}/{id}/delete", new { })).GetProperty("success").GetBoolean());
        Assert.False((await client.GetFromJsonAsync<JsonElement>($"{Api}/{id}")).GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Same_viindoo_product_is_rejected_but_shared_barcode_is_allowed()
    {
        var (client, token) = await factory.LoginAsync("/toast/admin/product");
        Assert.True((await ToastFactory.PostAsync(client, token, Api, Body(1001, "PN-EN", barcode: "SHARED-1"))).GetProperty("success").GetBoolean());

        var dup = await ToastFactory.PostAsync(client, token, Api, Body(1001, "PN-OTHER"));
        Assert.False(dup.GetProperty("success").GetBoolean());
        Assert.Contains("1001", dup.GetProperty("error").GetString());

        // Bản EN / EU là 2 sản phẩm khác nhau nhưng dùng chung barcode (có trong dữ liệu Twist)
        var shared = await ToastFactory.PostAsync(client, token, Api, Body(1002, "PN-EU", barcode: "SHARED-1"));
        Assert.True(shared.GetProperty("success").GetBoolean(), shared.ToString());
    }

    [Theory]
    [InlineData(0, "PN", "B", 4, "Carton", 10, "Viindoo product ID")]
    [InlineData(5, "", "B", 4, "Carton", 10, "mã sản phẩm")]
    [InlineData(5, "PN", "", 4, "Carton", 10, "barcode")]
    [InlineData(5, "PN", "B", 0, "Carton", 10, "pcs / box")]
    [InlineData(5, "PN", "B", 4, "Pallet", 10, "Carton hoặc Plate")]
    [InlineData(5, "PN", "B", 4, "Carton", 0, "box / Carton")]
    public async Task Invalid_input_is_rejected(int vid, string part, string barcode, int pcs, string outer, int boxPerOuter, string expected)
    {
        var (client, token) = await factory.LoginAsync("/toast/admin/product");
        var res = await ToastFactory.PostAsync(client, token, Api, Body(vid, part, barcode, pcs, outer, boxPerOuter));
        Assert.False(res.GetProperty("success").GetBoolean());
        Assert.Contains(expected, res.GetProperty("error").GetString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_outer_type_clears_box_per_outer()
    {
        var (client, token) = await factory.LoginAsync("/toast/admin/product");
        var res = await ToastFactory.PostAsync(client, token, Api, Body(2001, "PN-BOX-ONLY", outer: null, boxPerOuter: 99));
        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());
        var one = (await client.GetFromJsonAsync<JsonElement>($"{Api}/{res.GetProperty("id").GetInt32()}")).GetProperty("data");
        Assert.Equal(JsonValueKind.Null, one.GetProperty("boxPerOuter").ValueKind);
    }

    // Viindoo giả: product.product theo default_code, count dòng giống nhau. qtyJson = giá trị x_quantity_per_code trả về
    static WebApplicationFactory<Program> WithViindoo(ToastFactory factory, string qtyJson = "4", int count = 1) =>
        ViindooStub.Use(factory, (model, kwargs) =>
        {
            Assert.Equal("product.product", model);
            string row = """{"id":951,"default_code":"FG-TRO-CE-0002-00","name":"TG3 Case Dark Grey","x_quantity_per_code":""" + qtyJson + "}";
            return "[" + string.Join(",", Enumerable.Repeat(row, count)) + "]";
        });

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

    [Theory]
    [InlineData("4", 4)]
    [InlineData("4.0", 4)]
    [InlineData("0", null)]
    [InlineData("false", null)]
    public async Task Fetch_by_code_fills_id_name_and_qty(string qtyJson, int? expected)
    {
        using var app = WithViindoo(factory, qtyJson);
        var client = await LoginAsync(app);

        var res = await client.GetFromJsonAsync<JsonElement>($"{Api}/viindoo?code=FG-TRO-CE-0002-00");

        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());
        Assert.Equal(951, res.GetProperty("productId").GetInt32());
        Assert.Equal("FG-TRO-CE-0002-00", res.GetProperty("defaultCode").GetString());
        Assert.Equal("TG3 Case Dark Grey", res.GetProperty("name").GetString());
        var qty = res.GetProperty("viindooQtyPerCode");
        Assert.Equal(expected, qty.ValueKind == JsonValueKind.Null ? null : qty.GetInt32());
    }

    [Theory]
    [InlineData("done")]
    [InlineData("confirmed")]
    public async Task Code_from_wo_works_also_for_done_wo(string state)
    {
        using var app = ViindooStub.Use(factory, (model, kwargs) =>
        {
            Assert.Equal("mrp.production", model);
            return "[" + ViindooStub.WoRow(ViindooStub.DomainValue(kwargs), 951, "FG-TRO-CE-0002-00", state) + "]";
        });
        var client = await LoginAsync(app);

        var res = await client.GetFromJsonAsync<JsonElement>($"{Api}/viindoo-wo?wo=06229");

        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());
        Assert.Equal("NM/MO/06229", res.GetProperty("wo").GetString());
        Assert.Equal("FG-TRO-CE-0002-00", res.GetProperty("defaultCode").GetString());
    }

    [Theory]
    [InlineData(0, "Không tìm thấy sản phẩm có mã")]
    [InlineData(2, "nhiều sản phẩm cùng mã")]
    public async Task Fetch_by_code_reports_missing_or_duplicate(int count, string error)
    {
        using var app = WithViindoo(factory, count: count);
        var client = await LoginAsync(app);
        var res = await client.GetFromJsonAsync<JsonElement>($"{Api}/viindoo?code=KHONG-TON-TAI");
        Assert.False(res.GetProperty("success").GetBoolean());
        Assert.Contains(error, res.GetProperty("error").GetString());
    }
}
