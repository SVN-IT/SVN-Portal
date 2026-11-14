using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toast.Web.Features.Station;

namespace Toast.Web.Features.ClientLogAdmin;

// Màn Log trạm /toast/admin/client-log: xem log lỗi gửi từ trình duyệt màn công nhân (file logs/client-yyyyMMdd.log, ClientLogService).
// Chỉ đọc. Mỗi lần xem 1 ngày, tối đa 5000 dòng mới nhất; lọc mức / tìm ở trình duyệt.
[Authorize]
[Route("toast/admin/client-log")]
public sealed class ClientLogAdminController(ClientLogService logs) : Controller
{
    const int MaxRows = 5000;

    [HttpGet("")]
    public IActionResult Index() => View();

    // Log của 1 ngày (yyyyMMdd, trống = ngày mới nhất có log) + danh sách ngày có log
    [HttpGet("api")]
    public IActionResult Get(string? day)
    {
        var days = logs.Days();
        // Lỗi ghi file gần nhất từ lúc app chạy (thiếu quyền...): hiện trên màn
        var writeError = logs.LastWriteError == null ? null : new { message = logs.LastWriteError, at = logs.LastWriteErrorAt };
        string? selected = ClientLogService.IsDay(day) ? day : days.FirstOrDefault();
        if (selected == null) return Json(new { success = true, days, day = (string?)null, rows = Array.Empty<object>(), total = 0, limited = false, directory = logs.Directory, writeError, lastWriteAt = logs.LastWriteAt });
        var (rows, total) = logs.Read(selected, MaxRows);
        return Json(new { success = true, days, day = selected, rows, total, limited = total > rows.Count, directory = logs.Directory, writeError, lastWriteAt = logs.LastWriteAt });
    }

    // Tải nguyên file log của ngày
    [HttpGet("download")]
    public IActionResult Download(string day)
    {
        string? path = logs.FilePath(day);
        if (path == null) return NotFound();
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return File(stream, "text/plain; charset=utf-8", $"client-{day}.log");
    }
}
