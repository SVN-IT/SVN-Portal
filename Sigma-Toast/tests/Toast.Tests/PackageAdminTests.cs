using Microsoft.EntityFrameworkCore;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Toast.Web.Data;
using Toast.Web.Features.Package;

namespace Toast.Tests;

/// <summary>Màn quản lý kiện /toast/admin/package: sửa / xoá (không thêm), ràng buộc 1 box Open và số box không trùng trong WO.</summary>
public class PackageAdminTests(ToastFactory factory) : IClassFixture<ToastFactory>
{
    const string Api = "/toast/admin/package/api";

    // Kiện chỉ sinh ra từ màn công nhân; test tạo thẳng trong DB
    async Task<int> AddAsync(int no, string status)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
        var package = new Package
        {
            WoName = "NM/MO/04001", ProductId = 4001, DefaultCode = "PN-4001", PackageNo = no, Qty = 6, Capacity = 6,
            Status = status, CreatedAt = DateTime.Now, ClosedAt = status == PackageStatus.Closed ? DateTime.Now : null
        };
        db.Packages.Add(package);
        await db.SaveChangesAsync();
        return package.Id;
    }

    [Fact]
    public async Task Edit_and_delete_with_rules()
    {
        var (client, token) = await factory.LoginAsync("/toast/admin/package");
        int id = await AddAsync(1, PackageStatus.Closed);
        await AddAsync(2, PackageStatus.Open);

        Assert.Contains("js/package/package.js", await client.GetStringAsync("/toast/admin/package"));
        var one = (await client.GetFromJsonAsync<JsonElement>($"{Api}/{id}")).GetProperty("data");
        Assert.Equal(1, one.GetProperty("packageNo").GetInt32());

        // Không có API thêm kiện
        var add = await client.PostAsJsonAsync(Api, new { woName = "NM/MO/04001", productId = 4001, qty = 1 });
        Assert.False(add.IsSuccessStatusCode);

        var reopen = await ToastFactory.PostAsync(client, token, $"{Api}/{id}", new { packageNo = 1, qty = 6, capacity = 6, status = "Open" });
        Assert.Contains("đang quét", reopen.GetProperty("error").GetString());

        var dupNo = await ToastFactory.PostAsync(client, token, $"{Api}/{id}", new { packageNo = 2, qty = 6, capacity = 6, status = "Closed" });
        Assert.Contains("đã có box số 2", dupNo.GetProperty("error").GetString());

        var updated = await ToastFactory.PostAsync(client, token, $"{Api}/{id}", new { packageNo = 1, qty = 7, capacity = 6, status = "Closed" });
        Assert.True(updated.GetProperty("success").GetBoolean(), updated.ToString());

        var list = await client.GetFromJsonAsync<JsonElement>($"{Api}?q=04001");
        Assert.Equal(2, list.GetProperty("data").GetArrayLength());

        Assert.True((await ToastFactory.PostAsync(client, token, $"{Api}/{id}/delete", new { })).GetProperty("success").GetBoolean());
    }

    // Sửa / xoá kiện ở màn admin thì sửa / xoá dòng log dashboard của kiện (InputLogId)
    [Fact]
    public async Task Edit_and_delete_keep_dashboard_log_in_sync()
    {
        var (client, token) = await factory.LoginAsync("/toast/admin/package");
        int a, b, logA, logB;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
            var la = new ProductionInputLog { ProductId = 4101, ProductQty = 6, WoCode = "NM/MO/04101", Status = ProductionInputLog.ToastStatus };
            var lb = new ProductionInputLog { ProductId = 4101, ProductQty = 6, WoCode = "NM/MO/04101", Status = ProductionInputLog.ToastStatus };
            db.ProductionInputLogs.AddRange(la, lb);
            await db.SaveChangesAsync();
            Package P(int no, int logId) => new()
            {
                WoName = "NM/MO/04101", WoGroup = "NM/MO/04101", ProductId = 4101, DefaultCode = "PN-4101", PackageNo = no, Qty = 6, Capacity = 6,
                Status = PackageStatus.Closed, CreatedAt = DateTime.Now, ClosedAt = DateTime.Now, InputLogId = logId
            };
            var pa = P(1, la.Id); var pb = P(2, lb.Id);
            db.Packages.AddRange(pa, pb);
            await db.SaveChangesAsync();
            (a, b, logA, logB) = (pa.Id, pb.Id, la.Id, lb.Id);
        }

        // Sửa số pcs: dòng log đổi theo
        var updated = await ToastFactory.PostAsync(client, token, $"{Api}/{a}", new { packageNo = 1, qty = 5, capacity = 6, status = "Closed" });
        Assert.True(updated.GetProperty("success").GetBoolean(), updated.ToString());
        using (var scope = factory.Services.CreateScope())
            Assert.Equal(5m, (await scope.ServiceProvider.GetRequiredService<ToastDbContext>().ProductionInputLogs.SingleAsync(l => l.Id == logA)).ProductQty);

        // Mở lại kiện (Đang quét): xoá dòng log, kiện không còn InputLogId
        var reopened = await ToastFactory.PostAsync(client, token, $"{Api}/{a}", new { packageNo = 1, qty = 5, capacity = 6, status = "Open" });
        Assert.True(reopened.GetProperty("success").GetBoolean(), reopened.ToString());
        // Xoá kiện: xoá dòng log
        var deleted = await ToastFactory.PostAsync(client, token, $"{Api}/{b}/delete", new { });
        Assert.True(deleted.GetProperty("success").GetBoolean(), deleted.ToString());

        using var check = factory.Services.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<ToastDbContext>();
        Assert.False(await db2.ProductionInputLogs.AnyAsync(l => l.Id == logA || l.Id == logB));
        Assert.Null((await db2.Packages.SingleAsync(p => p.Id == a)).InputLogId);
    }

    // Đóng kiện ở admin (không in): gắn lần xác nhận gần nhất của phiên đang mở của nhóm lệnh, giống đóng ở trạm
    [Fact]
    public async Task Closing_in_admin_attaches_latest_session_confirm()
    {
        var (client, token) = await factory.LoginAsync("/toast/admin/package");
        int boxId, sessionId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ToastDbContext>();
            var session = new Toast.Web.Features.Station.Session { WoName = "NM/MO/04201-002", WoGroup = "NM/MO/04201", ProductId = 4201, DefaultCode = "PN-4201", Status = "Open", StartedAt = DateTime.Now };
            db.Sessions.Add(session);
            await db.SaveChangesAsync();
            var c1 = new Toast.Web.Features.Station.SessionConfirm { SessionId = session.Id, EmployeeCode = "SVN0001", ConfirmedAt = DateTime.Now.AddHours(-1) };
            var c2 = new Toast.Web.Features.Station.SessionConfirm { SessionId = session.Id, EmployeeCode = "SVN0002", ConfirmedAt = DateTime.Now };
            db.SessionConfirms.AddRange(c1, c2);
            var box = new Package { WoName = "NM/MO/04201-002", WoGroup = "NM/MO/04201", ProductId = 4201, DefaultCode = "PN-4201", PackageNo = 1, Qty = 3, Capacity = 6, Status = PackageStatus.Open, CreatedAt = DateTime.Now };
            db.Packages.Add(box);
            await db.SaveChangesAsync();
            (boxId, sessionId) = (box.Id, session.Id);
        }

        var closed = await ToastFactory.PostAsync(client, token, $"{Api}/{boxId}", new { packageNo = 1, qty = 3, capacity = 6, status = "Closed" });
        Assert.True(closed.GetProperty("success").GetBoolean(), closed.ToString());
        using var check = factory.Services.CreateScope();
        var saved = await check.ServiceProvider.GetRequiredService<ToastDbContext>().Packages.SingleAsync(p => p.Id == boxId);
        Assert.Equal(sessionId, saved.SessionId);
        Assert.Null(saved.PrintedAt); // admin không in tem
    }
}
