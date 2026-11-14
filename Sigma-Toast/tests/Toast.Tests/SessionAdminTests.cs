using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Toast.Web.Data;
using Toast.Web.Features.Package;
using Toast.Web.Features.Station;

namespace Toast.Tests;

/// <summary>Màn phiên /toast/admin/session: danh sách + chi tiết (lần xác nhận có tên / điện thoại, giá trị tem, cảnh báo; box của phiên), hoạt động cuối, kết thúc phiên.</summary>
public class SessionAdminTests(ToastFactory factory) : IClassFixture<ToastFactory>
{
    const string Api = "/toast/admin/session/api";

    [Fact]
    public async Task Lists_sessions_and_shows_confirms_and_boxes()
    {
        var (client, _) = await factory.LoginAsync("/toast/admin/session");
        Assert.Contains("js/session/session.js", await client.GetStringAsync("/toast/admin/session"));

        int openId, endedId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
            var now = DateTime.Now;
            var ended = new Session { WoName = "NM/MO/05001", WoGroup = "NM/MO/05001", ProductId = 5001, DefaultCode = "PN-5001", Status = SessionStatus.Ended, StartedAt = now.AddHours(-5), EndedAt = now.AddHours(-4), EndedBy = "leader" };
            var open = new Session { WoName = "NM/MO/05002", WoGroup = "NM/MO/05002", ProductId = 5002, DefaultCode = "PN-5002", Status = SessionStatus.Open, StartedAt = now.AddHours(-2) };
            db.Sessions.AddRange(ended, open);
            await db.SaveChangesAsync();

            var c1 = new SessionConfirm
            {
                SessionId = open.Id, EmployeeCode = "SVN0215", EmployeeName = "Nguyễn Văn A", EmployeePhone = "0225.366.8818", PrintTarget = "Server TCP · P1",
                ConfirmedAt = now.AddHours(-2), Values = new() { ["PO"] = "125", ["LOT_ID"] = "PVT" }, Warnings = "Cảnh báo 1\nCảnh báo 2"
            };
            var c2 = new SessionConfirm { SessionId = open.Id, EmployeeCode = "SVN0388", ConfirmedAt = now.AddHours(-1), Values = new() { ["PO"] = "125" } };
            var c3 = new SessionConfirm { SessionId = ended.Id, EmployeeCode = "SVN0001", ConfirmedAt = now.AddHours(-5) };
            db.SessionConfirms.AddRange(c1, c2, c3);
            await db.SaveChangesAsync();

            Package Box(string wo, int no, int qty, string status, int? session) => new()
            {
                WoName = wo, WoGroup = wo, ProductId = 5002, DefaultCode = "PN-5002", PackageNo = no, Qty = qty, Capacity = 10, Status = status,
                SessionId = session, CreatedAt = now, ClosedAt = status == PackageStatus.Closed ? now : null, PrintedAt = status == PackageStatus.Closed ? now : null
            };
            db.Packages.AddRange(Box("NM/MO/05002", 1, 10, PackageStatus.Closed, open.Id), Box("NM/MO/05002", 2, 10, PackageStatus.Closed, open.Id),
                Box("NM/MO/05002", 3, 4, PackageStatus.Open, null), Box("NM/MO/05001", 1, 7, PackageStatus.Closed, ended.Id));
            await db.SaveChangesAsync();
            (openId, endedId) = (open.Id, ended.Id);
        }

        var list = await client.GetFromJsonAsync<JsonElement>(Api);
        var rows = list.GetProperty("data").EnumerateArray().ToList();
        var openRow = rows.Single(r => r.GetProperty("id").GetInt32() == openId);
        Assert.Equal(2, openRow.GetProperty("confirmCount").GetInt32());
        Assert.Equal("SVN0388", openRow.GetProperty("lastEmployeeCode").GetString());
        Assert.Equal(2, openRow.GetProperty("closedBoxes").GetInt32());
        Assert.Equal(20, openRow.GetProperty("closedPcs").GetInt32());
        Assert.Equal(4, openRow.GetProperty("openBox").GetProperty("qty").GetInt32());
        Assert.Contains("SVN0215 Nguyễn Văn A", openRow.GetProperty("employees").EnumerateArray().Select(e => e.GetString()));
        var endedRow = rows.Single(r => r.GetProperty("id").GetInt32() == endedId);
        Assert.Equal(JsonValueKind.Null, endedRow.GetProperty("openBox").ValueKind);
        Assert.Equal(1, endedRow.GetProperty("closedBoxes").GetInt32());

