using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Toast.Tests;

/// <summary>
/// Màn nháp /toastv2: đọc WO + mã khách + PO / Lot (đơn bán) từ Viindoo giả, điền biến vào mẫu tem, báo biến thiếu. Chỉ xem.
/// </summary>
public class StationV2Tests(ToastFactory factory) : IClassFixture<ToastFactory>
{
    // Giống tem Box Toast thật: PART_NUMBER / PO / LOT_ID / QTY tự lấy, MODEL / PART / DESC lưu trong mẫu
    const string Zpl = "^XA^FD{PART_NUMBER}|{PO}|{LOT_ID}|{QTY}|{MODEL}|{PART}|{DESC}^FS^XZ";

    // Viindoo giả: WO con NM/MO/<wo> (origin = lệnh cha NM/MO/P<wo>, cha có origin = đơn bán S<wo>).
    // Đơn bán có PO / Lot nối dấu phẩy theo các dòng lineProducts. Sản phẩm có x_custcode HW0032, x_quantity_per_code 20.
    static Func<string, JsonElement, string> Responder(int productId, string po, string lot, int[] lineProducts) => (model, kwargs) =>
    {
        string value = ViindooStub.DomainValue(kwargs);
        switch (model)
        {
            case "mrp.production" when value.StartsWith("NM/MO/P"):
                return """[{"origin":"S""" + value[7..] + "\"}]";
            case "mrp.production":
                return "[" + ViindooStub.WoRow(value, productId, $"V2-{productId}", origin: "NM/MO/P" + value[6..]) + "]";
            case "product.product":
                return """[{"x_custcode":"HW0032","x_quantity_per_code":20}]""";
            case "sale.order":
                var ids = lineProducts.Select((_, i) => 100 + i).ToArray();
                return JsonSerializer.Serialize(new[] { new { name = value, x_ERP_PO = po, x_LotID = lot, order_line = ids } });
            case "sale.order.line":
                return JsonSerializer.Serialize(lineProducts.Select((p, i) => new { id = 100 + i, product_id = new object[] { p, "x" } }));
            default:
                return "[]";
        }
    };

