using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Toast.Tests;

/// <summary>Màn Tra cứu đơn bán (/toast/admin/sale-order): dòng đơn, PO / Lot theo dòng, ghép nhóm WO theo số lượng.</summary>
public class SaleOrderLookupTests(ToastFactory factory) : IClassFixture<ToastFactory>
{
    const string Api = "/toast/admin/sale-order/api";

    // Giống S00589: 5 dòng TG3 Pouch (1341). Dòng 1 và 2 cùng SL 300 (để thử ghép theo thứ tự).
    // Nhóm WO 501..505 = SL từng dòng (mỗi nhóm 2 lệnh: 1 done + 1 đang mở, trừ nhóm 501 xong hết), nhóm 509 SL 999 không khớp dòng nào.
    static readonly decimal[] LineQty = [300, 300, 3100, 3000, 600];

    static string Respond(string model, JsonElement kwargs)
    {
        switch (model)
        {
            case "sale.order":
                Assert.Equal("S00589", ViindooStub.DomainValue(kwargs)); // "589" -> "S00589"
                return JsonSerializer.Serialize(new[] { new
                {
                    name = "S00589", state = "sale", partner_id = new object[] { 242, "Sigma Worldwide Asia Limited" }, date_order = "2026-07-06 03:06:30",
                    x_ERP_PO = "PO14362,PO14361,PO14361,PO14473,PO14682",
                    x_LotID = "0001-0-0626",
                    x_ERP_Deadline_Shipment = "20260714,20260721,20260817,20260817,20260817",
                    order_line = new[] { 1479, 1480, 1481, 1482, 1483 }
                } });
            case "sale.order.line":
                return JsonSerializer.Serialize(LineQty.Select((q, i) => new
                {
                    id = 1479 + i, product_id = new object[] { 1341, "[FG-TRO-CE-0007-00] FG TG3 POUCH" }, product_uom_qty = q
                }));
            case "mrp.production":
                Assert.Equal("origin", kwargs.GetProperty("domain")[0][0].GetString());
                int id = 1;
                var rows = new List<object>();
                void Group(int gid, decimal total, bool allDone)
                {
                    rows.Add(new { id = id++, name = $"NM/MO/0{gid}-001", state = "done", product_id = new object[] { 1341, "x" }, product_qty = total - 10, procurement_group_id = new object[] { gid, $"NM/MO/0{gid}" } });
                    rows.Add(new { id = id++, name = $"NM/MO/0{gid}-002", state = allDone ? "done" : "confirmed", product_id = new object[] { 1341, "x" }, product_qty = 10m, procurement_group_id = new object[] { gid, $"NM/MO/0{gid}" } });
                }
                for (int i = 0; i < LineQty.Length; i++) Group(501 + i, LineQty[i], allDone: i == 0);
                Group(509, 999, allDone: false);
                return JsonSerializer.Serialize(rows);
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
    public async Task Lists_lines_with_po_lot_and_matched_work_order_groups()
    {
        using var app = ViindooStub.Use(factory, Respond);
        var client = await LoginAsync(app);

        var res = await client.GetFromJsonAsync<JsonElement>(Api + "?so=589");

        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());
        var o = res.GetProperty("order");
        Assert.Equal("S00589", o.GetProperty("name").GetString());
        var lines = o.GetProperty("lines").EnumerateArray().ToList();
        Assert.Equal(5, lines.Count);

        // PO theo dòng, Lot 1 giá trị = cả đơn, deadline theo dòng
        Assert.Equal(["PO14362", "PO14361", "PO14361", "PO14473", "PO14682"], lines.Select(l => l.GetProperty("po").GetString()).ToArray());
        Assert.All(lines, l => Assert.Equal("0001-0-0626", l.GetProperty("lot").GetString()));
        Assert.Equal("20260721", lines[1].GetProperty("deadline").GetString());

        // Ghép nhóm: dòng 1, 2 cùng SL 300 -> theo thứ tự nhóm 501, 502; còn lại theo SL
        Assert.Equal(["NM/MO/0501", "NM/MO/0502", "NM/MO/0503", "NM/MO/0504", "NM/MO/0505"],
            lines.Select(l => l.GetProperty("group").GetProperty("groupName").GetString()).ToArray());
        var g3 = lines[2].GetProperty("group");
        Assert.Equal(2, g3.GetProperty("count").GetInt32());
        Assert.Equal(3100, g3.GetProperty("totalQty").GetDecimal());
        Assert.Equal(["NM/MO/0503-002"], g3.GetProperty("openWos").EnumerateArray().Select(x => x.GetString()).ToArray());
        Assert.Empty(lines[0].GetProperty("group").GetProperty("openWos").EnumerateArray()); // nhóm 501 xong hết
        Assert.Equal("NM/MO/0501-002", lines[0].GetProperty("group").GetProperty("latestWo").GetString());

        // Nhóm 999 không khớp dòng nào
        var unmatched = o.GetProperty("unmatchedGroups").EnumerateArray().ToList();
        Assert.Single(unmatched);
        Assert.Equal("NM/MO/0509", unmatched[0].GetProperty("groupName").GetString());
        Assert.Equal(12, o.GetProperty("woCount").GetInt32());
        Assert.Empty(o.GetProperty("warnings").EnumerateArray());
    }

    [Fact]
    public async Task Requires_admin_login()
    {
        using var app = ViindooStub.Use(factory, Respond);
        var client = app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var res = await client.GetAsync(Api + "?so=589");
        Assert.False(res.IsSuccessStatusCode);
    }
}
