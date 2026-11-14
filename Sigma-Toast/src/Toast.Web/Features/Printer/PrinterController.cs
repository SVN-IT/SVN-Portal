using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Toast.Web.Data;
using Toast.Web.Features.LabelPrint;

namespace Toast.Web.Features.Printer;

// Màn quản lý máy in (bảng Toast_Printer), truy cập tại /toast/admin/printer
[Authorize]
[Route("toast/admin/printer")]
public sealed class PrinterController(ToastDbContext db) : Controller
{
    static readonly int[] DpiOptions = [203, 300, 600];

    // ===== Trang =====

    [HttpGet("")]
    public IActionResult Index() => View(DpiOptions);

    // ===== API JSON =====

    [HttpGet("api")]
    public async Task<IActionResult> GetAll()
    {
        var data = await db.Printers.AsNoTracking().OrderBy(p => p.Name).ToListAsync();
        return Json(new { success = true, data });
    }

    [HttpGet("api/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var data = await db.Printers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        return data == null ? Json(new { success = false, error = "Không tìm thấy máy in." }) : Json(new { success = true, data });
    }

    [HttpPost("api"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] PrinterRequest req)
    {
        var data = Normalize(req);
        string? error = Validate(data) ?? await DuplicateError(data, null);
        if (error != null) return Json(new { success = false, error });

        data.CreatedAt = DateTime.Now;
        db.Printers.Add(data);
        await db.SaveChangesAsync();
        return Json(new { success = true, id = data.Id, message = "Tạo mới thành công" });
    }

    [HttpPost("api/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, [FromBody] PrinterRequest req)
    {
        var current = await db.Printers.FirstOrDefaultAsync(p => p.Id == id);
        if (current == null) return Json(new { success = false, error = "Không tìm thấy máy in, có thể đã bị xoá." });

        var data = Normalize(req);
        string? error = Validate(data) ?? await DuplicateError(data, id);
        if (error != null) return Json(new { success = false, error });

        current.Code = data.Code;
        current.Name = data.Name;
        current.Ip = data.Ip;
        current.Port = data.Port;
        current.Dpi = data.Dpi;
        current.Location = data.Location;
        current.IsActive = data.IsActive;
        current.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Cập nhật thành công" });
    }

    // Chỉ xoá máy chưa in box nào; máy đã dùng thì chuyển Ngừng dùng để giữ lịch sử box
    [HttpPost("api/{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var current = await db.Printers.FirstOrDefaultAsync(p => p.Id == id);
        if (current == null) return Json(new { success = false, error = "Không tìm thấy máy in, có thể đã bị xoá." });

        int boxCount = await db.Packages.CountAsync(p => p.PrinterId == id);
        if (boxCount > 0)
            return Json(new { success = false, error = $"Máy in {current.Code} đã in {boxCount} box, không xoá được. Hãy chuyển sang Ngừng dùng." });

        db.Printers.Remove(current);
        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Xoá thành công" });
    }

    // Kiểm tra máy in có phản hồi không (chỉ mở/đóng kết nối, không in), dùng IP/Port đang nhập trong form
    [HttpPost("api/test"), ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection([FromBody] PrinterTestRequest req)
    {
        string ip = (req.Ip ?? "").Trim();
        if (!PrinterLookup.Ipv4Regex.IsMatch(ip)) return Json(new { success = false, error = "Địa chỉ IP không hợp lệ." });
        if (req.Port < 1 || req.Port > 65535) return Json(new { success = false, error = "Port phải từ 1 đến 65535." });

        var result = await TcpPrintService.TestAsync(ip, req.Port);
        return Json(new { success = result.Ok, error = result.Error, elapsedMs = result.ElapsedMs });
    }

    // ===== Helpers =====

    static Printer Normalize(PrinterRequest req) => new()
    {
        Code = (req.Code ?? "").Trim().ToUpperInvariant(),
        Name = (req.Name ?? "").Trim(),
        Ip = (req.Ip ?? "").Trim(),
        Port = req.Port,
        Dpi = req.Dpi is > 0 ? req.Dpi : null,
        Location = string.IsNullOrWhiteSpace(req.Location) ? null : req.Location.Trim(),
        IsActive = req.IsActive
    };

    static string? Validate(Printer p)
    {
        if (p.Code.Length == 0) return "Vui lòng nhập mã máy in.";
        if (p.Code.Length > 50) return "Mã máy in tối đa 50 ký tự.";
        if (p.Name.Length == 0) return "Vui lòng nhập tên máy in.";
        if (p.Name.Length > 100) return "Tên máy in tối đa 100 ký tự.";
        if (!PrinterLookup.Ipv4Regex.IsMatch(p.Ip)) return "Địa chỉ IP không hợp lệ (VD 192.168.1.50).";
        if (p.Port < 1 || p.Port > 65535) return "Port phải từ 1 đến 65535.";
        if (p.Dpi != null && !DpiOptions.Contains(p.Dpi.Value)) return "DPI chỉ được là 203, 300 hoặc 600.";
        if (p.Location?.Length > 100) return "Vị trí tối đa 100 ký tự.";
        return null;
    }

    // Mã máy in không trùng (DB cũng có unique index chặn lần cuối)
    async Task<string?> DuplicateError(Printer p, int? exceptId)
    {
        var other = await db.Printers.AsNoTracking()
            .Where(x => x.Id != exceptId && x.Code == p.Code)
            .Select(x => x.Name).FirstOrDefaultAsync();
        return other == null ? null : $"Mã {p.Code} đã dùng cho máy in {other}.";
    }
}