    async Task<HttpClient> StartAsync(WebApplicationFactory<Program> app, int productId, string? desc)
    {
        var admin = app.CreateClient();
        var loginPage = await admin.GetStringAsync("/toast/admin/account/login");
        await admin.PostAsync("/toast/admin/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["userName"] = "admin", ["password"] = "test-pass", ["__RequestVerificationToken"] = ToastFactory.Token(loginPage)
        }));
        string token = ToastFactory.Token(await admin.GetStringAsync("/toast/admin/product"));
        var product = await ToastFactory.PostAsync(admin, token, "/toast/admin/product/api",
            new { productId, defaultCode = $"V2-{productId}", name = "SP", barcode = "BC", pcsPerBox = 20 });
        Assert.True(product.GetProperty("success").GetBoolean(), product.ToString());
        var template = await ToastFactory.PostAsync(admin, token, "/toast/admin/label-template/api", new
        {
            defaultCode = $"V2-{productId}", type = "Box", operation = "Toast", size = "4x6", dpi = 203, zplData = Zpl,
            variables = new[] { new { key = "MODEL", value = "TDC300" }, new { key = "PART", value = "Toast Go 3 Charging Dock" }, new { key = "DESC", value = desc ?? "" } }
        });
        Assert.True(template.GetProperty("success").GetBoolean(), template.ToString());
        return app.CreateClient();
    }

    static Dictionary<string, JsonElement> Vars(JsonElement res) =>
        res.GetProperty("variables").EnumerateArray().ToDictionary(v => v.GetProperty("key").GetString()!);

    [Fact]
    public async Task Fills_template_from_viindoo_and_reports_missing()
    {
        // Đơn 3 dòng, sản phẩm 4101 ở dòng 2 -> PO2 / L2
        using var app = ViindooStub.Use(factory, Responder(4101, "PO1,PO2,PO3", "L1,L2,L3", [952, 4101, 953]));
        var c = await StartAsync(app, 4101, desc: null);

        var res = await c.GetFromJsonAsync<JsonElement>("/toastv2/api/wo?wo=X4101");

        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());
        var wo = res.GetProperty("wo");
        Assert.Equal("S" + "X4101", wo.GetProperty("sale_order").GetString()); // WO con -> lệnh cha -> đơn bán
        var v = Vars(res);
        Assert.Equal("HW0032", v["PART_NUMBER"].GetProperty("value").GetString());
        Assert.Equal("customer_part_number", v["PART_NUMBER"].GetProperty("field").GetString());
        Assert.Equal("PO2", v["PO"].GetProperty("value").GetString());
        Assert.Equal("L2", v["LOT_ID"].GetProperty("value").GetString());
        Assert.Equal("20", v["QTY"].GetProperty("value").GetString());
        Assert.Equal("template", v["MODEL"].GetProperty("source").GetString());
        Assert.Equal(["DESC"], res.GetProperty("missing").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Contains(res.GetProperty("problems").EnumerateArray(), p => p.GetString()!.Contains("{DESC}"));
        Assert.Empty(res.GetProperty("warnings").EnumerateArray());
    }

    [Fact]
    public async Task Several_po_for_same_product_left_blank_with_warning()
    {
        using var app = ViindooStub.Use(factory, Responder(4201, "PO1,PO2", "", [4201, 4201]));
        var c = await StartAsync(app, 4201, desc: "Station de recharge");

        var res = await c.GetFromJsonAsync<JsonElement>("/toastv2/api/wo?wo=X4201");

        var v = Vars(res);
        Assert.Equal("", v["PO"].GetProperty("value").GetString());
        Assert.Equal("", v["LOT_ID"].GetProperty("value").GetString());
        var warnings = res.GetProperty("warnings").EnumerateArray().Select(x => x.GetString()!).ToList();
        Assert.Contains(warnings, w => w.Contains("nhiều PO") && w.Contains("PO1, PO2"));
        Assert.Contains(warnings, w => w.Contains("chưa có Lot ID"));
        Assert.Equal(["PO", "LOT_ID"], res.GetProperty("missing").EnumerateArray().Select(x => x.GetString()).ToArray());
    }

    [Fact]
    public async Task Done_wo_opens_on_v2_with_warning_but_not_on_station()
    {
        var responder = Responder(4401, "PO1", "L1", [4401]);
        using var app = ViindooStub.Use(factory, (model, kwargs) =>
        {
            string json = responder(model, kwargs);
            return model == "mrp.production" && !ViindooStub.DomainValue(kwargs).StartsWith("NM/MO/P") ? json.Replace("\"confirmed\"", "\"done\"") : json;
        });
        var c = await StartAsync(app, 4401, desc: "x");

        var v2 = await c.GetFromJsonAsync<JsonElement>("/toastv2/api/wo?wo=X4401");
        Assert.True(v2.GetProperty("success").GetBoolean(), v2.ToString());
        Assert.Contains("đã hoàn thành", v2.GetProperty("warnings")[0].GetString());

        var station = await c.GetFromJsonAsync<JsonElement>("/toast/api/wo?wo=X4401");
        Assert.False(station.GetProperty("success").GetBoolean());
        Assert.Contains("đã hoàn thành", station.GetProperty("error").GetString());
    }

    // Giống S00589: 5 dòng cùng sản phẩm, khác SL, khác PO, cùng Lot. Mỗi dòng 1 nhóm WO (id 501..505), tổng SL nhóm = SL dòng.
    // groupQty: tổng SL từng nhóm (chia thành nhiều lệnh backorder để kiểm tra phép cộng). WO đang mở thuộc nhóm woGroup.
    static Func<string, JsonElement, string> PouchResponder(int productId, int woGroup, decimal[] lineQty, decimal[] groupQty) => (model, kwargs) =>
    {
        var domain = kwargs.GetProperty("domain");
        string field = domain[0][0].GetString()!;
        switch (model)
        {
            case "mrp.production" when field == "name":
                string name = domain[0][2].GetString()!;
                return JsonSerializer.Serialize(new[] { new
                {
                    name, state = "confirmed", product_id = new object[] { productId, $"[V2-{productId}] Pouch" }, product_qty = 10,
                    origin = "S589", procurement_group_id = new object[] { woGroup, "g" }
                } });
            case "mrp.production" when field == "procurement_group_id":
                int g = domain[0][2].GetInt32();
                decimal total = groupQty[g - 501];
                // Tách thành 2 lệnh (gốc + backorder)
                return JsonSerializer.Serialize(new[] { new { product_qty = total - 10 }, new { product_qty = 10m } });
            case "mrp.production" when field == "origin":
                return JsonSerializer.Serialize(groupQty.SelectMany((q, i) => new[]
                {
                    new { procurement_group_id = new object[] { 501 + i, "g" }, product_qty = q - 10 },
                    new { procurement_group_id = new object[] { 501 + i, "g" }, product_qty = 10m }
                }));
            case "product.product":
                return """[{"x_custcode":"HW0032","x_quantity_per_code":20}]""";
            case "sale.order":
                return JsonSerializer.Serialize(new[] { new
                {
                    name = "S589", x_ERP_PO = "PO14362,PO14361,PO14361,PO14473,PO14682",
                    x_LotID = "0001-0-0626,0001-0-0626,0001-0-0626,0001-0-0626,0001-0-0626",
                    order_line = lineQty.Select((_, i) => 100 + i).ToArray()
                } });
            case "sale.order.line":
                return JsonSerializer.Serialize(lineQty.Select((q, i) => new { id = 100 + i, product_id = new object[] { productId, "x" }, product_uom_qty = q }));
            default:
                return "[]";
        }
    };

    [Theory]
    [InlineData(501, "PO14362")] // nhóm 300 -> dòng 1
    [InlineData(503, "PO14361")] // nhóm 3100 -> dòng 3
    [InlineData(504, "PO14473")] // nhóm 3000 -> dòng 4
    [InlineData(505, "PO14682")] // nhóm 600 -> dòng 5
    public async Task Po_guessed_by_work_order_group_quantity(int woGroup, string po)
    {
        decimal[] qty = [300, 1800, 3100, 3000, 600];
        using var app = ViindooStub.Use(factory, PouchResponder(4501 + woGroup, woGroup, qty, qty));
        var c = await StartAsync(app, 4501 + woGroup, desc: "x");

        var res = await c.GetFromJsonAsync<JsonElement>("/toastv2/api/wo?wo=P" + woGroup);

        var v = Vars(res);
        Assert.Equal(po, v["PO"].GetProperty("value").GetString());
        Assert.Equal("0001-0-0626", v["LOT_ID"].GetProperty("value").GetString()); // 5 dòng cùng Lot: không cần đoán
        var warnings = res.GetProperty("warnings").EnumerateArray().Select(x => x.GetString()!).ToList();
        Assert.Contains(warnings, w => w.StartsWith($"PO = {po}: chọn theo số lượng"));
        Assert.DoesNotContain(warnings, w => w.StartsWith("Lot"));
    }

    [Theory]
    [InlineData(501, "PO14362")] // nhóm thứ 1 cùng SL 300 -> dòng 1
    [InlineData(502, "PO14361")] // nhóm thứ 2 cùng SL 300 -> dòng 2
    public async Task Same_quantity_lines_matched_by_order(int woGroup, string po)
    {
        // Dòng 1 và 2 cùng sản phẩm, cùng SL 300 nhưng khác PO: ghép theo thứ tự nhóm WO
        decimal[] qty = [300, 300, 3100, 3000, 600];
        using var app = ViindooStub.Use(factory, PouchResponder(4600 + woGroup, woGroup, qty, qty));
        var c = await StartAsync(app, 4600 + woGroup, desc: "x");

        var res = await c.GetFromJsonAsync<JsonElement>("/toastv2/api/wo?wo=P" + woGroup);

        Assert.Equal(po, Vars(res)["PO"].GetProperty("value").GetString());
        Assert.Contains(res.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains("thứ tự"));
    }

    [Fact]
    public async Task Group_quantity_matching_no_line_leaves_po_blank()
    {
        // Ai đó sửa SL: nhóm 502 tổng 1700, không dòng nào 1700
        using var app = ViindooStub.Use(factory, PouchResponder(4701, 502, [300, 1800, 3100, 3000, 600], [300, 1700, 3100, 3000, 600]));
        var c = await StartAsync(app, 4701, desc: "x");

        var res = await c.GetFromJsonAsync<JsonElement>("/toastv2/api/wo?wo=P502");

        Assert.Equal("", Vars(res)["PO"].GetProperty("value").GetString());
        Assert.Contains(res.GetProperty("warnings").EnumerateArray(), w => w.GetString()!.Contains("không khớp số lượng dòng nào"));
    }

    [Fact]
    public async Task Single_po_value_applies_to_whole_order()
    {
        using var app = ViindooStub.Use(factory, Responder(4301, "PO77", "LOT-A", [952, 4301]));
        var c = await StartAsync(app, 4301, desc: "x");

        var v = Vars(await c.GetFromJsonAsync<JsonElement>("/toastv2/api/wo?wo=X4301"));

        Assert.Equal("PO77", v["PO"].GetProperty("value").GetString());
        Assert.Equal("LOT-A", v["LOT_ID"].GetProperty("value").GetString());
    }
}
