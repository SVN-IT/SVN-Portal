using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Toast.Web.Data;
using Toast.Web.Features.LabelPrint;
using Toast.Web.Features.LabelTemplate;
using Toast.Web.Features.Package;
using Toast.Web.Features.Station;

namespace Toast.Web.Features.StationV2;

/// <summary>
/// Bản nháp màn công nhân, /toastv2 (không cần đăng nhập admin). CHỈ XEM để đánh giá dữ liệu điền vào tem:
/// nhập WO -> đọc Viindoo (WorkOrderReader: WO + mã khách + PO / Lot của đơn bán) -> Toast_Product theo product_id
/// -> mẫu tem Box theo default code -> giá trị từng biến (mẫu > tự động theo label-variables.json) -> xem trước tem.
/// Không quét, không in, không ghi DB.
/// </summary>
[AllowAnonymous]
[Route("toastv2")]
public sealed class StationV2Controller(
    ToastDbContext db,
    WorkOrderReader workOrders,
    PackageService packages,
    IOptionsMonitor<LabelVariablesOptions> variablesConfig) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpGet("api/wo")]
    public async Task<IActionResult> OpenWo(string? wo)
    {
        string name = StationApiController.NormalizeWo(wo);
        if (name.Length == 0) return Json(new { success = false, error = "Vui lòng nhập mã WO." });

        Dictionary<string, string>? info;
        string? error;
        try
        {
            // Màn nháp: mở được cả WO đã hoàn thành / huỷ để thử
            (info, error) = await workOrders.ReadAsync(name, allowClosed: true);
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = "Không đọc được Viindoo: " + ex.Message });
        }
        if (info == null) return Json(new { success = false, error });

        var warnings = (info.GetValueOrDefault(WorkOrderReader.WarningsKey) ?? "")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        var problems = new List<string>();
        if (info.GetValueOrDefault("requested_name") is { Length: > 0 } requested && !string.Equals(requested, info["name"], StringComparison.OrdinalIgnoreCase))
            warnings.Insert(0, $"WO {requested} đã tách backorder, đang xem lệnh {info["name"]} (lệnh đang mở mới nhất của nhóm, hết lệnh mở thì lệnh mới nhất).");
        string state = info.GetValueOrDefault("state") ?? "";
        if (state is "done" or "cancel")
            warnings.Insert(0, $"Lệnh {info["name"]} {(state == "done" ? "đã hoàn thành" : "đã bị huỷ")}: chỉ xem thử, màn /toast sẽ không mở lệnh này.");

        int.TryParse(info.GetValueOrDefault("product_id"), out int productId);
        var product = productId > 0 ? await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == productId) : null;
        if (product == null)
            problems.Add($"Sản phẩm {info.GetValueOrDefault("product_code")} (Viindoo ID {productId}) chưa có ở Quản trị → Sản phẩm.");
        else if (!string.Equals(product.DefaultCode, info.GetValueOrDefault("product_code"), StringComparison.OrdinalIgnoreCase))
            warnings.Add($"Mã sản phẩm trên Viindoo ({info.GetValueOrDefault("product_code")}) khác mã đã lưu trong Toast ({product.DefaultCode}).");

        // Mẫu tem tìm theo default code của sản phẩm Toast (chưa có sản phẩm thì thử theo mã của WO)
        string code = product?.DefaultCode ?? info.GetValueOrDefault("product_code") ?? "";
        var template = code.Length == 0 ? null : await packages.FindBoxTemplateAsync(code);
        if (template == null)
            problems.Add($"Chưa có mẫu tem Box cho mã sản phẩm {code}. Vào Quản trị → Mẫu tem, thêm mẫu có Mã sản phẩm = {code}, Loại tem = {PackageService.BoxType}.");

        // Biến: chưa có box (không quét) nên biến của box để trống
        var variables = template == null ? []
            : LabelVariableResolver.Resolve(template, variablesConfig.CurrentValue, new LabelContext(info, product));
        var missing = variables.Where(v => v.Value.Trim().Length == 0 && !v.Optional).Select(v => v.Key).ToList();
        if (missing.Count > 0)
            problems.Add("Tem còn thiếu giá trị: " + string.Join(", ", missing.Select(k => "{" + k + "}")) + ".");

        return Json(new
        {
            success = true,
            wo = info.Where(kv => kv.Key != WorkOrderReader.WarningsKey).ToDictionary(kv => kv.Key, kv => kv.Value),
            isTest = info.GetValueOrDefault("origin") == "WO thử",
            product = product == null ? null : new
            {
                product.ProductId, product.DefaultCode, product.Name, product.ScanSource, product.Barcode, product.PcsPerBox,
                product.OuterType, product.BoxPerOuter, product.ViindooQtyPerCode
            },
            template = template == null ? null : new
            {
                template.Id, template.DefaultCode, template.Type, template.Operation, template.Size, template.Dpi,
                dpmm = ZplPreviewService.DpmmByDpi.GetValueOrDefault(template.Dpi, ZplPreviewService.DefaultDpmm),
                template.ZplData
            },
            // field: trường của nguồn tự động (VD wo · po) để màn hình ghi rõ lấy từ đâu
            variables = variables.Select(v => new
            {
                v.Key, v.Label, v.Source, v.Value, v.Optional,
                field = variablesConfig.CurrentValue.Variables
                    .FirstOrDefault(d => string.Equals(d.Key, v.Key, StringComparison.OrdinalIgnoreCase))?.Field
            }),
            missing,
            warnings,
            problems
        });
    }

    // Xem trước tem (không đụng DB, giống màn Mẫu tem)
    [HttpPost("api/preview"), ValidateAntiForgeryToken]
    public IActionResult Preview([FromBody] ZplPreviewRequest req) => Json(ZplPreviewService.PreviewResponse(req));
}
