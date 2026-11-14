using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Toast.Web.Data;
using Toast.Web.Features.Printer;
using Toast.Web.Features.Station;

namespace Toast.Web.Features.LabelPrint;

// Màn In tem: nhập WO -> chọn mẫu tem -> điền biến {KEY} -> xem trước -> in. Truy cập tại /toast/admin/print
// Server TCP: server gửi ZPL tới máy in. TCP/USB qua Node: server chỉ trả ZPL đã điền, trình duyệt gửi tới agent localhost.
// Chuyển từ Twist: SVN-Portal/Areas/TwistAdmin/Controllers/LabelPrintController.cs
[Authorize]
[Route("toast/admin/print")]
public sealed class LabelPrintController(
    ToastDbContext db,
    WorkOrderReader workOrders,
    IOptionsMonitor<LabelVariablesOptions> variablesConfig,
    PrinterLookup printers) : Controller
{
    const int MaxCopies = 100;
    const int MaxSearchResults = 50;

    // ===== Trang =====

    [HttpGet("")]
    public async Task<IActionResult> Index() => View(await printers.ListAsync());

    // ===== API JSON =====

    // Tra WO + danh sách mẫu tem của sản phẩm thuộc WO. Đọc như màn công nhân (WorkOrderReader): có thêm mã khách, đơn bán, PO, Lot
    // để tự điền biến còn trống. Màn admin in tay nên mở được cả WO đã hoàn thành / huỷ (in bù, in lại).
    [HttpGet("api/wo")]
    public async Task<IActionResult> GetWorkOrder(string? wo)
    {
        string name = SessionService.NormalizeWo(wo);
        if (name.Length == 0) return Json(new { success = false, error = "Vui lòng nhập mã WO." });
        try
        {
            var (info, error) = await workOrders.ReadAsync(name, allowClosed: true);
            if (info == null) return Json(new { success = false, error });

            var warnings = (info.GetValueOrDefault(WorkOrderReader.WarningsKey) ?? "")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
            if (info.GetValueOrDefault("requested_name") is { Length: > 0 } requested && !string.Equals(requested, info["name"], StringComparison.OrdinalIgnoreCase))
                warnings.Insert(0, $"WO {requested} đã tách backorder, đang dùng lệnh {info["name"]}.");
            string state = info.GetValueOrDefault("state") ?? "";
            if (state is "done" or "cancel")
                warnings.Insert(0, $"Lệnh {info["name"]} {(state == "done" ? "đã hoàn thành" : "đã bị huỷ")}.");

            // Mẫu tem tìm theo mã sản phẩm của WO
            string code = info.GetValueOrDefault("product_code") ?? "";
            var templates = code.Length == 0 ? [] : await TemplateList(t => t.DefaultCode == code).ToListAsync();
            return Json(new { success = true, wo = info, templates, warnings });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = "Không lấy được thông tin WO: " + ex.Message });
        }
    }

    // Tìm mẫu tem theo mã sản phẩm (in không theo WO)
    [HttpGet("api/templates")]
    public async Task<IActionResult> SearchTemplates(string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return Json(new { success = true, templates = Array.Empty<object>() });
        // Không phân biệt hoa thường ('hw' tìm ra 'HW'): SQL Server mặc định đã vậy, SQLite thì không nên so bằng chữ hoa
        string term = q.Trim().ToUpperInvariant();
        var templates = await TemplateList(t => t.DefaultCode.ToUpper().Contains(term)).Take(MaxSearchResults).ToListAsync();
        return Json(new { success = true, templates });
    }

    // Lấy mẫu tem (có ZPL) + danh sách biến kèm giá trị gợi ý
    [HttpPost("api/resolve"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve([FromBody] LabelPrintResolveRequest req)
    {
        var template = await db.LabelTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.TemplateId);
        if (template == null) return Json(new { success = false, error = "Không tìm thấy mẫu tem." });

        var product = req.Wo == null ? null : await FindProductAsync(req.Wo);
        var variables = LabelVariableResolver.Resolve(template, variablesConfig.CurrentValue, new LabelContext(req.Wo, product, null, User.Identity?.Name));
        return Json(new { success = true, template, variables });
    }

    // Trả ZPL đã điền biến (dùng cho chế độ TCP/USB qua Node: trình duyệt tự gửi tới agent)
    [HttpPost("api/render"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Render([FromBody] LabelPrintRenderRequest req)
    {
        var (zpl, error) = await BuildZpl(req);
        return Json(zpl == null ? new { success = false, error, zpl = (string?)null } : new { success = true, error = (string?)null, zpl });
    }

    // Server TCP: server gửi ZPL đã điền tới máy in
    [HttpPost("api/print"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Print([FromBody] LabelPrintSendRequest req)
    {
        if (req.Copies < 1 || req.Copies > MaxCopies)
            return Json(new { success = false, error = $"Số bản phải từ 1 đến {MaxCopies}." });

        var (zpl, error) = await BuildZpl(req);
        if (zpl == null) return Json(new { success = false, error });

        var (printer, printerError) = await ResolvePrinter(req.PrinterId, req.Ip, req.Port);
        if (printer == null) return Json(new { success = false, error = printerError });

        var result = await TcpPrintService.SendAsync(printer.Ip, printer.Port, zpl, req.Copies);
        if (!result.Ok) return Json(new { success = false, error = result.Error });

        return Json(new
        {
            success = true,
            message = $"Đã gửi {req.Copies} bản tới {printer.Name} ({printer.Ip}:{printer.Port})",
            bytes = result.Bytes,
            elapsedMs = result.ElapsedMs
        });
    }

    // Kiểm tra máy in có phản hồi không (chỉ mở/đóng kết nối, không in)
    [HttpPost("api/test-connection"), ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection([FromBody] LabelPrintTestRequest req)
    {
        var (printer, error) = await ResolvePrinter(req.PrinterId, req.Ip, req.Port);
        if (printer == null) return Json(new { success = false, error });

        var result = await TcpPrintService.TestAsync(printer.Ip, printer.Port);
        return Json(new { success = result.Ok, error = result.Error, elapsedMs = result.ElapsedMs });
    }

    // ===== Helpers =====

    // Danh sách mẫu tem để chọn (không lấy ZPL)
    IQueryable<object> TemplateList(Expression<Func<LabelTemplate.LabelTemplate, bool>> filter) => db.LabelTemplates.AsNoTracking()
        .Where(filter)
        .OrderBy(t => t.DefaultCode).ThenBy(t => t.Type).ThenBy(t => t.Operation)
        .Select(t => new { t.Id, t.DefaultCode, t.Type, t.Operation, t.Size, t.Dpi, t.IsLocked })
        .Cast<object>();

    // Quy cách sản phẩm của WO (theo product_id)
    async Task<Product.Product?> FindProductAsync(Dictionary<string, string> wo) =>
        int.TryParse(wo.GetValueOrDefault("product_id"), out int productId)
            ? await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == productId)
            : null;

    // ZPL lấy lại từ DB theo templateId (không tin ZPL từ trình duyệt), rồi điền biến
    async Task<(string? zpl, string? error)> BuildZpl(LabelPrintRenderRequest req)
    {
        var template = await db.LabelTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.TemplateId);
        if (template == null) return (null, "Không tìm thấy mẫu tem.");

        string? error = LabelVariableResolver.Fill(template.ZplData, req.Values, out string zpl, LabelVariableResolver.OptionalKeys(variablesConfig.CurrentValue));
        return error == null ? (zpl, null) : (null, error);
    }

    // printerId có giá trị: tra trong Toast_Printer (máy đang dùng). Không có: dùng IP/Port nhập tay.
    async Task<(Printer.Printer? printer, string? error)> ResolvePrinter(string? printerId, string? ip, int? port)
    {
        if (!string.IsNullOrWhiteSpace(printerId))
        {
            var printer = await printers.FindAsync(printerId);
            return printer == null ? (null, "Không tìm thấy máy in, có thể đã bị xoá hoặc ngừng dùng.") : (printer, null);
        }

        ip = (ip ?? "").Trim();
        int p = port ?? Printer.Printer.DefaultPort;
        if (!PrinterLookup.Ipv4Regex.IsMatch(ip)) return (null, "Địa chỉ IP không hợp lệ.");
        if (p < 1 || p > 65535) return (null, "Port phải từ 1 đến 65535.");
        return (new Printer.Printer { Name = "Máy in nhập tay", Ip = ip, Port = p }, null);
    }
}
