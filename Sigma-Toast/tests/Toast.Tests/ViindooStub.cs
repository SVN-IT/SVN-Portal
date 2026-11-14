using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Toast.Web.Features.Viindoo;

namespace Toast.Tests;

/// <summary>
/// Viindoo giả (JSON-RPC) cho ViindooRpc: đăng nhập luôn OK; call_kw gọi respond(model, kwargs) để lấy mảng kết quả (JSON).
/// Kiểm tra luôn Toast chỉ gọi search_read có limit (hoặc read theo id). Hàm ghi (nhập KQSX: write, button_mark_done...) gọi
/// respond("model#method", params) với params = {args, kwargs} của lời gọi.
/// </summary>
public sealed class ViindooStub(Func<string, JsonElement, string> respond) : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.GetProperty("params");
        string result;
        if (request.RequestUri!.AbsolutePath == "/web/session/authenticate")
            result = """{"uid":2}""";
        else if (body.GetProperty("method").GetString() == "read")
        {
            // read(ids, fields) -> đưa về dạng search_read domain [["id","in",ids]] để responder xử lý chung
            var ids = body.GetProperty("args")[0];
            var kwargs = JsonDocument.Parse("""{"domain":[["id","in",""" + ids.GetRawText() + """]],"limit":""" + ids.GetArrayLength() + "}").RootElement;
            result = respond(body.GetProperty("model").GetString()!, kwargs);
        }
        else if (body.GetProperty("method").GetString() is "write" or "button_mark_done" or "create" or "action_confirm" or "action_backorder")
        {
            result = respond(body.GetProperty("model").GetString() + "#" + body.GetProperty("method").GetString(), body);
        }
        else
        {
            Assert.Equal("search_read", body.GetProperty("method").GetString());
            var kwargs = body.GetProperty("kwargs");
            Assert.True(kwargs.GetProperty("limit").GetInt32() > 0);
            result = respond(body.GetProperty("model").GetString()!, kwargs);
        }
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"jsonrpc":"2.0","result":""" + result + "}", Encoding.UTF8, "application/json")
        };
    }

    // Giá trị của điều kiện đầu tiên trong domain (bỏ qua toán tử "|" / "&"), VD [["name","=","NM/MO/06229"]] -> "NM/MO/06229"
    public static string DomainValue(JsonElement kwargs)
    {
        var v = kwargs.GetProperty("domain").EnumerateArray().First(d => d.ValueKind == JsonValueKind.Array)[2];
        return v.ValueKind == JsonValueKind.String ? v.GetString()! : v.GetRawText();
    }

    // 1 dòng mrp.production
    // bom: tên BoM của lệnh, null = lệnh không có BoM
    // id: id lệnh trên Viindoo, 0 = không trả id
    public static string WoRow(string name, int productId, string code, string state = "confirmed", decimal qty = 100, string origin = "S00689",
        string? bom = "BoM test", int id = 0) =>
        JsonSerializer.Serialize(new
        {
            id = id > 0 ? (object)id : false, name, state, product_id = new object[] { productId, $"[{code}] Sản phẩm {productId}" }, product_qty = qty,
            origin = origin.Length == 0 ? (object)false : origin,
            bom_id = bom == null ? (object)false : new object[] { 1, bom }
        });

    // App test trỏ ViindooRpc tới stub (extra: thêm cấu hình khác của test)
    public static WebApplicationFactory<Program> Use(WebApplicationFactory<Program> factory, Func<string, JsonElement, string> respond,
        Action<IWebHostBuilder>? extra = null) =>
        factory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Viindoo:Url", "http://viindoo.test");
            b.UseSetting("Viindoo:Db", "db");
            b.UseSetting("Viindoo:Login", "u");
            b.UseSetting("Viindoo:Password", "p");
            extra?.Invoke(b);
            b.ConfigureTestServices(s => s.AddHttpClient(ViindooRpc.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new ViindooStub(respond)));
        });
}
