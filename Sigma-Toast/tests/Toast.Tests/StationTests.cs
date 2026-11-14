using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Toast.Web.Data;

namespace Toast.Tests;

/// <summary>
/// Màn công nhân /toast: mở WO (khớp đúng tên), quét barcode, đủ pcs/box tự đóng box và in tem,
/// chỉnh số / đóng / xoá box bằng PIN, in thủ công. Máy in giả = TcpListener cục bộ, Viindoo giả = HTTP stub.
/// </summary>
public class StationTests(ToastFactory factory) : IClassFixture<ToastFactory>
{
    const string Pin = "1234"; // Toast:LeaderPin trong ToastFactory
    // Mẫu Box theo mã sản phẩm: {WO} tự lấy theo WO, {BOX_NO} theo box, {QTY} = pcs/box, {PART} / {MODEL} lưu giá trị trong mẫu
    const string Zpl = "^XA^FO50,50^FD{PART}^FS^FO50,100^FD{MODEL}^FS^FO50,150^FD{WO}#{BOX_NO}#{QTY}^FS^XZ";

    // Máy in giả nhận mọi kết nối, mỗi kết nối là 1 lần gửi
    sealed class FakePrinter : IDisposable
    {
        readonly TcpListener listener = new(IPAddress.Loopback, 0);
        public ConcurrentQueue<string> Received { get; } = new();
        public int Port { get; }

        public FakePrinter()
        {
            listener.Start();
            Port = ((IPEndPoint)listener.LocalEndpoint).Port;
            _ = Task.Run(async () =>
            {
                try
                {
                    while (true)
                    {
                        var conn = await listener.AcceptTcpClientAsync();
                        _ = Task.Run(async () =>
                        {
                            using (conn)
                            using (var ms = new MemoryStream())
                            {
                                await conn.GetStream().CopyToAsync(ms);
                                Received.Enqueue(Encoding.UTF8.GetString(ms.ToArray()));
                            }
                        });
                    }
                }
                catch (ObjectDisposedException) { }
                catch (SocketException) { }
            });
        }

        public async Task<int> WaitForAsync(int count)
        {
            for (int i = 0; i < 50 && Received.Count < count; i++) await Task.Delay(100);
            return Received.Count;
        }

        public void Dispose() => listener.Stop();
    }

    // Viindoo giả: mrp.production theo đúng tên; WO "NM/MO/WO<pid>" -> sản phẩm pid (mã PN-<pid>), tên khác map -> sản phẩm 951.
    // Tên chứa OTHER: không có lệnh nào (Toast tìm đúng tên). Tên chứa DONE: lệnh đã hoàn thành. Tên chứa NOBOM: lệnh không có BoM.
    // Nhân viên: mã nằm ở tên thẻ. "SVN 0216" có dấu cách vẫn nhận; "061" sai dạng bỏ; SVN0215 gắn 2 nhân viên -> bản ghi sửa gần nhất;
    // SVN0217 gắn 2 nhân viên sửa cùng lúc -> id nhỏ nhất. Nhân viên không thẻ bỏ.
    const string EmployeeTags = """[{"id":1,"name":"SVN0215"},{"id":2,"name":"SVN 0216"},{"id":3,"name":"061"},{"id":4,"name":"SVN0217"},{"id":5,"name":"HR Staff"},{"id":6,"name":"SVN0001"},{"id":7,"name":"SVN0388"}]""";
    const string Employees = """
        [{"id":10,"name":"Cũ","work_phone":"111","category_ids":[1],"__last_update":"2026-01-01 00:00:00"},
         {"id":11,"name":"Nguyễn Văn A","work_phone":"0225.366.8818","category_ids":[1],"__last_update":"2026-06-01 00:00:00"},
         {"id":12,"name":"Trần Thị B","work_phone":false,"category_ids":[2,5],"__last_update":"2026-06-01 00:00:00"},
         {"id":13,"name":"Leader","work_phone":false,"category_ids":[3],"__last_update":"2026-06-01 00:00:00"},
         {"id":15,"name":"Sau","work_phone":false,"category_ids":[4],"__last_update":"2026-06-01 00:00:00"},
         {"id":14,"name":"Trước","work_phone":false,"category_ids":[4],"__last_update":"2026-06-01 00:00:00"},
         {"id":16,"name":"Không thẻ","work_phone":false,"category_ids":[],"__last_update":"2026-06-01 00:00:00"},
         {"id":17,"name":"Nhân viên test","work_phone":false,"category_ids":[6],"__last_update":"2026-06-01 00:00:00"},
         {"id":18,"name":"Lê Văn C","work_phone":false,"category_ids":[7],"__last_update":"2026-06-01 00:00:00"}]
        """;

    // viindooPcs: x_quantity_per_code của product.product theo id (không có = không đọc được sản phẩm).
    // saleLot: đơn bán S00689 có Lot ID này (1 giá trị cho cả đơn), null = không có đơn bán.
    // id lệnh giả theo tên (cố định giữa các lần gọi, mỗi tên 1 id)
    static int StubMoId(string wo) => 1 + (int)(wo.Aggregate(17u, (h, ch) => h * 31 + ch) % 1_000_000);

    static Func<string, JsonElement, string> WoResponder(Dictionary<string, int> productByWo, Dictionary<int, int> viindooPcs, string? saleLot = null) => (model, kwargs) =>
    {
        if (model == "hr.employee.category") return EmployeeTags;
        if (model == "hr.employee") return Employees;
        if (model == "product.product")
            return int.TryParse(ViindooStub.DomainValue(kwargs), out int id) && viindooPcs.TryGetValue(id, out int pcs)
                ? JsonSerializer.Serialize(new[] { new { id, x_custcode = false, x_quantity_per_code = pcs } }) : "[]";
        if (model == "sale.order")
            return saleLot == null ? "[]"
                : JsonSerializer.Serialize(new[] { new { id = 1, name = "S00689", x_ERP_PO = "PO1", x_LotID = saleLot, order_line = Array.Empty<int>() } });
        if (model != "mrp.production") return "[]";
        string wo = ViindooStub.DomainValue(kwargs);
        if (wo.Contains("OTHER")) return "[]";
        // "WO<pid>L<n>": WO khác của cùng sản phẩm pid (VD 2 line chạy 2 WO cùng sản phẩm)
        int pid = productByWo.GetValueOrDefault(System.Text.RegularExpressions.Regex.Replace(wo.Replace("NM/MO/", "").Replace("NOSO", "WO"), @"L\d+$", ""), 951);
        // "NOSO<pid>": lệnh của sản phẩm pid nhưng không có đơn bán (origin trống)
        return "[" + ViindooStub.WoRow(wo, pid, $"PN-{pid}", wo.Contains("DONE") ? "done" : "confirmed", origin: wo.Contains("NOSO") ? "" : "S00689",
            bom: wo.Contains("NOBOM") ? null : "BoM test", id: StubMoId(wo)) + "]";
    };

    // Id máy in (Toast_Printer) trỏ tới máy in giả của test đang chạy (xUnit tạo instance mới cho mỗi test)
    string printerId = "";
    // Toast:WriteDashboardLog cho app của test đang chạy (mặc định tắt như appsettings)
    bool writeDashboardLog;
    // Id mẫu tem Box (của sản phẩm tạo sau cùng) trong test đang chạy
    int templateId;
    // Viindoo giả: x_quantity_per_code khác Pcs / box của Toast (theo product id); mặc định = Pcs / box của Toast
    readonly Dictionary<int, int> viindooPcsOverride = [];
    // Viindoo giả: Lot ID của đơn bán (null = không có đơn bán); sản phẩm tạo trong test quét theo Lot ID hay barcode cố định
    string? saleLot;
    bool scanByLot;
    // Toast:PushProduction cho app của test đang chạy (mặc định tắt như appsettings)
    bool pushProduction;
    // Viindoo giả: trả lời trước WoResponder (null = để WoResponder trả lời), VD giả lập nhập KQSX
    Func<string, JsonElement, string?>? extraResponder;