        var detail = await client.GetFromJsonAsync<JsonElement>($"{Api}/{openId}");
        var confirms = detail.GetProperty("confirms").EnumerateArray().ToList();
        Assert.Equal(2, confirms.Count);
        Assert.Equal("Nguyễn Văn A", confirms[0].GetProperty("employeeName").GetString());
        Assert.Equal("0225.366.8818", confirms[0].GetProperty("employeePhone").GetString());
        Assert.Equal(["Cảnh báo 1", "Cảnh báo 2"], confirms[0].GetProperty("warnings").EnumerateArray().Select(w => w.GetString()));
        Assert.Contains(confirms[0].GetProperty("values").EnumerateArray(), v => v.GetProperty("key").GetString() == "LOT_ID" && v.GetProperty("value").GetString() == "PVT");
        Assert.Equal(JsonValueKind.Null, confirms[1].GetProperty("employeeName").ValueKind);
        // Box của phiên: 2 box đã đóng + box đang dở của WO; box của phiên khác không có
        var boxes = detail.GetProperty("boxes").EnumerateArray().ToList();
        Assert.Equal([1, 2, 3], boxes.Select(b => b.GetProperty("packageNo").GetInt32()));
        Assert.Equal("Open", boxes[2].GetProperty("status").GetString());

        var missing = await client.GetFromJsonAsync<JsonElement>($"{Api}/999999");
        Assert.False(missing.GetProperty("success").GetBoolean());
    }

    // Phiên đang mở: hoạt động cuối = lần cập nhật box mới nhất của lệnh (hoặc lần xác nhận nếu muộn hơn). Admin kết thúc phiên: box dở giữ lại.
    [Fact]
    public async Task Shows_last_activity_and_admin_can_end_open_session()
    {
        var (client, token) = await factory.LoginAsync("/toast/admin/session");
        var now = DateTime.Now;
        var boxAt = now.AddMinutes(-90);
        int id, boxId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
            var session = new Session { WoName = "NM/MO/05101", WoGroup = "NM/MO/05101", ProductId = 5101, DefaultCode = "PN-5101", Status = SessionStatus.Open, StartedAt = now.AddHours(-3) };
            db.Sessions.Add(session);
            await db.SaveChangesAsync();
            db.SessionConfirms.Add(new SessionConfirm { SessionId = session.Id, EmployeeCode = "SVN0001", ConfirmedAt = now.AddHours(-2) });
            var box = new Package
            {
                WoName = "NM/MO/05101", WoGroup = "NM/MO/05101", ProductId = 5101, DefaultCode = "PN-5101", PackageNo = 1, Qty = 3, Capacity = 10,
                Status = PackageStatus.Open, CreatedAt = now.AddHours(-3), UpdatedAt = boxAt
            };
            db.Packages.Add(box);
            await db.SaveChangesAsync();
            (id, boxId) = (session.Id, box.Id);
        }

        var row = (await client.GetFromJsonAsync<JsonElement>(Api)).GetProperty("data").EnumerateArray().Single(r => r.GetProperty("id").GetInt32() == id);
        Assert.Equal(boxAt, row.GetProperty("lastActivityAt").GetDateTime(), TimeSpan.FromSeconds(1));
        var detail = await client.GetFromJsonAsync<JsonElement>($"{Api}/{id}");
        Assert.Equal(boxAt, detail.GetProperty("session").GetProperty("lastActivityAt").GetDateTime(), TimeSpan.FromSeconds(1));

        var end = await ToastFactory.PostAsync(client, token, $"{Api}/{id}/end", new { });
        Assert.True(end.GetProperty("success").GetBoolean(), end.ToString());
        var ended = await client.GetFromJsonAsync<JsonElement>($"{Api}/{id}");
        Assert.Equal("Ended", ended.GetProperty("session").GetProperty("status").GetString());
        Assert.StartsWith("Admin", ended.GetProperty("session").GetProperty("endedBy").GetString());
        Assert.Equal(JsonValueKind.Null, ended.GetProperty("session").GetProperty("lastActivityAt").ValueKind);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
            Assert.Equal(PackageStatus.Open, (await db.Packages.FindAsync(boxId))!.Status);
        }
        // Kết thúc lại: báo phiên không còn mở
        Assert.False((await ToastFactory.PostAsync(client, token, $"{Api}/{id}/end", new { })).GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Requires_login()
    {
        var res = await factory.CreateClient(new() { AllowAutoRedirect = false }).GetAsync("/toast/admin/session");
        Assert.True((int)res.StatusCode is 302 or 401);
    }
}
