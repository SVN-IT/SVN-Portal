using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Toast.Web.Data;
using Toast.Web.Features.Viindoo;

namespace Toast.Web.Features.Product;

// Màn quản lý quy cách sản phẩm (bảng Toast_Product), truy cập tại /toast/admin/product
[Authorize]
[Route("toast/admin/product")]
public sealed class ProductController(ToastDbContext db, ViindooRpc viindoo) : Controller
{
    static readonly string[] OuterTypes = ["Carton", "Plate"];

    // ===== Trang =====

    [HttpGet("")]
    public IActionResult Index() => View(OuterTypes);

    // ===== API JSON =====

    [HttpGet("api")]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var data = await db.Products.AsNoTracking().OrderBy(p => p.DefaultCode).ToListAsync();
            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = "Không tải được danh sách sản phẩm: " + ex.Message });
        }
    }

    [HttpGet("api/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var data = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        return data == null
            ? Json(new { success = false, error = "Không tìm thấy sản phẩm." })
            : Json(new { success = true, data });
    }

    [HttpPost("api"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] ProductRequest req)
    {
        var data = Normalize(req);
        string? error = Validate(data) ?? await DuplicateError(data, null);
        if (error != null)
            return Json(new { success = false, error });

        data.CreatedAt = DateTime.Now;
        db.Products.Add(data);
        await db.SaveChangesAsync();
        return Json(new { success = true, id = data.Id, message = "Tạo mới thành công" });
    }

    [HttpPost("api/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, [FromBody] ProductRequest req)
    {
        var current = await db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (current == null)
            return Json(new { success = false, error = "Không tìm thấy sản phẩm, có thể đã bị xoá." });

        var data = Normalize(req);
        string? error = Validate(data) ?? await DuplicateError(data, id);
        if (error != null)
            return Json(new { success = false, error });

        current.ProductId = data.ProductId;
        current.DefaultCode = data.DefaultCode;
        current.Name = data.Name;
        current.ScanSource = data.ScanSource;
        current.Barcode = data.Barcode;
        current.PcsPerBox = data.PcsPerBox;
        current.OuterType = data.OuterType;
        current.BoxPerOuter = data.BoxPerOuter;
        current.ViindooQtyPerCode = data.ViindooQtyPerCode;
        current.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Cập nhật thành công" });
    }

    [HttpPost("api/{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var current = await db.Products.FirstOrDefaultAsync(p => p.Id == id);
        if (current == null)
            return Json(new { success = false, error = "Không tìm thấy sản phẩm, có thể đã bị xoá." });

        db.Products.Remove(current);
        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Xoá thành công" });
    }

    // Điền sẵn form theo mã sản phẩm (default_code): product.product id, tên, x_quantity_per_code. Đọc thẳng Viindoo, chỉ đọc.
    [HttpGet("api/viindoo")]
    public async Task<IActionResult> FromViindoo(string code)
    {
        try
        {
            var (product, error) = await viindoo.FindProductByCodeAsync(code);
            if (product == null) return Json(new { success = false, error });

            var existing = await db.Products.AsNoTracking().Where(p => p.ProductId == product.ProductId)
                .Select(p => new { p.Id }).FirstOrDefaultAsync();
            return Json(new
            {
                success = true,
                product.ProductId,
                product.DefaultCode,
                product.Name,
                viindooQtyPerCode = product.QtyPerCode,
                existingId = existing?.Id
            });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = "Không lấy được dữ liệu từ Viindoo: " + ex.Message });
        }
    }

    // Tra mã sản phẩm (default code) từ 1 WO, kể cả WO đã hoàn thành. Màn hình điền mã rồi đọc tiếp theo mã. Chỉ đọc.
    [HttpGet("api/viindoo-wo")]
    public async Task<IActionResult> CodeFromWo(string wo)
    {
        try
        {
            var (info, error) = await viindoo.GetWorkOrderAsync(wo, allowClosed: true);
            if (info == null) return Json(new { success = false, error });
            if (info["product_code"].Length == 0)
                return Json(new { success = false, error = $"Sản phẩm của {info["name"]} không có mã (default code) trên Viindoo." });
            return Json(new { success = true, wo = info["name"], defaultCode = info["product_code"], productName = info["product_name"] });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = "Không lấy được dữ liệu từ Viindoo: " + ex.Message });
        }
    }

    // ===== Helpers =====

    static Product Normalize(ProductRequest req)
    {
        string scan = (req.ScanSource ?? "").Trim();
        scan = ScanSources.All.FirstOrDefault(x => string.Equals(x, scan, StringComparison.OrdinalIgnoreCase))
            ?? (scan.Length == 0 ? ScanSources.Barcode : scan);
        string? outer = OuterTypes.FirstOrDefault(t => string.Equals(t, (req.OuterType ?? "").Trim(), StringComparison.OrdinalIgnoreCase));
        return new Product
        {
            ProductId = req.ProductId,
            DefaultCode = (req.DefaultCode ?? "").Trim(),
            Name = (req.Name ?? "").Trim(),
            ScanSource = scan,
            // Quét theo Lot ID thì không dùng barcode cố định
            Barcode = scan == ScanSources.LotId ? "" : (req.Barcode ?? "").Trim(),
            PcsPerBox = req.PcsPerBox,
            // Giá trị lạ giữ nguyên để Validate báo lỗi; không có cấp 2 thì bỏ số box
            OuterType = outer ?? (string.IsNullOrWhiteSpace(req.OuterType) ? null : req.OuterType.Trim()),
            BoxPerOuter = string.IsNullOrWhiteSpace(req.OuterType) ? null : req.BoxPerOuter,
            ViindooQtyPerCode = req.ViindooQtyPerCode is > 0 ? req.ViindooQtyPerCode : null
        };
    }

    static string? Validate(Product p)
    {
        if (p.ProductId <= 0)
            return "Vui lòng nhập Viindoo product ID (số dương).";
        if (p.DefaultCode.Length == 0)
            return "Vui lòng nhập mã sản phẩm.";
        if (p.DefaultCode.Length > 50)
            return "Mã sản phẩm tối đa 50 ký tự.";
        if (p.Name.Length > 255)
            return "Tên sản phẩm tối đa 255 ký tự.";
        if (!ScanSources.All.Contains(p.ScanSource))
            return "Mã quét pcs chỉ được là Barcode cố định hoặc Lot ID.";
        if (p.ScanSource == ScanSources.Barcode && p.Barcode.Length == 0)
            return "Vui lòng nhập barcode pcs.";
        if (p.Barcode.Length > 50)
            return "Barcode tối đa 50 ký tự.";
        if (p.PcsPerBox <= 0)
            return "Số pcs / box phải lớn hơn 0.";
        if (p.OuterType != null && !OuterTypes.Contains(p.OuterType))
            return "Cấp 2 chỉ được là Carton hoặc Plate.";
        if (p.OuterType != null && p.BoxPerOuter is not > 0)
            return $"Số box / {p.OuterType} phải lớn hơn 0.";
        return null;
    }

    // Mỗi sản phẩm Viindoo chỉ có 1 quy cách (DB cũng có unique index chặn lần cuối)
    async Task<string?> DuplicateError(Product p, int? exceptId)
    {
        var other = await db.Products.AsNoTracking()
            .Where(x => x.Id != exceptId && x.ProductId == p.ProductId)
            .Select(x => x.DefaultCode).FirstOrDefaultAsync();
        return other == null ? null : $"Viindoo product ID {p.ProductId} đã có quy cách ({other}).";
    }
}
