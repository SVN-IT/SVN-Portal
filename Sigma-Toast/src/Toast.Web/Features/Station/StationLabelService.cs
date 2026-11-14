using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Toast.Web.Data;
using Toast.Web.Features.LabelPrint;
using Toast.Web.Features.Package;

namespace Toast.Web.Features.Station;

// 1 biến tem trên màn In tem ở trạm. Source: template (lưu trong mẫu) | wo | product | system | session (giá trị đã xác nhận
// của phiên) | spec (tính từ quy cách: QTY plate / carton) | manual (nhập tay)
public sealed record StationLabelVariable(string Key, string Label, string Source, string Value, bool Optional);

// Mẫu tem để chọn (không kèm ZPL)
public sealed record StationTemplateItem(int Id, string DefaultCode, string Type, string Operation, string Size, int Dpi);

/// <summary>
/// Menu → In tem ở màn công nhân (in tem bằng tay: tem box, plate, carton, tem phụ; trong hoặc ngoài phiên làm việc).
/// Mẫu tem chọn độc lập (tìm theo mã). WO (không bắt buộc) chỉ để điền dữ liệu: WO đang có phiên mở thì giá trị theo WO = lần xác nhận
/// mới nhất của phiên; ngoài phiên đọc mới từ Viindoo (cả lệnh đã xong / huỷ). Mẫu Plate / Carton: QTY = box / plate × pcs / box
/// (màn Sản phẩm). Biến không có nguồn để trống cho nhập tay (VD số plate, số box). Người in sửa được mọi giá trị trước khi in.
/// </summary>
public sealed class StationLabelService(
    ToastDbContext db,
    WorkOrderReader workOrders,
    PackageService packages,
    IOptionsMonitor<LabelVariablesOptions> variables)
{
    public const string SourceSession = "session", SourceSpec = "spec";

    // Đọc WO (giống màn In tem admin): thông tin WO + các mẫu tem của sản phẩm để chọn
    public async Task<object> WorkOrderAsync(string? wo)
    {
        var r = await ReadWoAsync(wo);
        if (r.Error != null) return new { success = false, error = r.Error };
        if (r.Info == null) return new { success = false, error = "Vui lòng nhập mã WO." };
        string code = r.Product?.DefaultCode ?? r.Info.GetValueOrDefault("product_code") ?? "";
        var templates = code.Length == 0 ? [] : await TemplateList(db.LabelTemplates.Where(t => t.DefaultCode == code)).ToListAsync();
        return new { success = true, wo = WoView(r.Info, r.Product), inSession = r.Session != null, templates, warnings = r.Warnings };
    }

    public async Task<object> ContextAsync(int? templateId, string? wo)
    {
        var template = templateId is int tid ? await db.LabelTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == tid) : null;
        if (template == null) return new { success = false, error = "Chọn mẫu tem." };
        var r = await ReadWoAsync(wo);
        if (r.Error != null) return new { success = false, error = r.Error, field = "wo" };
        var (info, session, product, warnings) = (r.Info, r.Session, r.Product, r.Warnings);
        if (info != null && template.DefaultCode != (product?.DefaultCode ?? info.GetValueOrDefault("product_code")))
            warnings.Add($"Mẫu tem {template.DefaultCode} khác mã sản phẩm của WO ({product?.DefaultCode ?? info.GetValueOrDefault("product_code")}).");

        var confirmed = session != null ? await packages.ConfirmedValuesAsync(session.Id) : null;
        var list = new List<StationLabelVariable>();
        foreach (var v in LabelVariableResolver.Resolve(template, variables.CurrentValue, new LabelContext(info, product)))
        {
            string source = v.Source, value = v.Value;
            if (source != LabelVariableResolver.SourceTemplate && confirmed?.GetValueOrDefault(v.Key) is { Length: > 0 } c)
                (source, value) = (SourceSession, c);
            // Mẫu Plate / Carton: số lượng = box / plate × pcs / box
            if (template.Type != PackageService.BoxType && source != LabelVariableResolver.SourceTemplate
                && string.Equals(v.Key, "QTY", StringComparison.OrdinalIgnoreCase) && product is { PcsPerBox: > 0, BoxPerOuter: > 0 })
                (source, value) = (SourceSpec, (product.PcsPerBox * product.BoxPerOuter.Value).ToString());
            // Không lấy được giá trị (không có WO, biến của box...): nhập tay
            if (value.Length == 0 && source != LabelVariableResolver.SourceTemplate) source = LabelVariableResolver.SourceManual;
            list.Add(new StationLabelVariable(v.Key, v.Label, source, value, v.Optional));
        }

        return new
        {
            success = true,
            template = new { template.Id, template.DefaultCode, template.Type, template.Operation, template.Size, template.Dpi, template.ZplData },
            wo = info == null ? null : WoView(info, product),
            inSession = session != null,
            variables = list,
            warnings
        };
    }

    // Tìm mẫu tem theo mã sản phẩm (tối đa 50; trống = không trả gì, không liệt kê hết mẫu)
    public Task<List<StationTemplateItem>> SearchTemplatesAsync(string? q)
    {
        string term = (q ?? "").Trim().ToUpperInvariant();
        return term.Length == 0 ? Task.FromResult(new List<StationTemplateItem>())
            : TemplateList(db.LabelTemplates.Where(t => t.DefaultCode.ToUpper().Contains(term))).Take(50).ToListAsync();
    }

    static IQueryable<StationTemplateItem> TemplateList(IQueryable<LabelTemplate.LabelTemplate> q) => q.AsNoTracking()
        .OrderBy(t => t.DefaultCode).ThenBy(t => t.Type).ThenBy(t => t.Operation)
        .Select(t => new StationTemplateItem(t.Id, t.DefaultCode, t.Type, t.Operation, t.Size, t.Dpi));

    sealed record WoRead(Dictionary<string, string>? Info, Session? Session, Product.Product? Product, List<string> Warnings, string? Error);

    // WO trống: không theo WO. Trong phiên: WO đã đọc lúc mở phiên (RAM). Ngoài phiên: đọc mới Viindoo, mở được cả lệnh đã xong / huỷ (in bù)
    async Task<WoRead> ReadWoAsync(string? wo)
    {
        string name = SessionService.NormalizeWo(wo);
        if (name.Length == 0) return new(null, null, null, [], null);
        var session = await OpenSessionAsync(name);
        Dictionary<string, string>? info;
        if (session != null) info = await workOrders.GetAsync(session.WoName);
        else
        {
            string? error;
            try { (info, error) = await workOrders.ReadAsync(name, allowClosed: true); }
            catch (Exception ex) { (info, error) = (null, "Không đọc được WO trên Viindoo: " + ex.Message); }
            if (info == null) return new(null, null, null, [], error);
            session = await OpenSessionAsync(info["name"]);
        }
        if (info == null) return new(null, null, null, [], $"Không đọc được thông tin WO {name}.");
        var warnings = (info.GetValueOrDefault(WorkOrderReader.WarningsKey) ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (info.GetValueOrDefault("state") is "done" or "cancel")
            warnings.Insert(0, $"Lệnh {info["name"]} {(info["state"] == "done" ? "đã hoàn thành" : "đã huỷ")}.");
        var product = int.TryParse(info.GetValueOrDefault("product_id"), out int pid)
            ? await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == pid) : null;
        return new(info, session, product, warnings, null);
    }

    static object WoView(Dictionary<string, string> info, Product.Product? product) => new
    {
        name = info["name"],
        productCode = product?.DefaultCode ?? info.GetValueOrDefault("product_code") ?? "",
        productName = product?.Name ?? info.GetValueOrDefault("product_name") ?? "",
        productQty = info.GetValueOrDefault("product_qty") ?? "",
        state = info.GetValueOrDefault("state") ?? "",
        origin = info.GetValueOrDefault("origin") ?? "",
        saleOrder = info.GetValueOrDefault("sale_order") ?? "",
        po = info.GetValueOrDefault("po") ?? "",
        lotId = info.GetValueOrDefault("lot_id") ?? "",
        customerPartNumber = info.GetValueOrDefault("customer_part_number") ?? ""
    };

    Task<Session?> OpenSessionAsync(string woName) =>
        db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.WoName == woName && s.Status == SessionStatus.Open);
}
