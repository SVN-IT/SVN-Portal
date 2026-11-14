using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Toast.Web.Data;
using Toast.Web.Features.LabelPrint;
using Toast.Web.Features.Package;
using Toast.Web.Features.Station;

namespace Toast.Web.Features.SessionAdmin;

// Màn phiên làm việc /toast/admin/session (bảng Toast_Session + Toast_SessionConfirm): xem + kết thúc phiên đang mở.
// Box thuộc phiên = box đã đóng trong phiên (Toast_Package.SessionId); box đang dở (Open) của lệnh
// chỉ hiện khi phiên còn mở (box dở được giữ sang phiên sau nên pcs không tách chính xác theo phiên).
// Hoạt động cuối của phiên đang mở = lần cập nhật box mới nhất của lệnh (quét / đóng), hoặc lần xác nhận / mở phiên nếu muộn hơn.
[Authorize]
[Route("toast/admin/session")]
public sealed class SessionAdminController(ToastDbContext db, SessionService sessionService, ProductionPush.ProductionPushService push,
    IOptionsMonitor<LabelVariablesOptions> variables) : Controller
{
    const int MaxRows = 500;

    [HttpGet("")]
    public IActionResult Index() => View();

    // 500 phiên mới nhất + mọi phiên đang mở (để thấy phiên bị treo). Lọc / tìm ở trình duyệt.
    [HttpGet("api")]
    public async Task<IActionResult> GetAll()
    {
        var latest = await db.Sessions.AsNoTracking().OrderByDescending(s => s.Id).Take(MaxRows).ToListAsync();
        var minId = latest.Count == MaxRows ? latest[^1].Id : 0;
        var olderOpen = minId == 0 ? [] : await db.Sessions.AsNoTracking()
            .Where(s => s.Id < minId && s.Status == SessionStatus.Open).ToListAsync();
        var sessions = latest.Concat(olderOpen).ToList();
        var ids = sessions.Select(s => s.Id).ToList();

        var confirms = await db.SessionConfirms.AsNoTracking().Where(c => ids.Contains(c.SessionId))
            .Select(c => new { c.Id, c.SessionId, c.EmployeeCode, c.EmployeeName, c.ConfirmedAt }).ToListAsync();
        var closed = (await db.Packages.AsNoTracking()
                .Where(p => p.SessionId != null && ids.Contains(p.SessionId.Value) && p.Status == PackageStatus.Closed)
                .Select(p => new { SessionId = p.SessionId!.Value, p.Qty }).ToListAsync())
            .GroupBy(p => p.SessionId)
            .ToDictionary(g => g.Key, g => (boxes: g.Count(), pcs: g.Sum(p => p.Qty)));
        var openWos = sessions.Where(s => s.Status == SessionStatus.Open).Select(s => s.WoName).Distinct().ToList();
        var openBoxes = await db.Packages.AsNoTracking().Where(p => p.Status == PackageStatus.Open && openWos.Contains(p.WoName))
            .Select(p => new { p.WoName, p.PackageNo, p.Qty, p.Capacity }).ToListAsync();
        var lastBoxAt = await sessionService.LastBoxActivityAsync(openWos);
        var productIds = sessions.Select(s => s.ProductId).Distinct().ToList();
        var products = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.ProductId))
            .ToDictionaryAsync(p => p.ProductId, p => p.Name);

        var data = sessions.Select(s =>
        {
            var mine = confirms.Where(c => c.SessionId == s.Id).OrderBy(c => c.Id).ToList();
            var last = mine.LastOrDefault();
            var box = closed.GetValueOrDefault(s.Id);
            bool isOpen = s.Status == SessionStatus.Open;
            var open = isOpen ? openBoxes.FirstOrDefault(b => b.WoName == s.WoName) : null;
            return new
            {
                s.Id, s.WoName, s.DefaultCode, productName = products.GetValueOrDefault(s.ProductId) ?? "",
                s.Status, s.StartedAt, s.EndedAt, s.EndedBy,
                s.PushStatus, s.PushQty, s.PushedAt, s.PushMessage,
                confirmCount = mine.Count,
                // Mọi nhân viên đã xác nhận trong phiên (để tìm theo mã / tên)
                employees = mine.Select(c => (c.EmployeeCode + " " + c.EmployeeName).Trim()).Distinct().ToList(),
                lastEmployeeCode = last?.EmployeeCode, lastEmployeeName = last?.EmployeeName, lastConfirmedAt = last?.ConfirmedAt,
                closedBoxes = box.boxes, closedPcs = box.pcs,
                openBox = open == null ? null : new { open.PackageNo, open.Qty, open.Capacity },
                lastActivityAt = isOpen ? SessionService.LastActivity(s, lastBoxAt.GetValueOrDefault(s.WoName), last?.ConfirmedAt) : (DateTime?)null
            };
        }).OrderByDescending(s => s.Id).ToList();
        return Json(new { success = true, data, limited = latest.Count == MaxRows, pushEnabled = push.IsEnabled });
    }

    // Chi tiết 1 phiên: các lần xác nhận (mã NV, tên, điện thoại, máy in, giá trị tem, cảnh báo) + box của phiên
    [HttpGet("api/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var s = await db.Sessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (s == null) return Json(new { success = false, error = "Không tìm thấy phiên." });

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == s.ProductId);
        var confirms = await db.SessionConfirms.AsNoTracking().Where(c => c.SessionId == id).OrderBy(c => c.Id).ToListAsync();
        var boxes = await db.Packages.AsNoTracking()
            .Where(p => (p.SessionId == id && p.Status == PackageStatus.Closed)
                || (s.Status == SessionStatus.Open && p.WoName == s.WoName && p.Status == PackageStatus.Open))
            .OrderBy(p => p.PackageNo).ToListAsync();

        // Nhãn biến tem theo Configs/label-variables.json, không có thì để trống (màn hiện tên biến)
        var labels = variables.CurrentValue.Variables
            .GroupBy(v => v.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Label, StringComparer.OrdinalIgnoreCase);

        return Json(new
        {
            success = true,
            session = new
            {
                s.Id, s.WoName, s.DefaultCode, productName = product?.Name ?? "", s.Status, s.StartedAt, s.EndedAt, s.EndedBy,
                s.ViindooMoId, s.PushStatus, s.PushQty, s.PushedAt, s.PushMessage, s.PushDetail,
                lastActivityAt = s.Status == SessionStatus.Open
                    ? SessionService.LastActivity(s, (await sessionService.LastBoxActivityAsync([s.WoName])).GetValueOrDefault(s.WoName), confirms.LastOrDefault()?.ConfirmedAt)
                    : (DateTime?)null
            },
            confirms = confirms.Select((c, i) => new
            {
                c.Id, no = i + 1, c.ConfirmedAt, c.EmployeeCode, c.EmployeeName, c.EmployeePhone, c.PrintTarget,
                values = c.Values.Select(v => new { key = v.Key, label = labels.GetValueOrDefault(v.Key) ?? "", value = v.Value }),
                warnings = (c.Warnings ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            }),
            boxes = boxes.Select(b => new
            {
                b.Id, b.PackageNo, b.Qty, b.Capacity, b.Status, b.ClosedAt, b.PrintedAt, b.PrintError, b.PrintTarget, b.InputLogId
            })
        });
    }

    // Kết thúc phiên đang mở (VD phiên bị treo, trạm không còn mở lệnh đó). Box dở giữ lại cho phiên sau như kết thúc ở trạm.
    [HttpPost("api/{id:int}/end"), ValidateAntiForgeryToken]
    public async Task<IActionResult> End(int id)
    {
        var (session, _, _, error) = await sessionService.EndAsync(id, closeOpenBox: false, printer: null, endedBy: "Admin " + (User.Identity?.Name ?? ""));
        return session == null ? Json(new { success = false, error })
            : Json(new { success = true, message = $"Đã kết thúc phiên #{id}.", push = new { status = session.PushStatus, message = session.PushMessage } });
    }

    // Nhập KQSX cho phiên đã kết thúc mà chưa nhập (lúc kết thúc đang tắt) hoặc nhập lỗi. Cần Toast:PushProduction đang bật.
    [HttpPost("api/{id:int}/push"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Push(int id)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == id);
        if (session == null) return Json(new { success = false, error = "Không tìm thấy phiên." });
        if (session.Status != SessionStatus.Ended) return Json(new { success = false, error = "Phiên đang mở: kết thúc phiên để nhập KQSX." });
        if (session.PushStatus is not (PushStatus.Off or PushStatus.Error or PushStatus.Mismatch or null))
            return Json(new { success = false, error = "Phiên này đã nhập KQSX hoặc không cần nhập." });
        if (!push.IsEnabled) return Json(new { success = false, error = "Nhập KQSX đang tắt: bật Toast:PushProduction trong appsettings trước." });
        await push.PushAsync(session);
        return Json(new { success = true, push = new { status = session.PushStatus, message = session.PushMessage } });
    }
}
