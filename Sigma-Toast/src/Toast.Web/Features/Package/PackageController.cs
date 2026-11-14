using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Toast.Web.Data;

namespace Toast.Web.Features.Package;

// Dữ liệu form Sửa kiện ở /toast/admin/package
public class PackageRequest
{
    public int PackageNo { get; set; }
    public int Qty { get; set; }
    public int Capacity { get; set; }
    public string? Status { get; set; }
}

// Màn quản lý kiện đóng gói (bảng Toast_Package), truy cập tại /toast/admin/package. Chỉ sửa / xoá, không thêm, không in.
// Sửa / xoá kiện thì sửa / xoá luôn dòng SVN_ProductionInputLogs (dashboard) của kiện (PackageService.SyncDashboardLogAsync).
[Authorize]
[Route("toast/admin/package")]
public sealed class PackageController(ToastDbContext db, PackageService packages) : Controller
{
    const int MaxRows = 2000;

    [HttpGet("")]
    public IActionResult Index() => View();

    // Danh sách mới nhất trước; q lọc theo WO / mã sản phẩm (không phân biệt hoa thường)
    [HttpGet("api")]
    public async Task<IActionResult> GetAll(string? q)
    {
        string term = (q ?? "").Trim().ToUpperInvariant();
        var query = db.Packages.AsNoTracking();
        if (term.Length > 0)
            query = query.Where(p => p.WoName.ToUpper().Contains(term) || p.DefaultCode.ToUpper().Contains(term));
        var data = await query.OrderByDescending(p => p.Id).Take(MaxRows).ToListAsync();
        // Mã máy in đã in từng kiện (gồm cả máy đã ngừng dùng)
        var printers = await db.Printers.AsNoTracking().ToDictionaryAsync(p => p.Id, p => p.Code);
        return Json(new { success = true, data, printers, limited = data.Count == MaxRows });
    }

    [HttpGet("api/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var data = await db.Packages.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        return data == null ? Json(new { success = false, error = "Không tìm thấy kiện." }) : Json(new { success = true, data });
    }

    [HttpPost("api/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, [FromBody] PackageRequest req)
    {
        var package = await db.Packages.FirstOrDefaultAsync(p => p.Id == id);
        if (package == null) return Json(new { success = false, error = "Không tìm thấy kiện, có thể đã bị xoá." });

        string? oldStatus = package.Status;
        package.PackageNo = req.PackageNo;
        package.Qty = req.Qty;
        package.Capacity = req.Capacity;
        package.Status = req.Status ?? "";
        if (oldStatus != PackageStatus.Closed && package.Status == PackageStatus.Closed) package.ClosedAt = DateTime.Now;
        if (package.Status == PackageStatus.Open) package.ClosedAt = null;

        string? error = await Validate(package, id);
        if (error != null) return Json(new { success = false, error });

        package.UpdatedAt = DateTime.Now;
        // Dòng log dashboard theo kiện: còn đóng thì sửa số pcs, mở lại thì xoá; vừa chuyển sang đóng thì ghi dòng mới (nếu bật công tắc)
        await packages.SyncDashboardLogAsync(package);
        bool justClosed = oldStatus != PackageStatus.Closed && package.Status == PackageStatus.Closed;
        // Đóng ở admin (không in tem): gắn phiên đang mở của lệnh như đóng ở trạm; không có phiên mở thì giữ phiên cũ của kiện (nếu có)
        if (justClosed && await packages.OpenSessionIdAsync(package) is int sessionId) package.SessionId = sessionId;
        var log = justClosed ? packages.AddDashboardLogIfEnabled(package) : null;
        await db.SaveChangesAsync();
        if (log != null)
        {
            package.InputLogId = log.Id;
            await db.SaveChangesAsync();
        }
        return Json(new { success = true, message = "Cập nhật thành công" });
    }

    [HttpPost("api/{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var package = await db.Packages.FirstOrDefaultAsync(p => p.Id == id);
        if (package == null) return Json(new { success = false, error = "Không tìm thấy kiện, có thể đã bị xoá." });
        await packages.SyncDashboardLogAsync(package, deleted: true); // xoá luôn dòng log dashboard của kiện
        db.Packages.Remove(package);
        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Xoá thành công" });
    }

    async Task<string?> Validate(Package package, int? exceptId)
    {
        if (package.PackageNo <= 0) return "Số box phải lớn hơn 0.";
        if (package.Qty < 0) return "Số pcs không được âm.";
        if (package.Capacity <= 0) return "Pcs / box phải lớn hơn 0.";
        if (!PackageStatus.All.Contains(package.Status)) return "Trạng thái chỉ được là Open hoặc Closed.";
        if (await db.Packages.AnyAsync(p => p.Id != exceptId && p.WoName == package.WoName && p.PackageNo == package.PackageNo))
            return $"{package.WoName} đã có box số {package.PackageNo}.";
        // Mỗi lệnh 1 box đang quét (trạm quét vào box Open của lệnh). Các lệnh khác trong nhóm (VD box của -001 mở lại) không chặn
        if (package.Status == PackageStatus.Open && await db.Packages.AnyAsync(p => p.Id != exceptId && p.Status == PackageStatus.Open
                && p.WoName == package.WoName))
            return $"{package.WoName} đã có 1 box đang quét (Open).";
        return null;
    }
}