    // Viindoo trỏ tới stub. Tạo sẵn máy in (trỏ tới máy in giả), quy cách + mẫu tem Box (theo mã sản phẩm) cho các product id.
    async Task<(HttpClient station, string token, WebApplicationFactory<Program> app)> StartAsync(FakePrinter printer, params (int pid, string barcode, int pcs)[] products)
    {
        var baseResponder = WoResponder(products.ToDictionary(p => "WO" + p.pid, p => p.pid),
            products.ToDictionary(p => p.pid, p => viindooPcsOverride.GetValueOrDefault(p.pid, p.pcs)), saleLot);
        var extra = extraResponder;
        var app = ViindooStub.Use(factory, (m, k) => extra?.Invoke(m, k) ?? baseResponder(m, k),
            b =>
            {
                b.UseSetting("Toast:WriteDashboardLog", writeDashboardLog ? "true" : "false");
                b.UseSetting("Toast:PushProduction", pushProduction ? "true" : "false");
            });

        // Dữ liệu cần có: đăng nhập admin để tạo sản phẩm + mẫu tem
        var admin = app.CreateClient();
        var loginPage = await admin.GetStringAsync("/toast/admin/account/login");
        await admin.PostAsync("/toast/admin/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["userName"] = "admin", ["password"] = "test-pass", ["__RequestVerificationToken"] = ToastFactory.Token(loginPage)
        }));
        string adminToken = ToastFactory.Token(await admin.GetStringAsync("/toast/admin/product"));
        var created = await ToastFactory.PostAsync(admin, adminToken, "/toast/admin/printer/api",
            new { code = $"FAKE-{printer.Port}", name = "Máy in giả", ip = "127.0.0.1", port = printer.Port, isActive = true });
        printerId = created.GetProperty("id").GetInt32().ToString();
        foreach (var (pid, barcode, pcs) in products)
        {
            var product = await ToastFactory.PostAsync(admin, adminToken, "/toast/admin/product/api",
                new { productId = pid, defaultCode = $"PN-{pid}", name = $"SP {pid}", scanSource = scanByLot ? "LotId" : "Barcode", barcode, pcsPerBox = pcs });
            Assert.True(product.GetProperty("success").GetBoolean(), product.ToString());
            var template = await ToastFactory.PostAsync(admin, adminToken, "/toast/admin/label-template/api", new
            {
                defaultCode = $"PN-{pid}", type = "Box", operation = "Toast", size = "4x6", dpi = 203, zplData = Zpl,
                variables = new[] { new { key = "PART", value = $"PN-{pid}" }, new { key = "MODEL", value = $"M-{pid}" } }
            });
            Assert.True(template.GetProperty("success").GetBoolean(), template.ToString());
            templateId = template.GetProperty("id").GetInt32();
        }

        // Màn công nhân không cần đăng nhập
        var station = app.CreateClient();
        string token = ToastFactory.Token(await station.GetStringAsync("/toast"));
        return (station, token, app);
    }

    static Task<JsonElement> Post(HttpClient c, string token, string url, object body) => ToastFactory.PostAsync(c, token, url, body);

    Task<JsonElement> Scan(HttpClient c, string token, string wo, int pid, string barcode) =>
        Post(c, token, "/toast/api/scan", new { wo, productId = pid, barcode, printerId });

    // Modal mở phiên: tick xác nhận + mã nhân viên + máy in (quét chỉ được khi WO có phiên đang mở)
    async Task<JsonElement> OpenSession(HttpClient c, string token, string wo, string employeeCode = "SVN0001")
    {
        var res = await Post(c, token, "/toast/api/session/start", new { wo, employeeCode, confirmed = true, printerId });
        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());
        return res;
    }

    [Fact]
    public async Task Scan_fills_box_and_prints_when_full()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (3101, "BC-3101", 2), (3102, "BC-3102", 4));
        using var _ = app;

        var open = await c.GetFromJsonAsync<JsonElement>("/toast/api/wo?wo=WO3101");
        Assert.True(open.GetProperty("success").GetBoolean(), open.ToString());
        Assert.True(open.GetProperty("state").GetProperty("canScan").GetBoolean());

        await OpenSession(c, token, "WO3101");
        // Quét mã của sản phẩm khác / mã lạ: chỉ báo "Mã sai", không tính
        var wrong = await Scan(c, token, "NM/MO/WO3101", 3101, "BC-3102");
        Assert.Equal("wrong", wrong.GetProperty("result").GetString());
        Assert.Equal("Mã sai.", wrong.GetProperty("message").GetString());
        Assert.Equal("wrong", (await Scan(c, token, "NM/MO/WO3101", 3101, "XYZ")).GetProperty("result").GetString());
        Assert.Equal(0, wrong.GetProperty("state").GetProperty("totalPcs").GetInt32());

        var first = await Scan(c, token, "NM/MO/WO3101", 3101, "bc-3101"); // không phân biệt hoa thường
        Assert.Equal(1, first.GetProperty("state").GetProperty("openBox").GetProperty("qty").GetInt32());

        var second = await Scan(c, token, "NM/MO/WO3101", 3101, "BC-3101");
        Assert.Equal(1, second.GetProperty("closedBoxNo").GetInt32());
        Assert.True(second.GetProperty("printed").GetProperty("ok").GetBoolean(), second.ToString());
        var state = second.GetProperty("state");
        Assert.Equal(JsonValueKind.Null, state.GetProperty("openBox").ValueKind);
        Assert.Equal(1, state.GetProperty("closedBoxes").GetInt32());
        Assert.Equal(2, state.GetProperty("totalPcs").GetInt32());

        Assert.Equal(1, await printer.WaitForAsync(1));
        string zpl = printer.Received.First();
        Assert.Contains("^FDPN-3101^FS", zpl);
        Assert.Contains("^FDM-3101^FS", zpl);
        Assert.Contains("^FDNM/MO/WO3101#1#2^FS", zpl);

        // Refresh: mở lại WO vẫn thấy box đã đóng
        var reopened = await c.GetFromJsonAsync<JsonElement>("/toast/api/wo?wo=NM/MO/WO3101");
        Assert.Equal(1, reopened.GetProperty("state").GetProperty("closedBoxes").GetInt32());

        // Công tắc ghi log dashboard đang tắt: không ghi SVN_ProductionInputLogs
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
        Assert.False(await db.ProductionInputLogs.AnyAsync(l => l.ProductId == 3101));
    }

    [Fact]
    public async Task Closing_box_writes_dashboard_log_when_enabled()
    {
        writeDashboardLog = true;
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (3501, "BC-3501", 2));
        using var _ = app;
        const string wo = "NM/MO/WO3501";

        await OpenSession(c, token, wo);
        await Scan(c, token, wo, 3501, "BC-3501");
        using (var scope = app.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<ToastDbContext>().ProductionInputLogs.AnyAsync(l => l.ProductId == 3501)); // box chưa đủ: chưa ghi

        var closed = await Scan(c, token, wo, 3501, "BC-3501");
        int boxId = closed.GetProperty("state").GetProperty("boxes")[0].GetProperty("id").GetInt32();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
            var log = await db.ProductionInputLogs.SingleAsync(l => l.ProductId == 3501);
            var package = await db.Packages.SingleAsync(p => p.Id == boxId);
            Assert.Equal(2m, log.ProductQty);
            Assert.Equal(wo, log.MasterWoCode);
            Assert.Equal(wo, log.WoCode);
            Assert.Equal("", log.SerialCode);
            Assert.Equal("none", log.ProductType);
            Assert.Equal("Used", log.State);
            Assert.Equal("Toast", log.Status);
            Assert.Equal(package.ClosedAt, log.DateFinished);
            Assert.Equal(log.Id, package.InputLogId);
        }

        // Sửa số pcs box đã đóng (Menu → Box): sửa luôn product_qty của dòng log
        var edited = await Post(c, token, $"/toast/api/box/{boxId}/qty", new { pin = Pin, qty = 1 });
        Assert.True(edited.GetProperty("success").GetBoolean(), edited.ToString());
        using (var scope = app.Services.CreateScope())
            Assert.Equal(1m, (await scope.ServiceProvider.GetRequiredService<ToastDbContext>().ProductionInputLogs.SingleAsync(l => l.ProductId == 3501)).ProductQty);

        // Xoá box: xoá hẳn dòng log
        await Post(c, token, $"/toast/api/box/{boxId}/delete", new { pin = Pin });
        using (var scope = app.Services.CreateScope())
            Assert.False(await scope.ServiceProvider.GetRequiredService<ToastDbContext>().ProductionInputLogs.AnyAsync(l => l.ProductId == 3501));
    }

    [Fact]
    public async Task Test_wo_skips_viindoo_and_never_writes_dashboard_log()
    {
        writeDashboardLog = true;
        using var printer = new FakePrinter();
        // Toast:TestWo trong appsettings.json: NM/MO/991144, sản phẩm thử Viindoo product ID 9999
        var (c, token, app) = await StartAsync(printer, (9999, "BC-9999", 2));
        using var _ = app;

        var open = await c.GetFromJsonAsync<JsonElement>("/toast/api/wo?wo=991144");
        Assert.True(open.GetProperty("success").GetBoolean(), open.ToString());
        Assert.True(open.GetProperty("wo").GetProperty("isTest").GetBoolean());
        Assert.Equal("WO thử", open.GetProperty("wo").GetProperty("origin").GetString());
        Assert.Equal("1000", open.GetProperty("wo").GetProperty("productQty").GetString());
        Assert.Equal(0, open.GetProperty("warnings").GetArrayLength());

        await OpenSession(c, token, "991144");
        await Scan(c, token, "NM/MO/991144", 9999, "BC-9999");
        var closed = await Scan(c, token, "NM/MO/991144", 9999, "BC-9999");
        Assert.True(closed.GetProperty("printed").GetProperty("ok").GetBoolean(), closed.ToString());
        Assert.Equal(1, await printer.WaitForAsync(1));

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
        Assert.False(await db.ProductionInputLogs.AnyAsync(l => l.ProductId == 9999));
        Assert.Null((await db.Packages.SingleAsync(p => p.WoName == "NM/MO/991144")).InputLogId);
    }

    [Fact]
    public async Task Template_variable_without_value_blocks_scan()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (3701, "BC-3701", 1));
        using var _ = app;

        // Mẫu thêm {LOT}: không có nguồn tự động, mẫu chưa lưu giá trị
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
            var t = await db.LabelTemplates.SingleAsync(x => x.Id == templateId);
            t.ZplData = Zpl.Replace("^XZ", "^FO50,200^FD{LOT}^FS^XZ");
            await db.SaveChangesAsync();
        }
        var blocked = await c.GetFromJsonAsync<JsonElement>("/toast/api/wo?wo=WO3701");
        Assert.False(blocked.GetProperty("state").GetProperty("canScan").GetBoolean());
        Assert.Contains("{LOT}", blocked.GetProperty("state").GetProperty("blocker").GetString());
        // Modal mở phiên báo thiếu và không cho vào làm
        var check = await c.GetFromJsonAsync<JsonElement>("/toast/api/session/check?wo=WO3701");
        Assert.False(check.GetProperty("canStart").GetBoolean());
        Assert.Contains(check.GetProperty("items").EnumerateArray(), i => i.GetProperty("status").GetString() == "error" && i.GetProperty("text").GetString()!.Contains("{LOT}"));
        var start = await Post(c, token, "/toast/api/session/start", new { wo = "WO3701", employeeCode = "SVN0001", confirmed = true, printerId });
        Assert.False(start.GetProperty("success").GetBoolean());
        var scanBlocked = await Scan(c, token, "NM/MO/WO3701", 3701, "BC-3701");
        Assert.False(scanBlocked.GetProperty("success").GetBoolean());

        // Mẫu lưu giá trị LOT: quét được, tem in ra giá trị lưu trong mẫu
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
            var t = await db.LabelTemplates.SingleAsync(x => x.Id == templateId);
            t.Variables = [.. t.Variables, new() { Key = "LOT", Value = "L-01" }];
            await db.SaveChangesAsync();
        }
        Assert.True((await c.GetFromJsonAsync<JsonElement>("/toast/api/wo?wo=WO3701")).GetProperty("state").GetProperty("canScan").GetBoolean());
        await OpenSession(c, token, "WO3701");
        var closed = await Scan(c, token, "NM/MO/WO3701", 3701, "BC-3701");
        Assert.True(closed.GetProperty("printed").GetProperty("ok").GetBoolean(), closed.ToString());
        Assert.Equal(1, await printer.WaitForAsync(1));
        Assert.Contains("^FDM-3701^FS", printer.Received.First());
        Assert.Contains("^FDL-01^FS", printer.Received.First());
    }

    [Fact]
    public async Task Wo_must_match_exact_name_and_product_must_exist()
    {
        using var printer = new FakePrinter();
        var (c, _, app) = await StartAsync(printer, (3201, "BC-3201", 2));
        using var __ = app;

        var other = await c.GetFromJsonAsync<JsonElement>("/toast/api/wo?wo=OTHER3201");
        Assert.False(other.GetProperty("success").GetBoolean());
        Assert.Contains("Không tìm thấy lệnh sản xuất NM/MO/OTHER3201", other.GetProperty("error").GetString());

        var done = await c.GetFromJsonAsync<JsonElement>("/toast/api/wo?wo=DONE3201");
        Assert.False(done.GetProperty("success").GetBoolean());
        Assert.Contains("đã hoàn thành", done.GetProperty("error").GetString());

        var noSpec = await c.GetFromJsonAsync<JsonElement>("/toast/api/wo?wo=WO9999");
        Assert.False(noSpec.GetProperty("success").GetBoolean());
        Assert.Contains("chưa khai báo sản phẩm", noSpec.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Pin_actions_edit_close_and_delete_box()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (3301, "BC-3301", 5));
        using var _ = app;
        const string wo = "NM/MO/WO3301";

        await OpenSession(c, token, wo);
        var scanned = await Scan(c, token, wo, 3301, "BC-3301");
        int boxId = scanned.GetProperty("state").GetProperty("openBox").GetProperty("id").GetInt32();

        var badPin = await Post(c, token, $"/toast/api/box/{boxId}/qty", new { qty = 3, pin = "0000", printerId });
        Assert.Equal("PIN leader không đúng.", badPin.GetProperty("error").GetString());

        // Quét thiếu: tăng lên 3 pcs, box vẫn mở (chưa đủ 5)
        var qty3 = await Post(c, token, $"/toast/api/box/{boxId}/qty", new { qty = 3, pin = Pin, printerId });
        Assert.Equal(3, qty3.GetProperty("state").GetProperty("openBox").GetProperty("qty").GetInt32());

        // Đóng box thiếu -> in tem
        var closed = await Post(c, token, $"/toast/api/box/{boxId}/close", new { pin = Pin, printerId });
        Assert.True(closed.GetProperty("printed").GetProperty("ok").GetBoolean(), closed.ToString());
        Assert.Equal(1, await printer.WaitForAsync(1));

        // Box đã đóng vẫn sửa được số (VD 3 -> 4), không in lại
        var edited = await Post(c, token, $"/toast/api/box/{boxId}/qty", new { qty = 4, pin = Pin, printerId });
        Assert.Equal(4, edited.GetProperty("state").GetProperty("totalPcs").GetInt32());

        var deleted = await Post(c, token, $"/toast/api/box/{boxId}/delete", new { pin = Pin });
        Assert.Equal(0, deleted.GetProperty("state").GetProperty("totalPcs").GetInt32());

        // Số box tiếp theo = số lớn nhất còn lại + 1 (xoá hết thì lại từ 1)
        var next = await Scan(c, token, wo, 3301, "BC-3301");
        Assert.Equal(1, next.GetProperty("state").GetProperty("openBox").GetProperty("boxNo").GetInt32());
    }

    [Fact]
    public async Task Print_failure_still_closes_box_with_error()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (3401, "BC-3401", 1));
        using var _ = app;
        await OpenSession(c, token, "WO3401");
        printer.Dispose(); // máy in tắt

        var res = await Scan(c, token, "NM/MO/WO3401", 3401, "BC-3401");
        Assert.False(res.GetProperty("printed").GetProperty("ok").GetBoolean());
        var box = res.GetProperty("state").GetProperty("boxes")[0];
        Assert.Equal("Closed", box.GetProperty("status").GetString());
        Assert.Contains("Không kết nối được máy in", box.GetProperty("printError").GetString());
    }

    // Menu → In tem, trong phiên: chọn mẫu tem, WO điền dữ liệu theo giá trị đã xác nhận của phiên; biến không có nguồn (số box) nhập tay.
    // Không có WO: chỉ giá trị lưu trong mẫu, còn lại nhập tay. Cần PIN.
    [Fact]
    public async Task Label_print_in_session_fills_from_session()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (3601, "BC-3601", 2));
        using var _ = app;
        await OpenSession(c, token, "NM/MO/WO3601");

        var ctx = await c.GetFromJsonAsync<JsonElement>($"/toast/api/label/context?templateId={templateId}&wo=WO3601");
        Assert.True(ctx.GetProperty("success").GetBoolean(), ctx.ToString());
        Assert.True(ctx.GetProperty("inSession").GetBoolean());
        Assert.Equal("NM/MO/WO3601", ctx.GetProperty("wo").GetProperty("name").GetString());
        var vars = ctx.GetProperty("variables").EnumerateArray().ToDictionary(v => v.GetProperty("key").GetString()!);
        Assert.Equal("session", vars["WO"].GetProperty("source").GetString());
        Assert.Equal(("manual", ""), (vars["BOX_NO"].GetProperty("source").GetString(), vars["BOX_NO"].GetProperty("value").GetString()));

        var noWo = await c.GetFromJsonAsync<JsonElement>($"/toast/api/label/context?templateId={templateId}");
        Assert.Equal(JsonValueKind.Null, noWo.GetProperty("wo").ValueKind);
        var noWoVars = noWo.GetProperty("variables").EnumerateArray().ToDictionary(v => v.GetProperty("key").GetString()!);
        Assert.Equal(("manual", ""), (noWoVars["WO"].GetProperty("source").GetString(), noWoVars["WO"].GetProperty("value").GetString()));
        Assert.Equal("template", noWoVars["PART"].GetProperty("source").GetString());
        Assert.False((await c.GetFromJsonAsync<JsonElement>("/toast/api/label/context?wo=WO3601")).GetProperty("success").GetBoolean()); // chưa chọn mẫu

        var values = ctx.GetProperty("variables").EnumerateArray().ToDictionary(v => v.GetProperty("key").GetString()!, v => v.GetProperty("value").GetString());
        values["BOX_NO"] = "7";
        Assert.False((await Post(c, token, "/toast/api/label/print", new { templateId, values, copies = 1, pin = "", printerId })).GetProperty("success").GetBoolean());
        var ok = await Post(c, token, "/toast/api/label/print", new { templateId, values, copies = 2, pin = Pin, printerId });
        Assert.True(ok.GetProperty("success").GetBoolean(), ok.ToString());
        Assert.Equal(1, await printer.WaitForAsync(1));
        Assert.Equal(2, printer.Received.First().Split("^XA").Length - 1);
        Assert.Contains("^FDNM/MO/WO3601#7#2^FS", printer.Received.First());
    }

    // Ngoài phiên: WO đọc từ Viindoo; mẫu Plate: QTY = box / plate × pcs / box, số plate nhập tay (thiếu thì không in); tìm mẫu theo mã
    [Fact]
    public async Task Label_print_plate_outside_session()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (3602, "BC-3602", 2));
        using var _ = app;
        int plateTpl;
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
            var product = await db.Products.SingleAsync(p => p.ProductId == 3602);
            (product.OuterType, product.BoxPerOuter) = ("Plate", 5);
            var plate = new Toast.Web.Features.LabelTemplate.LabelTemplate
            {
                DefaultCode = "PN-3602", Type = "Plate", Operation = "Toast", Size = "4x6", Dpi = 203,
                ZplData = "^XA^FD{WO}|{QTY}|{PLATE_NO}^FS^XZ", CreatedAt = DateTime.Now
            };
            db.LabelTemplates.Add(plate);
            await db.SaveChangesAsync();
            plateTpl = plate.Id;
        }
        var found = await c.GetFromJsonAsync<JsonElement>("/toast/api/label/templates?q=pn-3602");
        Assert.Equal(["Box", "Plate"], found.GetProperty("templates").EnumerateArray().Select(t => t.GetProperty("type").GetString()));
        // Ô tìm trống: không liệt kê hết mẫu
        Assert.Equal(0, (await c.GetFromJsonAsync<JsonElement>("/toast/api/label/templates?q=")).GetProperty("templates").GetArrayLength());
        // Đọc WO: thông tin WO + mẫu tem của sản phẩm (ngoài phiên)
        var woRes = await c.GetFromJsonAsync<JsonElement>("/toast/api/label/wo?wo=WO3602");
        Assert.True(woRes.GetProperty("success").GetBoolean(), woRes.ToString());
        Assert.False(woRes.GetProperty("inSession").GetBoolean());
        Assert.Equal("PN-3602", woRes.GetProperty("wo").GetProperty("productCode").GetString());
        Assert.Equal(["Box", "Plate"], woRes.GetProperty("templates").EnumerateArray().Select(t => t.GetProperty("type").GetString()));
        Assert.False((await c.GetFromJsonAsync<JsonElement>("/toast/api/label/wo?wo=OTHER1")).GetProperty("success").GetBoolean());

        var ctx = await c.GetFromJsonAsync<JsonElement>($"/toast/api/label/context?templateId={plateTpl}&wo=WO3602");
        Assert.True(ctx.GetProperty("success").GetBoolean(), ctx.ToString());
        Assert.False(ctx.GetProperty("inSession").GetBoolean());
        var vars = ctx.GetProperty("variables").EnumerateArray().ToDictionary(v => v.GetProperty("key").GetString()!);
        Assert.Equal(("spec", "10"), (vars["QTY"].GetProperty("source").GetString(), vars["QTY"].GetProperty("value").GetString()));
        Assert.Equal("wo", vars["WO"].GetProperty("source").GetString());
        Assert.Equal(("manual", ""), (vars["PLATE_NO"].GetProperty("source").GetString(), vars["PLATE_NO"].GetProperty("value").GetString()));

        var missing = await Post(c, token, "/toast/api/label/print", new
        {
            templateId = plateTpl, copies = 1, pin = Pin, printerId, values = new Dictionary<string, string> { ["WO"] = "NM/MO/WO3602", ["QTY"] = "10", ["PLATE_NO"] = "" }
        });
        Assert.False(missing.GetProperty("success").GetBoolean());
        var ok = await Post(c, token, "/toast/api/label/print", new
        {
            templateId = plateTpl, copies = 1, pin = Pin, printerId, values = new Dictionary<string, string> { ["WO"] = "NM/MO/WO3602", ["QTY"] = "10", ["PLATE_NO"] = "3" }
        });
        Assert.True(ok.GetProperty("success").GetBoolean(), ok.ToString());
        Assert.Equal(1, await printer.WaitForAsync(1));
        Assert.Contains("^FDNM/MO/WO3602|10|3^FS", printer.Received.First());

        // WO không có trên Viindoo: báo lỗi ở ô WO
        var bad = await c.GetFromJsonAsync<JsonElement>($"/toast/api/label/context?templateId={plateTpl}&wo=OTHER1");
        Assert.Equal("wo", bad.GetProperty("field").GetString());
    }

    [Fact]
    public async Task Session_check_lists_items_and_blocks_missing_bom()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (3801, "BC-3801", 10));
        using var _ = app;

        var ok = await c.GetFromJsonAsync<JsonElement>("/toast/api/session/check?wo=WO3801");
        Assert.True(ok.GetProperty("success").GetBoolean(), ok.ToString());
        Assert.True(ok.GetProperty("canStart").GetBoolean(), ok.ToString());
        Assert.Equal(JsonValueKind.Null, ok.GetProperty("session").ValueKind);
        var texts = ok.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("text").GetString()!).ToList();
        Assert.Contains(texts, t => t.StartsWith("BoM: BoM test"));
        Assert.Contains(texts, t => t.Contains("10 pcs/box"));
        Assert.Contains(texts, t => t == "Barcode pcs: BC-3801");
        // Giá trị tem để đối chiếu: biến lưu trong mẫu + lấy theo WO / sản phẩm; số box (lúc in) không hiện
        var values = ok.GetProperty("values").EnumerateArray().ToDictionary(v => v.GetProperty("key").GetString()!, v => v.GetProperty("value").GetString());
        Assert.Equal("PN-3801", values["PART"]);
        Assert.Equal("M-3801", values["MODEL"]);
        Assert.Equal("10", values["QTY"]);
        Assert.False(values.ContainsKey("BOX_NO"));

        // Lệnh không có BoM: báo lỗi, không cho mở phiên (sản phẩm map mặc định 951 không được khai báo -> thêm lỗi chưa khai báo sản phẩm)
        var noBom = await c.GetFromJsonAsync<JsonElement>("/toast/api/session/check?wo=NOBOM3801");
        Assert.False(noBom.GetProperty("canStart").GetBoolean());
        Assert.Contains(noBom.GetProperty("items").EnumerateArray(), i => i.GetProperty("text").GetString() == "Lệnh chưa có BoM trên Viindoo.");
        // WO mới tinh (sản phẩm 951 chưa khai báo, chưa có mẫu tem): báo cả 2 lỗi một lượt
        var errors = noBom.GetProperty("items").EnumerateArray().Where(i => i.GetProperty("status").GetString() == "error")
            .Select(i => i.GetProperty("text").GetString()!).ToList();
        Assert.Contains(errors, t => t.Contains("chưa khai báo sản phẩm"));
        Assert.Contains(errors, t => t.StartsWith("Chưa có mẫu tem Box cho mã sản phẩm PN-951"));
    }

    [Fact]
    public async Task Product_scanned_by_lot_id_uses_lot_of_sale_order()
    {
        saleLot = "PVT-01";
        scanByLot = true;
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (4601, "", 2));
        using var _ = app;

        // Mã quét = Lot ID của đơn bán, hiện trong modal mở phiên
        var check = await c.GetFromJsonAsync<JsonElement>("/toast/api/session/check?wo=WO4601");
        Assert.True(check.GetProperty("canStart").GetBoolean(), check.ToString());
        Assert.Contains(check.GetProperty("items").EnumerateArray(), i => i.GetProperty("text").GetString() == "Mã quét pcs = Lot ID: PVT-01");

        await OpenSession(c, token, "WO4601");
        var state = (await c.GetFromJsonAsync<JsonElement>("/toast/api/wo?wo=WO4601")).GetProperty("state");
        Assert.Equal("PVT-01", state.GetProperty("session").GetProperty("scanCode").GetString());
        Assert.Equal("wrong", (await Scan(c, token, "NM/MO/WO4601", 4601, "BC-4601")).GetProperty("result").GetString());
        Assert.Equal("ok", (await Scan(c, token, "NM/MO/WO4601", 4601, "pvt-01")).GetProperty("result").GetString()); // không phân biệt hoa thường
        var closed = await Scan(c, token, "NM/MO/WO4601", 4601, "PVT-01");
        Assert.Equal(1, closed.GetProperty("closedBoxNo").GetInt32());

        // Lệnh không có đơn bán: không xác định được Lot -> không mở được phiên
        var noLot = await c.GetFromJsonAsync<JsonElement>("/toast/api/session/check?wo=NOSO4601");
        Assert.False(noLot.GetProperty("canStart").GetBoolean());
        Assert.Contains(noLot.GetProperty("items").EnumerateArray(), i => i.GetProperty("status").GetString() == "error"
            && i.GetProperty("text").GetString()!.StartsWith("Không xác định được Lot ID của WO"));
    }

    [Fact]
    public async Task Viindoo_packing_standard_must_exist_and_match_toast()
    {
        viindooPcsOverride[4701] = 0;  // Viindoo chưa có x_quantity_per_code
        viindooPcsOverride[4702] = 12; // Viindoo khác Toast (10)
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (4701, "BC-4701", 10), (4702, "BC-4702", 10), (4703, "BC-4703", 10));
        using var _ = app;

        async Task<List<string>> Errors(string wo) =>
            (await c.GetFromJsonAsync<JsonElement>("/toast/api/session/check?wo=" + wo)).GetProperty("items").EnumerateArray()
                .Where(i => i.GetProperty("status").GetString() == "error").Select(i => i.GetProperty("text").GetString()!).ToList();

        Assert.Contains(await Errors("WO4701"), t => t.StartsWith("Viindoo chưa có tiêu chuẩn đóng gói"));
        Assert.Contains(await Errors("WO4702"), t => t == "Pcs / box của Toast (10) khác Viindoo (12). Sửa cho khớp ở Quản trị → Sản phẩm hoặc trên Viindoo.");
        // Lỗi ngẫu nhiên hiếm gặp (chưa tái hiện được): in cả danh sách kiểm tra để có dữ liệu tìm nguyên nhân
        var check4703 = await c.GetFromJsonAsync<JsonElement>("/toast/api/session/check?wo=WO4703");
        Assert.True(check4703.GetProperty("items").EnumerateArray().All(i => i.GetProperty("status").GetString() != "error"), check4703.ToString());
        var start = await Post(c, token, "/toast/api/session/start", new { wo = "WO4702", employeeCode = "SVN0001", confirmed = true, printerId });
        Assert.False(start.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Session_start_validates_and_resumes_open_session()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (3901, "BC-3901", 2));
        using var _ = app;
        const string wo = "NM/MO/WO3901";

        // Chưa mở phiên thì không quét được
        var noSession = await Scan(c, token, wo, 3901, "BC-3901");
        Assert.False(noSession.GetProperty("success").GetBoolean());
        Assert.Contains("không có phiên làm việc đang mở", noSession.GetProperty("error").GetString());
        Assert.True(noSession.GetProperty("needSession").GetBoolean());

        var notConfirmed = await Post(c, token, "/toast/api/session/start", new { wo, employeeCode = "SVN0001", confirmed = false, printerId });
        Assert.False(notConfirmed.GetProperty("success").GetBoolean());
        foreach (string bad in new[] { "", "SVN12", "SVN12345", "ABC1234", "SVN12a4" })
        {
            var res = await Post(c, token, "/toast/api/session/start", new { wo, employeeCode = bad, confirmed = true, printerId });
            Assert.False(res.GetProperty("success").GetBoolean(), bad);
            Assert.Contains("SVNxxxx", res.GetProperty("error").GetString());
        }
        // Mã đúng dạng nhưng không có trong danh sách nhân viên Viindoo: báo lỗi ở ô mã nhân viên
        var unknown = await Post(c, token, "/toast/api/session/start", new { wo, employeeCode = "SVN9999", confirmed = true, printerId });
        Assert.False(unknown.GetProperty("success").GetBoolean());
        Assert.Equal("Nhân viên không tồn tại.", unknown.GetProperty("error").GetString());
        Assert.Equal("employee", unknown.GetProperty("field").GetString());
        var noPrinter = await Post(c, token, "/toast/api/session/start", new { wo, employeeCode = "SVN0001", confirmed = true, printerId = "" });
        Assert.False(noPrinter.GetProperty("success").GetBoolean());

        // Mã chữ thường được chuẩn hoá thành chữ hoa
        var first = await OpenSession(c, token, "WO3901", "svn0215");
        int sessionId = first.GetProperty("state").GetProperty("session").GetProperty("id").GetInt32();
        Assert.Equal("SVN0215", first.GetProperty("state").GetProperty("session").GetProperty("employeeCode").GetString());
        await Scan(c, token, wo, 3901, "BC-3901");

        // Mở lại (refresh / máy khác): modal báo phiên đang mở + box dở; xác nhận lại vào đúng phiên cũ
        var check = await c.GetFromJsonAsync<JsonElement>("/toast/api/session/check?wo=WO3901");
        var open = check.GetProperty("session");
        Assert.Equal(sessionId, open.GetProperty("id").GetInt32());
        Assert.Equal(1, open.GetProperty("openBox").GetProperty("qty").GetInt32());
        var again = await OpenSession(c, token, wo, "SVN0388");
        Assert.Equal(sessionId, again.GetProperty("state").GetProperty("session").GetProperty("id").GetInt32());

        var closed = await Scan(c, token, wo, 3901, "BC-3901");
        Assert.True(closed.GetProperty("printed").GetProperty("ok").GetBoolean(), closed.ToString());

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
        Assert.Equal(1, await db.Sessions.CountAsync(x => x.WoName == wo));
        var confirms = await db.SessionConfirms.Where(x => x.SessionId == sessionId).OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(["SVN0215", "SVN0388"], confirms.Select(x => x.EmployeeCode));
        // Tên / điện thoại lấy từ Viindoo lúc xác nhận; Viindoo không có điện thoại thì để trống
        Assert.Equal("Nguyễn Văn A", confirms[0].EmployeeName);
        Assert.Equal("0225.366.8818", confirms[0].EmployeePhone);
        Assert.Equal("Lê Văn C", confirms[1].EmployeeName);
        Assert.Null(confirms[1].EmployeePhone);
        Assert.Equal(int.Parse(printerId), confirms[0].PrinterId);
        Assert.Equal("PN-3901", confirms[0].Values["PART"]);
        // Box đóng gắn với phiên
        var box = await db.Packages.SingleAsync(p => p.WoName == wo);
        Assert.Equal(confirms[1].SessionId, box.SessionId);
    }

    [Fact]
    public async Task Pin_not_checked_when_require_leader_pin_off()
    {
        using var app = factory.WithWebHostBuilder(b => b.UseSetting("Toast:RequireLeaderPin", "false"));
        var c = app.CreateClient();
        string page = await c.GetStringAsync("/toast");
        Assert.Contains("requirePin: false", page);

        var res = await Post(c, ToastFactory.Token(page), "/toast/api/pin", new { pin = "" });
        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());

        Assert.Contains("syncEvery: 0", page); // mặc định chỉ gửi khi đủ box

        using var every5 = factory.WithWebHostBuilder(b => b.UseSetting("Toast:ScanSyncEvery", "5"));
        Assert.Contains("syncEvery: 5", await every5.CreateClient().GetStringAsync("/toast"));

        // Bật (ToastFactory) thì vẫn hỏi PIN
        Assert.Contains("requirePin: true", await factory.CreateClient().GetStringAsync("/toast"));
    }

    [Fact]
    public async Task Menu_pin_reprint_uses_confirmed_values()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (4001, "BC-4001", 1));
        using var _ = app;
        const string wo = "NM/MO/WO4001";

        Assert.False((await Post(c, token, "/toast/api/pin", new { pin = "0000" })).GetProperty("success").GetBoolean());
        Assert.True((await Post(c, token, "/toast/api/pin", new { pin = Pin })).GetProperty("success").GetBoolean());

        await OpenSession(c, token, wo);
        var closed = await Scan(c, token, wo, 4001, "BC-4001");
        int boxId = closed.GetProperty("state").GetProperty("boxes")[0].GetProperty("id").GetInt32();
        Assert.Equal(1, await printer.WaitForAsync(1));

        // Sửa giá trị PART trong mẫu sau khi đã xác nhận: in lại vẫn dùng giá trị đã xác nhận, số box giữ nguyên
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
            var t = await db.LabelTemplates.SingleAsync(x => x.Id == templateId);
            t.Variables = [.. t.Variables.Where(v => v.Key != "PART"), new() { Key = "PART", Value = "PART-MOI" }];
            await db.SaveChangesAsync();
        }
        Assert.False((await Post(c, token, $"/toast/api/box/{boxId}/reprint", new { pin = "0000", printerId })).GetProperty("success").GetBoolean());
        var reprint = await Post(c, token, $"/toast/api/box/{boxId}/reprint", new { pin = Pin, printerId });
        Assert.True(reprint.GetProperty("success").GetBoolean(), reprint.ToString());
        Assert.Equal(2, await printer.WaitForAsync(2));
        string second = printer.Received.Last();
        Assert.Contains("^FDPN-4001^FS", second);
        Assert.DoesNotContain("PART-MOI", second);
        Assert.Contains("^FDNM/MO/WO4001#1#1^FS", second);

        Assert.False((await Post(c, token, "/toast/api/box/0/reprint", new { pin = Pin, printerId })).GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task End_session_keeps_or_closes_open_box()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (4101, "BC-4101", 5));
        using var _ = app;
        const string wo = "NM/MO/WO4101";

        var first = await OpenSession(c, token, wo);
        int firstId = first.GetProperty("state").GetProperty("session").GetProperty("id").GetInt32();
        await Scan(c, token, wo, 4101, "BC-4101");
        await Scan(c, token, wo, 4101, "BC-4101");

        Assert.False((await Post(c, token, $"/toast/api/session/{firstId}/end", new { pin = "0000", closeOpenBox = false, printerId })).GetProperty("success").GetBoolean());
        // Giữ box dở: phiên kết thúc, box vẫn mở, không in
        var ended = await Post(c, token, $"/toast/api/session/{firstId}/end", new { pin = Pin, closeOpenBox = false, printerId });
        Assert.True(ended.GetProperty("success").GetBoolean(), ended.ToString());
        Assert.False((await Post(c, token, $"/toast/api/session/{firstId}/end", new { pin = Pin, closeOpenBox = false, printerId })).GetProperty("success").GetBoolean());
        Assert.False((await Scan(c, token, wo, 4101, "BC-4101")).GetProperty("success").GetBoolean());
        var check = await c.GetFromJsonAsync<JsonElement>("/toast/api/session/check?wo=WO4101");
        Assert.Equal(JsonValueKind.Null, check.GetProperty("session").ValueKind);

        // Phiên mới quét tiếp box dở, rồi kết thúc kèm đóng box thiếu + in tem
        var next = await OpenSession(c, token, wo);
        int nextId = next.GetProperty("state").GetProperty("session").GetProperty("id").GetInt32();
        Assert.NotEqual(firstId, nextId);
        Assert.Equal(2, next.GetProperty("state").GetProperty("openBox").GetProperty("qty").GetInt32());
        await Scan(c, token, wo, 4101, "BC-4101");
        var endClose = await Post(c, token, $"/toast/api/session/{nextId}/end", new { pin = Pin, closeOpenBox = true, printerId });
        Assert.True(endClose.GetProperty("printed").GetProperty("ok").GetBoolean(), endClose.ToString());
        Assert.Equal(1, await printer.WaitForAsync(1));
        Assert.Contains("^FDNM/MO/WO4101#1#5^FS", printer.Received.First()); // {QTY} = pcs/box của sản phẩm

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
        var box = await db.Packages.SingleAsync(p => p.WoName == wo);
        Assert.Equal("Closed", box.Status);
        Assert.Equal(3, box.Qty);
        Assert.Equal(nextId, box.SessionId);
        var sessions = await db.Sessions.Where(x => x.WoName == wo).ToListAsync();
        Assert.All(sessions, x => { Assert.Equal("Ended", x.Status); Assert.NotNull(x.EndedAt); });
    }

    [Fact]
    public async Task Agent_modes_return_job_and_box_waits_for_print_result()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (4201, "BC-4201", 1));
        using var _ = app;
        const string wo = "NM/MO/WO4201";
        var usb = new { mode = "usb", usbName = "ZDesigner ZT411" };

        var bad = await Post(c, token, "/toast/api/session/start", new { wo, employeeCode = "SVN0001", confirmed = true, printer = new { mode = "usb", usbName = "" } });
        Assert.Contains("USB", bad.GetProperty("error").GetString());
        var started = await Post(c, token, "/toast/api/session/start", new { wo, employeeCode = "SVN0001", confirmed = true, printer = usb });
        Assert.True(started.GetProperty("success").GetBoolean(), started.ToString());

        // USB qua Node: server không gửi máy in, trả việc in cho trình duyệt; box chờ kết quả
        var closed = await Post(c, token, "/toast/api/scan", new { wo, productId = 4201, barcode = "BC-4201", printer = usb });
        var printed = closed.GetProperty("printed");
        Assert.False(printed.GetProperty("ok").GetBoolean());
        Assert.Equal("/print-usb", printed.GetProperty("agent").GetProperty("path").GetString());
        Assert.Equal("ZDesigner ZT411", printed.GetProperty("agent").GetProperty("printerName").GetString());
        Assert.Contains("^FDPN-4201^FS", printed.GetProperty("agent").GetProperty("zpl").GetString());
        int boxId = closed.GetProperty("closedBoxId").GetInt32();
        var waiting = closed.GetProperty("state").GetProperty("boxes")[0];
        Assert.Equal(JsonValueKind.Null, waiting.GetProperty("printedAt").ValueKind);
        Assert.Contains("agent", waiting.GetProperty("printError").GetString());
        await Task.Delay(300);
        Assert.Empty(printer.Received);

        var ok = await Post(c, token, $"/toast/api/box/{boxId}/print-result", new { ok = true });
        Assert.NotEqual(JsonValueKind.Null, ok.GetProperty("state").GetProperty("boxes")[0].GetProperty("printedAt").ValueKind);

        // In lại qua TCP qua Node (IP nhập tay): agent /print tới IP:port, agent báo lỗi thì box ghi lỗi
        var node = new { mode = "node", source = "manual", ip = "10.1.2.3", port = 9200 };
        var re = await Post(c, token, $"/toast/api/box/{boxId}/reprint", new { pin = Pin, printer = node });
        var job = re.GetProperty("printed").GetProperty("agent");
        Assert.Equal("/print", job.GetProperty("path").GetString());
        Assert.Equal("10.1.2.3", job.GetProperty("printerIp").GetString());
        Assert.Equal(9200, job.GetProperty("printerPort").GetInt32());
        var fail = await Post(c, token, $"/toast/api/box/{boxId}/print-result", new { ok = false, error = "Agent offline" });
        Assert.Equal("Agent offline", fail.GetProperty("state").GetProperty("boxes")[0].GetProperty("printError").GetString());

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
        var box = await db.Packages.SingleAsync(p => p.Id == boxId);
        Assert.Null(box.PrinterId);
        Assert.Equal("TCP qua Node · Máy in nhập tay · 10.1.2.3:9200", box.PrintTarget);
        var confirm = await db.SessionConfirms.SingleAsync(x => x.EmployeeCode == "SVN0001" && x.PrintTarget!.StartsWith("USB"));
        Assert.Null(confirm.PrinterId);
        Assert.Equal("USB qua Node · ZDesigner ZT411", confirm.PrintTarget);
    }

    [Fact]
    public async Task Server_mode_prints_to_manual_ip_and_tests_connection()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (4301, "BC-4301", 1));
        using var _ = app;
        const string wo = "NM/MO/WO4301";
        var manual = new { mode = "server", source = "manual", ip = "127.0.0.1", port = printer.Port };

        var test = await Post(c, token, "/toast/api/test-connection", manual);
        Assert.True(test.GetProperty("success").GetBoolean(), test.ToString());
        Assert.False((await Post(c, token, "/toast/api/test-connection", new { mode = "server", source = "manual", ip = "abc" })).GetProperty("success").GetBoolean());

        Assert.True((await Post(c, token, "/toast/api/session/start", new { wo, employeeCode = "SVN0001", confirmed = true, printer = manual })).GetProperty("success").GetBoolean());
        var closed = await Post(c, token, "/toast/api/scan", new { wo, productId = 4301, barcode = "BC-4301", printer = manual });
        Assert.True(closed.GetProperty("printed").GetProperty("ok").GetBoolean(), closed.ToString());
        // Kiểm tra kết nối cũng mở 1 kết nối (không gửi gì) tới máy in giả
        Assert.Equal(2, await printer.WaitForAsync(2));
        Assert.Contains(printer.Received, r => r.Contains("^FDPN-4301^FS"));

        // Máy trong danh sách gửi dạng cấu hình (source list)
        var listed = await Post(c, token, "/toast/api/box/" + closed.GetProperty("closedBoxId").GetInt32() + "/reprint",
            new { pin = Pin, printer = new { mode = "server", source = "list", printerId } });
        Assert.True(listed.GetProperty("printed").GetProperty("ok").GetBoolean(), listed.ToString());
        Assert.Equal(3, await printer.WaitForAsync(3));
    }

    // Lệnh sang backorder sau lần nhập KQSX (06230 -> 06230-001 done, lệnh mới 06230-002): lệnh mới = phiên mới, box dở đi theo nhóm.
    // Giả lập: phiên + box dở đang thuộc lệnh trước "NM/MO/WO4401-001" (id khác), mở WO4401 (lệnh hiện tại) -> phiên mới,
    // phiên lệnh trước còn mở thì tự kết thúc, box dở chuyển sang lệnh hiện tại và quét tiếp, số box liên tục theo nhóm.
    [Fact]
    public async Task New_backorder_opens_new_session_and_carries_open_box()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (4401, "BC-4401", 2));
        using var _ = app;
        const string wo = "NM/MO/WO4401", oldName = "NM/MO/WO4401-001";

        var first = await OpenSession(c, token, wo);
        int oldSession = first.GetProperty("state").GetProperty("session").GetProperty("id").GetInt32();
        await Scan(c, token, wo, 4401, "BC-4401");
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
            var session = await db.Sessions.SingleAsync(x => x.Id == oldSession);
            Assert.Equal(wo, session.WoGroup);
            Assert.NotNull(session.ViindooMoId);
            session.WoName = oldName;
            session.ViindooMoId = 1; // lệnh trước (id khác lệnh hiện tại)
            (await db.Packages.SingleAsync(p => p.WoName == wo)).WoName = oldName;
            await db.SaveChangesAsync();
        }

        // Modal: lệnh hiện tại chưa có phiên; cảnh báo phiên lệnh trước còn mở
        var check = await c.GetFromJsonAsync<JsonElement>("/toast/api/session/check?wo=WO4401");
        Assert.Equal(JsonValueKind.Null, check.GetProperty("session").ValueKind);
        Assert.Contains(check.GetProperty("items").EnumerateArray(), i => i.GetProperty("status").GetString() == "warn"
            && i.GetProperty("text").GetString()!.StartsWith($"Phiên #{oldSession} của lệnh trước {oldName}"));

        var again = await OpenSession(c, token, wo);
        int newSession = again.GetProperty("state").GetProperty("session").GetProperty("id").GetInt32();
        Assert.NotEqual(oldSession, newSession);
        // Box dở chuyển sang lệnh hiện tại: quét thêm 1 là đủ box
        Assert.Equal(1, again.GetProperty("state").GetProperty("openBox").GetProperty("qty").GetInt32());
        var closed = await Scan(c, token, wo, 4401, "BC-4401");
        Assert.True(closed.GetProperty("printed").GetProperty("ok").GetBoolean(), closed.ToString());
        Assert.Equal(1, closed.GetProperty("closedBoxNo").GetInt32());
        // Box tiếp theo đánh số tiếp theo nhóm
        await Scan(c, token, wo, 4401, "BC-4401");

        using var scope2 = app.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ToastDbContext>();
        var old = await db2.Sessions.SingleAsync(x => x.Id == oldSession);
        Assert.Equal(Toast.Web.Features.Station.SessionStatus.Ended, old.Status);
        Assert.StartsWith("Tự động", old.EndedBy);
        Assert.Equal(Toast.Web.Features.Station.SessionStatus.Open, (await db2.Sessions.SingleAsync(x => x.Id == newSession)).Status);
        var boxes = await db2.Packages.Where(p => p.WoGroup == wo).OrderBy(p => p.PackageNo).ToListAsync();
        Assert.Equal([1, 2], boxes.Select(b => b.PackageNo));
        Assert.All(boxes, b => Assert.Equal(wo, b.WoName));
        Assert.Equal(2, boxes[0].Qty);

        // Mở lại cùng lệnh: vào lại phiên mới (không tạo thêm)
        var resume = await OpenSession(c, token, wo);
        Assert.Equal(newSession, resume.GetProperty("state").GetProperty("session").GetProperty("id").GetInt32());
    }

    // Mở lệnh mới của nhóm: chỉ xét box mới nhất của nhóm. Đã đóng -> box mới; Open nhưng đủ / vượt pcs/box -> box mới (box đó ở lại
    // lệnh trước); Open và chưa đủ (kể cả box đã đóng rồi bị mở lại) -> chuyển sang lệnh mới quét tiếp.
    [Theory]
    [InlineData("Closed", 2, false)]
    [InlineData("Open", 2, false)]
    [InlineData("Open", 3, false)]
    [InlineData("Open", 1, true)]
    public async Task New_backorder_carries_only_latest_open_box_not_full(string status, int qty, bool carried)
    {
        using var printer = new FakePrinter();
        int pid = 4801 + qty * 10 + (status == "Open" ? 1 : 0);
        var (c, token, app) = await StartAsync(printer, (pid, $"BC-{pid}", 2));
        using var _ = app;
        string wo = $"NM/MO/WO{pid}", oldName = wo + "-001";
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
            db.Packages.Add(new Toast.Web.Features.Package.Package
            {
                WoName = oldName, WoGroup = wo, ProductId = pid, DefaultCode = $"PN-{pid}", PackageNo = 1,
                Qty = qty, Capacity = 2, Status = status, CreatedAt = DateTime.Now
            });
            await db.SaveChangesAsync();
        }

        var state = (await OpenSession(c, token, wo)).GetProperty("state");
        await Scan(c, token, wo, pid, $"BC-{pid}");

        using var scope2 = app.Services.CreateScope();
        var db2 = scope2.ServiceProvider.GetRequiredService<ToastDbContext>();
        var boxes = await db2.Packages.Where(p => p.WoGroup == wo).OrderBy(p => p.PackageNo).ToListAsync();
        if (carried)
        {
            Assert.Single(boxes);
            Assert.Equal(wo, boxes[0].WoName);
            Assert.Equal(2, boxes[0].Qty);
            Assert.Equal(Toast.Web.Features.Package.PackageStatus.Closed, boxes[0].Status);
        }
        else
        {
            Assert.Equal(JsonValueKind.Null, state.GetProperty("openBox").ValueKind);
            Assert.Equal(2, boxes.Count);
            Assert.Equal(oldName, boxes[0].WoName);
            Assert.Equal(qty, boxes[0].Qty);
            Assert.Equal((wo, 2, 1), (boxes[1].WoName, boxes[1].PackageNo, boxes[1].Qty));
        }
    }

    // Viindoo giả cho nhập KQSX: 1 lệnh (id = StubMoId(wo)) SL planQty, 2 NVL (1,01 và 0,033633 / pcs, đơn vị làm tròn 0,0001).
    // Ghi lại các lời gọi ghi; button_mark_done chuyển lệnh sang done + đổi tên -001 (failMarkDone = Viindoo báo lỗi).
    // failAfterDone: Viindoo hoàn thành lệnh nhưng Toast không nhận được kết quả (lỗi trả về). doneBy: lệnh đã được người khác hoàn
    // thành từ trước (qtyProduced pcs, cách đây 1 giờ).
    sealed class FakeMo(string wo, int planQty, bool failMarkDone = false, bool failAfterDone = false, string? doneBy = null, int qtyProduced = 0)
    {
        public readonly int Id = StubMoId(wo);
        public string State = doneBy != null ? "done" : "confirmed", Name = doneBy != null ? wo + "-001" : wo;
        public decimal Produced = qtyProduced;
        public DateTime? FinishedUtc = doneBy != null ? DateTime.UtcNow.AddHours(-1) : null;
        public string By = doneBy ?? "Administrator";
        public JsonElement? Write, MarkDone;

        public string? Respond(string model, JsonElement k)
        {
            switch (model)
            {
                case "mrp.production" when ViindooStub.DomainValue(k) == $"[{Id}]":
                    return JsonSerializer.Serialize(new[] { new
                    {
                        id = Id, name = Name, state = State, product_qty = (decimal)planQty, product_tracking = "none",
                        move_raw_ids = new[] { 11, 12 }, procurement_group_id = new object[] { 77, wo },
                        qty_produced = Produced, date_finished = FinishedUtc is DateTime f ? (object)f.ToString("yyyy-MM-dd HH:mm:ss") : false,
                        write_uid = new object[] { 2, By }
                    } });
                case "mrp.production" when ViindooStub.DomainValue(k) == "77":
                    return """[{"name":"NM/MO/WO-BACKORDER-002"}]""";
                case "stock.move":
                    return """[{"id":11,"unit_factor":1.01,"state":"waiting","product_uom":[27,"PCS"]},{"id":12,"unit_factor":0.033633,"state":"waiting","product_uom":[27,"PCS"]}]""";
                case "uom.uom":
                    return """[{"id":27,"rounding":0.0001}]""";
                case "mrp.production#write":
                    Write = k.Clone();
                    return "true";
                case "mrp.production#button_mark_done":
                    if (failMarkDone) throw new InvalidOperationException("Viindoo lỗi giả");
                    MarkDone = k.Clone();
                    (State, Name, FinishedUtc) = ("done", wo + "-001", DateTime.UtcNow);
                    Produced = Write!.Value.GetProperty("args")[1].GetProperty("qty_producing").GetDecimal();
                    if (failAfterDone) throw new InvalidOperationException("Mất kết nối giả sau khi Viindoo đã xong");
                    // Viindoo thật (06387, 09/11/2026): tách backorder xong trả action mở lệnh mới, không phải wizard
                    return planQty > 0 ? """{"type":"ir.actions.act_window","res_model":"mrp.production","res_id":999,"views":[[false,"form"]]}""" : "true";
                default:
                    return null;
            }
        }
    }

    // Kết thúc phiên + công tắc bật: nhập tổng pcs box đã đóng (box dở không tính), tiêu hao theo tỉ lệ BoM, còn thiếu thì tách backorder;
    // Viindoo đổi tên lệnh -001 thì phiên + box đã đóng đổi theo, box dở giữ tên (sang lệnh sau)
    [Fact]
    public async Task End_session_pushes_closed_boxes_to_viindoo_with_backorder()
    {
        var mo = new FakeMo("NM/MO/WO4901", planQty: 10);
        extraResponder = mo.Respond;
        pushProduction = true;
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (4901, "BC-4901", 2));
        using var _ = app;
        const string wo = "NM/MO/WO4901";

        int sessionId = (await OpenSession(c, token, wo)).GetProperty("state").GetProperty("session").GetProperty("id").GetInt32();
        for (int i = 0; i < 5; i++) await Scan(c, token, wo, 4901, "BC-4901"); // 2 box đóng (4 pcs) + box dở 1/2
        var ended = await Post(c, token, $"/toast/api/session/{sessionId}/end", new { pin = Pin, closeOpenBox = false, printerId });
        Assert.True(ended.GetProperty("success").GetBoolean(), ended.ToString());
        Assert.Equal("Done", ended.GetProperty("push").GetProperty("status").GetString());

        // Ghi: qty_producing 4, NVL 4 × 1,01 = 4,04 và 4 × 0,033633 = 0,134532 → làm tròn lên 0,1346
        var vals = mo.Write!.Value.GetProperty("args")[1];
        Assert.Equal(4, vals.GetProperty("qty_producing").GetInt32());
        var raw = vals.GetProperty("move_raw_ids").EnumerateArray().ToDictionary(x => x[1].GetInt32(), x => x[2].GetProperty("quantity_done").GetDecimal());
        Assert.Equal(4.04m, raw[11]);
        Assert.Equal(0.1346m, raw[12]);
        var ctx = mo.MarkDone!.Value.GetProperty("kwargs").GetProperty("context");
        Assert.True(ctx.GetProperty("skip_backorder").GetBoolean());
        Assert.Equal([mo.Id], ctx.GetProperty("mo_ids_to_backorder").EnumerateArray().Select(x => x.GetInt32()));

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
        var session = await db.Sessions.SingleAsync(x => x.Id == sessionId);
        Assert.Equal((Toast.Web.Features.Station.PushStatus.Done, 4, wo + "-001"), (session.PushStatus, session.PushQty, session.WoName));
        Assert.Contains("NM/MO/WO-BACKORDER-002", session.PushMessage);
        // Chi tiết: lệnh trước / sau, số lượng, tiêu hao từng NVL, các bước đều ok
        var detail = JsonDocument.Parse(session.PushDetail!).RootElement;
        Assert.Equal((wo, wo + "-001", "done"), (detail.GetProperty("mo").GetProperty("nameBefore").GetString(),
            detail.GetProperty("mo").GetProperty("nameAfter").GetString(), detail.GetProperty("mo").GetProperty("stateAfter").GetString()));
        Assert.Equal((4, 4, 0), (detail.GetProperty("toastQty").GetInt32(), detail.GetProperty("pushQty").GetInt32(), detail.GetProperty("excess").GetInt32()));
        Assert.Equal([4.04m, 0.1346m], detail.GetProperty("materials").EnumerateArray().Select(m => m.GetProperty("qtyDone").GetDecimal()));
        Assert.Equal(["read", "write", "mark_done", "verify"], detail.GetProperty("steps").EnumerateArray().Select(x => x.GetProperty("step").GetString()));
        Assert.All(detail.GetProperty("steps").EnumerateArray(), x => Assert.True(x.GetProperty("ok").GetBoolean()));
        var boxes = await db.Packages.Where(p => p.WoGroup == wo).OrderBy(p => p.PackageNo).ToListAsync();
        Assert.Equal([wo + "-001", wo + "-001", wo], boxes.Select(b => b.WoName));
    }

    // Tổng pcs vượt SL còn lại: chỉ nhập SL còn lại, không tách backorder, báo số thừa. Viindoo lỗi: phiên vẫn kết thúc, PushStatus Error.
    [Fact]
    public async Task Push_caps_to_remaining_qty_and_records_off_and_error()
    {
        using var printer = new FakePrinter();

        var mo = new FakeMo("NM/MO/WO5001", planQty: 3);
        extraResponder = mo.Respond;
        pushProduction = true;
        var (c, token, app) = await StartAsync(printer, (5001, "BC-5001", 2), (5003, "BC-5003", 2));
        using var _ = app;
        int s1 = (await OpenSession(c, token, "NM/MO/WO5001")).GetProperty("state").GetProperty("session").GetProperty("id").GetInt32();
        for (int i = 0; i < 4; i++) await Scan(c, token, "NM/MO/WO5001", 5001, "BC-5001"); // 4 pcs > SL 3
        var e1 = await Post(c, token, $"/toast/api/session/{s1}/end", new { pin = Pin, closeOpenBox = false, printerId });
        Assert.Equal("Done", e1.GetProperty("push").GetProperty("status").GetString());
        Assert.Equal(3, mo.Write!.Value.GetProperty("args")[1].GetProperty("qty_producing").GetInt32());
        Assert.Empty(mo.MarkDone!.Value.GetProperty("kwargs").GetProperty("context").GetProperty("mo_ids_to_backorder").EnumerateArray());
        Assert.Contains("Thừa 1 pcs", e1.GetProperty("push").GetProperty("message").GetString());

        // Viindoo không hoàn thành lệnh (Viindoo giả không xử lý nhập KQSX cho WO5003: lệnh vẫn confirmed sau mark done) → lỗi
        int s3 = (await OpenSession(c, token, "NM/MO/WO5003")).GetProperty("state").GetProperty("session").GetProperty("id").GetInt32();
        for (int i = 0; i < 2; i++) await Scan(c, token, "NM/MO/WO5003", 5003, "BC-5003");
        var e3 = await Post(c, token, $"/toast/api/session/{s3}/end", new { pin = Pin, closeOpenBox = false, printerId });
        Assert.True(e3.GetProperty("success").GetBoolean());
        Assert.Equal("Error", e3.GetProperty("push").GetProperty("status").GetString());
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
        var failed = await db.Sessions.SingleAsync(x => x.Id == s3);
        Assert.Equal(Toast.Web.Features.Station.SessionStatus.Ended, failed.Status);
        // Chi tiết lưu cả khi lỗi: bước cuối (verify) không ok, có nội dung lỗi
        var last = JsonDocument.Parse(failed.PushDetail!).RootElement.GetProperty("steps").EnumerateArray().Last();
        Assert.Equal(("verify", false), (last.GetProperty("step").GetString(), last.GetProperty("ok").GetBoolean()));
        Assert.Contains("chưa hoàn thành", last.GetProperty("error").GetString());
    }

    // Đẩy lại khi lệnh đã done: lần trước Toast đã hoàn thành lệnh (chỉ mất kết quả) → Đã nhập + đổi tên; bên khác hoàn thành → Lệch
    [Fact]
    public async Task Repush_on_done_order_reconciles_toast_push_or_flags_mismatch()
    {
        var ours = new FakeMo("NM/MO/WO5101", planQty: 10, failAfterDone: true);
        var other = new FakeMo("NM/MO/WO5102", planQty: 10, doneBy: "Staff PD", qtyProduced: 8);
        extraResponder = (m, k) => ours.Respond(m, k) ?? other.Respond(m, k);
        pushProduction = true;
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (5101, "BC-5101", 2), (5102, "BC-5102", 2));
        using var _ = app;

        int s1 = (await OpenSession(c, token, "NM/MO/WO5101")).GetProperty("state").GetProperty("session").GetProperty("id").GetInt32();
        for (int i = 0; i < 2; i++) await Scan(c, token, "NM/MO/WO5101", 5101, "BC-5101");
        var e1 = await Post(c, token, $"/toast/api/session/{s1}/end", new { pin = Pin, closeOpenBox = false, printerId });
        Assert.Equal("Error", e1.GetProperty("push").GetProperty("status").GetString()); // Toast không nhận được kết quả
        Assert.Equal("done", ours.State);                                                // nhưng Viindoo đã xong

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
            var session = await db.Sessions.SingleAsync(x => x.Id == s1);
            // Như 06387 (09/11/2026): 1 lần đẩy lại bằng bản cũ đã ghi đè chi tiết (chỉ còn bước đọc lệnh, mất bằng chứng lần gửi).
            // Vẫn nhận ra là Toast nhờ giờ kết thúc phiên + số khớp
            session.PushDetail = """{"toastQty":2,"pushQty":0,"steps":[{"step":"read","at":"2026-01-01T00:00:00","ok":true}]}""";
            ours.Write = null;
            await scope.ServiceProvider.GetRequiredService<Toast.Web.Features.ProductionPush.ProductionPushService>().PushAsync(session);
            Assert.Null(ours.Write); // không ghi lại
            Assert.Equal((Toast.Web.Features.Station.PushStatus.Done, 2, "NM/MO/WO5101-001"), (session.PushStatus, session.PushQty, session.WoName));
            Assert.Contains("xác nhận lại từ Viindoo", session.PushMessage);
            var step = JsonDocument.Parse(session.PushDetail!).RootElement.GetProperty("steps").EnumerateArray().Last();
            Assert.Equal(("reconcile", true), (step.GetProperty("step").GetString(), step.GetProperty("ok").GetBoolean()));
            Assert.EndsWith("Khớp giờ kết thúc phiên.", step.GetProperty("note").GetString());
            Assert.True(await db.Packages.AnyAsync(p => p.WoName == "NM/MO/WO5101-001" && p.Status == "Closed"));
        }

        // Lệnh đã được Staff PD hoàn thành 8 pcs từ 1 giờ trước: Lệch, không ghi
        int s2 = (await OpenSession(c, token, "NM/MO/WO5102")).GetProperty("state").GetProperty("session").GetProperty("id").GetInt32();
        for (int i = 0; i < 2; i++) await Scan(c, token, "NM/MO/WO5102", 5102, "BC-5102");
        var e2 = await Post(c, token, $"/toast/api/session/{s2}/end", new { pin = Pin, closeOpenBox = false, printerId });
        Assert.Equal("Mismatch", e2.GetProperty("push").GetProperty("status").GetString());
        Assert.Contains("Staff PD", e2.GetProperty("push").GetProperty("message").GetString());
        Assert.Null(other.Write);
    }

    // Trạm đếm ở trình duyệt, gửi lô khi đủ box: 1 lô đủ nhiều box (mất mạng lâu) thì đóng + in từng box; gửi lại / gửi chồng lô không cộng đôi
    [Fact]
    public async Task Scan_batch_closes_boxes_and_ignores_resent_pcs()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (5301, "BC-5301", 2));
        using var _ = app;
        const string wo = "NM/MO/WO5301", device = "dev-A";
        var start = await Post(c, token, "/toast/api/session/start", new { wo, employeeCode = "SVN0001", confirmed = true, printerId, deviceId = device });
        Assert.True(start.GetProperty("success").GetBoolean(), start.ToString());

        Task<JsonElement> Batch(long from, long to) => Post(c, token, "/toast/api/scan",
            new { wo, productId = 5301, barcode = "BC-5301", printerId, deviceId = device, fromSeq = from, toSeq = to, count = (int)(to - from + 1) });

        // 5 pcs, 2 pcs / box: đóng box 1, 2 + in 2 tem, box 3 dở 1 pcs
        var res = await Batch(1, 5);
        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());
        Assert.Equal([1, 2], res.GetProperty("closed").EnumerateArray().Select(x => x.GetProperty("boxNo").GetInt32()).ToArray());
        Assert.Equal(2, await printer.WaitForAsync(2));
        Assert.Equal(1, res.GetProperty("state").GetProperty("openBox").GetProperty("qty").GetInt32());

        // Gửi lại đúng lô đó (mất phản hồi): không cộng thêm
        var again = await Batch(1, 5);
        Assert.Equal(5, again.GetProperty("state").GetProperty("totalPcs").GetInt32());
        Assert.Empty(again.GetProperty("closed").EnumerateArray());
        // Lô chồng lên phần đã nhận (4..6): chỉ cộng pcs 6 -> box 3 đủ 2 pcs, đóng
        var overlap = await Batch(4, 6);
        Assert.Equal(6, overlap.GetProperty("state").GetProperty("totalPcs").GetInt32());
        Assert.Equal(3, overlap.GetProperty("closedBoxNo").GetInt32());
    }

    // Log lỗi từ trình duyệt trạm: ghi file logs/client-yyyyMMdd.log, mỗi lỗi 1 dòng (bỏ xuống dòng / dấu |)
    [Fact]
    public async Task Client_log_is_written_to_daily_file()
    {
        string dir = Path.Combine(Path.GetTempPath(), "toast-client-log-" + Guid.NewGuid().ToString("N"));
        using var app = factory.WithWebHostBuilder(b => b.UseSetting("Toast:ClientLogDir", dir));
        var c = app.CreateClient();
        string token = ToastFactory.Token(await c.GetStringAsync("/toast"));
        var res = await Post(c, token, "/toast/api/client-log", new
        {
            device = "dev-log",
            entries = new object[]
            {
                new { time = "2026-11-11T10:00:00", level = "error", @event = "agent.print", message = "Agent offline\nline 2 | x", wo = "NM/MO/1", data = new { boxId = 5 } }
            }
        });
        Assert.True(res.GetProperty("success").GetBoolean());
        string file = Path.Combine(dir, $"client-{DateTime.Now:yyyyMMdd}.log");
        string line = Assert.Single(File.ReadAllLines(file));
        Assert.Contains("| error | dev-log |", line);
        Assert.Contains("| NM/MO/1 | agent.print | Agent offline line 2 / x | {\"boxId\":5}", line);

        // Màn admin Log trạm đọc lại file (cần đăng nhập)
        var admin = app.CreateClient();
        var loginPage = await admin.GetStringAsync("/toast/admin/account/login");
        await admin.PostAsync("/toast/admin/account/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["userName"] = "admin", ["password"] = "test-pass", ["__RequestVerificationToken"] = ToastFactory.Token(loginPage)
        }));
        Assert.Contains("Log trạm", await admin.GetStringAsync("/toast/admin/client-log"));
        var view = await admin.GetFromJsonAsync<JsonElement>("/toast/admin/client-log/api");
        Assert.Equal(DateTime.Now.ToString("yyyyMMdd"), view.GetProperty("day").GetString());
        var row = Assert.Single(view.GetProperty("rows").EnumerateArray());
        Assert.Equal("error", row.GetProperty("level").GetString());
        Assert.Equal("agent.print", row.GetProperty("event").GetString());
        Assert.Equal("{\"boxId\":5}", row.GetProperty("data").GetString());
        Assert.Contains("agent.print", await admin.GetStringAsync("/toast/admin/client-log/download?day=" + DateTime.Now.ToString("yyyyMMdd")));
        Directory.Delete(dir, true);
    }

    // Server không ghi được file log (VD thiếu quyền): trả 503 để trạm giữ log gửi lại sau; màn Log trạm hiện lỗi
    [Fact]
    public async Task Client_log_write_failure_returns_503()
    {
        // Thư mục log trỏ vào 1 file có sẵn -> không tạo được thư mục
        string blocker = Path.GetTempFileName();
        using var app = factory.WithWebHostBuilder(b => b.UseSetting("Toast:ClientLogDir", blocker));
        var c = app.CreateClient();
        string token = ToastFactory.Token(await c.GetStringAsync("/toast"));
        var req = new HttpRequestMessage(HttpMethod.Post, "/toast/api/client-log")
        {
            Content = JsonContent.Create(new { device = "dev", entries = new[] { new { time = "t", level = "warn", @event = "scan", message = "Mã sai." } } })
        };
        req.Headers.Add("RequestVerificationToken", token);
        var res = await c.SendAsync(req);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, res.StatusCode);
        File.Delete(blocker);
    }

    // Khoá Data Protection lưu ra thư mục cố định: app khởi động lại (instance mới) vẫn nhận mã chống giả mạo của trang mở từ trước
    [Fact]
    public async Task Antiforgery_token_survives_app_restart()
    {
        string keys = Path.Combine(Path.GetTempPath(), "toast-keys-" + Guid.NewGuid().ToString("N"));
        using var before = factory.WithWebHostBuilder(b => b.UseSetting("Toast:DataProtectionKeysDir", keys));
        var page = await before.CreateClient().GetAsync("/toast");
        string html = await page.Content.ReadAsStringAsync();
        string cookie = string.Join("; ", page.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));

        // "Restart": host mới, cùng thư mục khoá
        using var after = factory.WithWebHostBuilder(b => b.UseSetting("Toast:DataProtectionKeysDir", keys));
        var req = new HttpRequestMessage(HttpMethod.Post, "/toast/api/pin") { Content = JsonContent.Create(new { pin = Pin }) };
        req.Headers.Add("RequestVerificationToken", ToastFactory.Token(html));
        req.Headers.Add("Cookie", cookie);
        var res = await after.CreateClient().SendAsync(req);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Directory.Delete(keys, true);
    }

    // Icon mạng: ping = server + DB đều kết nối được, kèm giờ server
    [Fact]
    public async Task Ping_reports_database_and_server_time()
    {
        var c = factory.CreateClient();
        var res = await c.GetFromJsonAsync<JsonElement>("/toast/api/ping");
        Assert.True(res.GetProperty("db").GetBoolean());
        Assert.True(DateTime.TryParse(res.GetProperty("now").GetString(), out _));
    }

    // Mất mạng: trạm in box đủ qua agent (máy in đang chọn) bằng mẫu lưu (label/offline), có mạng lại gửi kèm box đã in -> server đóng, không in lại;
    // số box / mẫu lệch thì đánh dấu ⚠ để leader kiểm tra
    [Fact]
    public async Task Offline_printed_boxes_are_closed_without_reprint()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (5401, "BC-5401", 2));
        using var _ = app;
        const string wo = "NM/MO/WO5401", device = "dev-A";
        Assert.True((await Post(c, token, "/toast/api/session/start", new { wo, employeeCode = "SVN0001", confirmed = true, printerId, deviceId = device }))
            .GetProperty("success").GetBoolean());

        // Mẫu lưu trên trạm: biến cố định đã điền, còn {BOX_NO} (theo box) để trạm tự điền
        var offlineLabel = await c.GetFromJsonAsync<JsonElement>("/toast/api/label/offline?wo=" + wo);
        Assert.True(offlineLabel.GetProperty("success").GetBoolean(), offlineLabel.ToString());
        var label = offlineLabel.GetProperty("label");
        string zpl = label.GetProperty("zpl").GetString()!;
        Assert.Contains("^FDPN-5401^FS", zpl);
        Assert.Contains("NM/MO/WO5401#{BOX_NO}#2", zpl);
        Assert.Contains(label.GetProperty("dynamic").EnumerateArray(), d => d.GetProperty("key").GetString() == "BOX_NO");
        string version = label.GetProperty("version").GetString()!;
        Assert.Equal(version, (await c.GetFromJsonAsync<JsonElement>("/toast/api/wo?wo=" + wo)).GetProperty("state").GetProperty("template").GetProperty("version").GetString());

        var closedAt = DateTime.Now.AddMinutes(-3);
        var res = await Post(c, token, "/toast/api/scan", new
        {
            wo, productId = 5401, barcode = "BC-5401", printerId, deviceId = device, fromSeq = 1, toSeq = 4, count = 4,
            offlineBoxes = new object[]
            {
                new { boxNo = 1, closedAt = closedAt.ToString("yyyy-MM-ddTHH:mm:ss"), printer = "ZDesigner USB", version, endSeq = 2 },
                new { boxNo = 7, closedAt = closedAt.AddMinutes(1).ToString("yyyy-MM-ddTHH:mm:ss"), printer = "ZDesigner USB", version = "old", endSeq = 4 }
            }
        });
        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());
        Assert.True(res.GetProperty("closed").EnumerateArray().All(x => x.GetProperty("offline").GetBoolean()));
        await Task.Delay(300);
        Assert.Empty(printer.Received); // đã in offline: không in lại

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
        var boxes = await db.Packages.Where(p => p.WoName == wo).OrderBy(p => p.PackageNo).ToListAsync();
        Assert.Equal(2, boxes.Count);
        Assert.All(boxes, b => { Assert.Equal("Closed", b.Status); Assert.NotNull(b.PrintedAt); Assert.StartsWith("In khi mất mạng (offline)", b.PrintTarget); });
        Assert.Equal(closedAt.ToString("yyyy-MM-dd HH:mm:ss"), boxes[0].ClosedAt!.Value.ToString("yyyy-MM-dd HH:mm:ss"));
        Assert.DoesNotContain("⚠", boxes[0].PrintTarget);
        Assert.Contains("tem in số box 7, server đánh số 2", boxes[1].PrintTarget);
        Assert.Contains("mẫu tem cũ", boxes[1].PrintTarget);
    }

    // Box chưa đồng bộ trạm tự giữ lúc mất kết nối (đã sửa / xoá / đóng thiếu ở trạm): server đóng từng box đúng số pcs
    // (đã in thì không in lại, chưa in thì in bù), box đang quét = OpenQty; gửi lại lô cũ không cộng đôi
    [Fact]
    public async Task Offline_kept_boxes_are_closed_with_their_qty()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (5402, "BC-5402", 10));
        using var _ = app;
        const string wo = "NM/MO/WO5402", device = "dev-B";
        Assert.True((await Post(c, token, "/toast/api/session/start", new { wo, employeeCode = "SVN0001", confirmed = true, printerId, deviceId = device }))
            .GetProperty("success").GetBoolean());
        string version = (await c.GetFromJsonAsync<JsonElement>("/toast/api/label/offline?wo=" + wo)).GetProperty("label").GetProperty("version").GetString()!;

        // Trạm quét 30 pcs (seq 1..30), sửa ở trạm: box 1 đóng thiếu 7 pcs (đã in), box 2 đủ 10 (chưa in), box đang quét 3 pcs;
        // 10 pcs bị xoá cùng box cũ ở giữa -> adj, server chỉ dùng số pcs từng box + OpenQty
        object Batch(long to) => new
        {
            wo, productId = 5402, barcode = "BC-5402", printerId, deviceId = device, fromSeq = 1, toSeq = to, count = (int)to,
            boxes = new object[]
            {
                new { boxNo = 1, qty = 7, endSeq = 8, closedAt = DateTime.Now.AddMinutes(-2).ToString("yyyy-MM-ddTHH:mm:ss"), printed = true, printer = "ZDesigner USB", version },
                new { boxNo = 2, qty = 10, endSeq = 25, closedAt = (string?)null, printed = false, printer = (string?)null, version = (string?)null }
            },
            openQty = 3
        };
        var res = await Post(c, token, "/toast/api/scan", Batch(30));
        Assert.True(res.GetProperty("success").GetBoolean(), res.ToString());
        Assert.Equal(2, res.GetProperty("closed").GetArrayLength());
        await Task.Delay(300);
        Assert.Single(printer.Received); // box 2 in bù, box 1 đã in ở trạm

        // Gửi lại đúng lô đó (mất phản hồi): không đóng thêm, không cộng
        var again = await Post(c, token, "/toast/api/scan", Batch(30));
        Assert.True(again.GetProperty("success").GetBoolean(), again.ToString());
        Assert.Equal(0, again.GetProperty("closed").GetArrayLength());

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
        var boxes = await db.Packages.Where(p => p.WoName == wo).OrderBy(p => p.PackageNo).ToListAsync();
        Assert.Equal(3, boxes.Count);
        Assert.Equal((7, "Closed"), (boxes[0].Qty, boxes[0].Status));
        Assert.StartsWith("In khi mất mạng (offline)", boxes[0].PrintTarget);
        Assert.Equal((10, "Closed"), (boxes[1].Qty, boxes[1].Status));
        Assert.NotNull(boxes[1].PrintedAt);
        Assert.Equal((3, "Open"), (boxes[2].Qty, boxes[2].Status));

        // Trạm xoá box đang quét (3 pcs) lúc mất kết nối: lô sau báo OpenQty = 0 -> server bỏ box đang mở, như Xoá box
        var deleted = await Post(c, token, "/toast/api/scan", new
        {
            wo, productId = 5402, barcode = "BC-5402", printerId, deviceId = device, fromSeq = 31, toSeq = 31, count = 1,
            boxes = Array.Empty<object>(), openQty = 0
        });
        Assert.True(deleted.GetProperty("success").GetBoolean(), deleted.ToString());
        Assert.False(await db.Packages.AsNoTracking().AnyAsync(p => p.WoName == wo && p.Status == "Open"));
    }

    // Máy khác xác nhận vào làm thì giữ phiên: lô chưa gửi của máy trước bị từ chối (không cộng thừa), máy mới gửi bình thường
    [Fact]
    public async Task Scan_batch_from_previous_device_is_rejected_after_takeover()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (5302, "BC-5302", 10));
        using var _ = app;
        const string wo = "NM/MO/WO5302";
        Task<JsonElement> Start(string device) => Post(c, token, "/toast/api/session/start", new { wo, employeeCode = "SVN0001", confirmed = true, printerId, deviceId = device });
        Task<JsonElement> Batch(string device, long from, long to) => Post(c, token, "/toast/api/scan",
            new { wo, productId = 5302, barcode = "BC-5302", printerId, deviceId = device, fromSeq = from, toSeq = to, count = (int)(to - from + 1) });

        Assert.True((await Start("dev-A")).GetProperty("success").GetBoolean());
        Assert.True((await Batch("dev-A", 1, 3)).GetProperty("success").GetBoolean());
        // Máy B mở lại WO: modal thấy phiên đang ở máy A
        var check = await c.GetFromJsonAsync<JsonElement>("/toast/api/session/check?wo=" + wo);
        Assert.Equal("dev-A", check.GetProperty("session").GetProperty("deviceId").GetString());
        Assert.True((await Start("dev-B")).GetProperty("success").GetBoolean());

        var stale = await Batch("dev-A", 4, 6);
        Assert.False(stale.GetProperty("success").GetBoolean());
        Assert.True(stale.GetProperty("otherDevice").GetBoolean());

        var fromB = await Batch("dev-B", 1, 2);
        Assert.True(fromB.GetProperty("success").GetBoolean(), fromB.ToString());
        Assert.Equal(5, fromB.GetProperty("state").GetProperty("totalPcs").GetInt32()); // 3 của A + 2 của B, không có 3 pcs cũ của A
    }

    // Toast:AutoEndInactiveHours: phiên không hoạt động đủ giờ thì tự kết thúc (box dở giữ lại); quét tiếp thì báo phiên đã kết thúc
    [Fact]
    public async Task Inactive_session_ends_automatically_and_scan_asks_to_confirm_again()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (5201, "BC-5201", 5));
        using var _ = app;
        const string wo = "NM/MO/WO5201";
        int id = (await OpenSession(c, token, wo)).GetProperty("state").GetProperty("session").GetProperty("id").GetInt32();
        await Scan(c, token, wo, 5201, "BC-5201");

        using (var scope = app.Services.CreateScope())
        {
            var sessions = scope.ServiceProvider.GetRequiredService<Toast.Web.Features.Station.SessionService>();
            string by = Toast.Web.Features.Station.SessionAutoEndService.EndedBy(4);
            Assert.Equal("Tự động: không hoạt động 4 giờ", by);
            Assert.Equal("Tự động: không hoạt động 1 giờ 30 phút", Toast.Web.Features.Station.SessionAutoEndService.EndedBy(1.5));
            Assert.Equal("Tự động: không hoạt động 6 phút", Toast.Web.Features.Station.SessionAutoEndService.EndedBy(0.1));

            // Mới không hoạt động 3 giờ: chưa kết thúc
            Assert.DoesNotContain(await sessions.EndInactiveAsync(TimeSpan.FromHours(4), DateTime.Now.AddHours(3), by), x => x.Id == id);
            // Quá 4 giờ: kết thúc, nhập KQSX như kết thúc tay (công tắc tắt -> Off)
            var ended = Assert.Single(await sessions.EndInactiveAsync(TimeSpan.FromHours(4), DateTime.Now.AddHours(4).AddMinutes(1), by), x => x.Id == id);
            Assert.Equal(by, ended.EndedBy);
            Assert.Equal("Off", ended.PushStatus);
        }

        // Quét tiếp: không tính, trạm mở lại modal xác nhận
        var res = await Scan(c, token, wo, 5201, "BC-5201");
        Assert.False(res.GetProperty("success").GetBoolean());
        Assert.True(res.GetProperty("needSession").GetBoolean());

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
            var box = await db.Packages.SingleAsync(p => p.WoName == wo);
            Assert.Equal("Open", box.Status); // box dở giữ lại cho phiên sau
            Assert.Equal(1, box.Qty);
        }
    }

    // Toast:PushProduction tắt (mặc định): kết thúc phiên không gọi Viindoo, PushStatus Off
    [Fact]
    public async Task Push_off_does_not_call_viindoo()
    {
        var mo = new FakeMo("NM/MO/WO5002", planQty: 10);
        extraResponder = mo.Respond;
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (5002, "BC-5002", 2));
        using var _ = app;
        int id = (await OpenSession(c, token, "NM/MO/WO5002")).GetProperty("state").GetProperty("session").GetProperty("id").GetInt32();
        for (int i = 0; i < 2; i++) await Scan(c, token, "NM/MO/WO5002", 5002, "BC-5002");
        var ended = await Post(c, token, $"/toast/api/session/{id}/end", new { pin = Pin, closeOpenBox = false, printerId });
        Assert.Equal("Off", ended.GetProperty("push").GetProperty("status").GetString());
        Assert.Null(mo.Write);
    }

    // 2 line chạy 2 WO khác nhau cùng 1 sản phẩm: mỗi WO 1 nhóm lệnh -> phiên riêng, box dở riêng; hôm sau mở lại mỗi WO làm tiếp đúng box của nó
    [Fact]
    public async Task Two_lines_two_work_orders_same_product_keep_separate_boxes()
    {
        using var printer = new FakePrinter();
        var (c, token, app) = await StartAsync(printer, (4501, "BC-4501", 6));
        using var _ = app;
        const string woA = "NM/MO/WO4501L1", woB = "NM/MO/WO4501L2";

        int sessionA = (await OpenSession(c, token, woA)).GetProperty("state").GetProperty("session").GetProperty("id").GetInt32();
        int sessionB = (await OpenSession(c, token, woB)).GetProperty("state").GetProperty("session").GetProperty("id").GetInt32();
        Assert.NotEqual(sessionA, sessionB);
        for (int i = 0; i < 3; i++) await Scan(c, token, woA, 4501, "BC-4501"); // line 1: box dở 3/6
        await Scan(c, token, woB, 4501, "BC-4501");                              // line 2: box dở 1/6

        // Hôm sau: mỗi WO thấy đúng phiên + box dở của nó
        var checkA = await c.GetFromJsonAsync<JsonElement>("/toast/api/session/check?wo=WO4501L1");
        var checkB = await c.GetFromJsonAsync<JsonElement>("/toast/api/session/check?wo=WO4501L2");
        Assert.Equal(sessionA, checkA.GetProperty("session").GetProperty("id").GetInt32());
        Assert.Equal(3, checkA.GetProperty("session").GetProperty("openBox").GetProperty("qty").GetInt32());
        Assert.Equal(sessionB, checkB.GetProperty("session").GetProperty("id").GetInt32());
        Assert.Equal(1, checkB.GetProperty("session").GetProperty("openBox").GetProperty("qty").GetInt32());

        await OpenSession(c, token, woA);
        JsonElement last = default;
        for (int i = 0; i < 3; i++) last = await Scan(c, token, woA, 4501, "BC-4501");
        Assert.True(last.GetProperty("printed").GetProperty("ok").GetBoolean(), last.ToString()); // line 1 đủ 6/6 -> in tem

        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
        var boxes = await db.Packages.Where(p => p.ProductId == 4501).OrderBy(p => p.WoName).ToListAsync();
        Assert.Equal([(woA, 6, "Closed"), (woB, 1, "Open")], boxes.Select(b => (b.WoGroup, b.Qty, b.Status)));
    }
}
