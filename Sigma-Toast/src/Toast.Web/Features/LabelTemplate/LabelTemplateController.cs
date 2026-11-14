using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Toast.Web.Data;
using Toast.Web.Features.LabelPrint;

namespace Toast.Web.Features.LabelTemplate;

// Màn quản lý mẫu tem (bảng Toast_LabelTemplate), truy cập tại /toast/admin/label-template.
// Chuyển từ Twist: SVN-Portal/Areas/TwistAdmin/Controllers/LabelTemplateController.cs
[Authorize]
[Route("toast/admin/label-template")]
public sealed class LabelTemplateController(
    ToastDbContext db,
    IOptionsMonitor<LabelTemplateOptions> options,
    IOptionsMonitor<LabelVariablesOptions> variablesConfig) : Controller
{
    const int DefaultCodeMaxLength = 50;

    // ===== Trang =====

    [HttpGet("")]
    public IActionResult Index() => View(options.CurrentValue);

    // ===== API JSON =====

    // Danh sách: chỉ lấy 80 ký tự đầu của ZPL để xem trước
    [HttpGet("api")]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            var data = await db.LabelTemplates.AsNoTracking()
                .OrderBy(t => t.DefaultCode).ThenBy(t => t.Type).ThenBy(t => t.Operation)
                .Select(t => new
                {
                    t.Id, t.DefaultCode, t.Type, t.Operation, t.Size, t.Dpi, t.IsLocked, t.Variables,
                    ZplData = t.ZplData.Substring(0, Math.Min(80, t.ZplData.Length)),
                    t.CreatedAt, t.UpdatedAt
                })
                .ToListAsync();
            return Json(new { success = true, data });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = "Không tải được danh sách mẫu tem: " + ex.Message });
        }
    }

    [HttpGet("api/{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var data = await db.LabelTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        return data == null
            ? Json(new { success = false, error = "Không tìm thấy mẫu tem." })
            : Json(new { success = true, data });
    }

    [HttpPost("api"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromBody] LabelTemplateRequest req)
    {
        var data = Normalize(req, out string? varError);
        string? error = varError ?? Validate(data, null) ?? await DuplicateError(data, null);
        if (error != null)
            return Json(new { success = false, error });

        data.CreatedAt = DateTime.Now;
        db.LabelTemplates.Add(data);
        await db.SaveChangesAsync();
        return Json(new { success = true, id = data.Id, message = "Tạo mới thành công" });
    }

    [HttpPost("api/{id:int}"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, [FromBody] LabelTemplateRequest req)
    {
        var current = await db.LabelTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (current == null)
            return Json(new { success = false, error = "Không tìm thấy mẫu tem, có thể đã bị xoá." });
        if (current.IsLocked)
            return Json(new { success = false, error = LockedMessage });

        var data = Normalize(req, out string? varError);
        string? error = varError ?? Validate(data, current) ?? await DuplicateError(data, id);
        if (error != null)
            return Json(new { success = false, error });

        current.DefaultCode = data.DefaultCode;
        current.Type = data.Type;
        current.Operation = data.Operation;
        current.Size = data.Size;
        current.Dpi = data.Dpi;
        current.ZplData = data.ZplData;
        current.Variables = data.Variables;
        current.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Cập nhật thành công" });
    }

    [HttpPost("api/{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var current = await db.LabelTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (current == null)
            return Json(new { success = false, error = "Không tìm thấy mẫu tem, có thể đã bị xoá." });
        if (current.IsLocked)
            return Json(new { success = false, error = LockedMessage });

        db.LabelTemplates.Remove(current);
        await db.SaveChangesAsync();
        return Json(new { success = true, message = "Xoá thành công" });
    }

    // Khoá / mở khoá: khoá rồi thì không sửa, không xoá được
    [HttpPost("api/{id:int}/lock"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetLock(int id, [FromBody] LabelTemplateLockRequest req)
    {
        var current = await db.LabelTemplates.FirstOrDefaultAsync(t => t.Id == id);
        if (current == null)
            return Json(new { success = false, error = "Không tìm thấy mẫu tem, có thể đã bị xoá." });

        current.IsLocked = req.Locked;
        current.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
        return Json(new { success = true, message = req.Locked ? "Đã khoá mẫu tem" : "Đã mở khoá mẫu tem" });
    }

    // Xem trước hình tem. Không đụng DB: client gửi ZPL đang xem/đang soạn lên.
    [HttpPost("api/preview"), ValidateAntiForgeryToken]
    public IActionResult Preview([FromBody] ZplPreviewRequest req) => Json(ZplPreviewService.PreviewResponse(req));

    // Chuẩn hoá ZPL: liệt kê chữ / barcode để chọn biến + căn giữa. Không đụng DB, chỉ trả ZPL mới cho form.
    [HttpPost("api/normalize/analyze"), ValidateAntiForgeryToken]
    public IActionResult NormalizeAnalyze([FromBody] ZplNormalizeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ZplData))
            return Json(new { success = false, error = "Chưa có nội dung ZPL." });
        var fields = ZplNormalizer.Analyze(req.ZplData);
        if (fields.Count == 0)
            return Json(new { success = false, error = "Không tìm thấy chữ hoặc barcode nào (^FD) trong ZPL." });
        var variables = variablesConfig.CurrentValue.Variables.Select(v => new { v.Key, v.Label });
        return Json(new { success = true, fields, variables });
    }

    [HttpPost("api/normalize/apply"), ValidateAntiForgeryToken]
    public IActionResult NormalizeApply([FromBody] ZplNormalizeRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ZplData))
            return Json(new { success = false, error = "Chưa có nội dung ZPL." });
        var (zpl, warnings) = ZplNormalizer.Apply(req.ZplData, req.Changes ?? []);
        return Json(new { success = true, zplData = zpl, warnings });
    }

    // ===== Helpers =====

    const string LockedMessage = "Mẫu tem đang khoá. Mở khoá trước khi sửa hoặc xoá.";

    // varError: giá trị biến không hợp lệ (chứa ^ hoặc ~)
    static LabelTemplate Normalize(LabelTemplateRequest req, out string? varError)
    {
        string zpl = req.ZplData ?? "";
        varError = ZplVariables.Normalize(zpl, req.Variables, out var variables);
        return new LabelTemplate
        {
            DefaultCode = (req.DefaultCode ?? "").Trim(),
            Type = (req.Type ?? "").Trim(),
            Operation = (req.Operation ?? "").Trim(),
            Size = (req.Size ?? "").Trim(),
            Dpi = req.Dpi,
            ZplData = zpl,
            Variables = variables
        };
    }

    // current = bản ghi đang sửa (null khi tạo mới). Giá trị cũ không có trong cấu hình vẫn được giữ nguyên khi sửa.
    string? Validate(LabelTemplate data, LabelTemplate? current)
    {
        var opt = options.CurrentValue;

        if (string.IsNullOrEmpty(data.DefaultCode))
            return "Vui lòng nhập mã sản phẩm.";
        if (data.DefaultCode.Length > DefaultCodeMaxLength)
            return $"Mã sản phẩm tối đa {DefaultCodeMaxLength} ký tự.";
        if (!IsAllowed(opt.Types, data.Type, current?.Type))
            return "Loại tem không hợp lệ.";
        if (!IsAllowed(opt.Operations, data.Operation, current?.Operation))
            return "Công đoạn không hợp lệ.";
        if (!IsAllowed(opt.Sizes, data.Size, current?.Size))
            return "Size không hợp lệ.";
        if (!ZplPreviewService.DpmmByDpi.ContainsKey(data.Dpi))
            return "DPI không hợp lệ.";
        if (string.IsNullOrWhiteSpace(data.ZplData))
            return "Vui lòng nhập nội dung ZPL.";
        return null;
    }

    static bool IsAllowed(List<LabelTemplateOption> list, string value, string? currentValue)
    {
        if (string.IsNullOrEmpty(value))
            return false;
        if (list.Any(o => string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase)))
            return true;
        return currentValue != null && string.Equals(currentValue, value, StringComparison.OrdinalIgnoreCase);
    }

    // Mỗi mã sản phẩm + loại tem + công đoạn chỉ có 1 mẫu (DB cũng có unique index chặn lần cuối)
    async Task<string?> DuplicateError(LabelTemplate data, int? exceptId)
    {
        bool exists = await db.LabelTemplates.AnyAsync(t => t.Id != exceptId
            && t.DefaultCode == data.DefaultCode && t.Type == data.Type && t.Operation == data.Operation);
        return exists ? $"Mẫu tem {data.DefaultCode} · {data.Type} · {data.Operation} đã tồn tại." : null;
    }
}
