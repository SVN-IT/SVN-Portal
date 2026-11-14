using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Toast.Web.Data;
using Toast.Web.Features.LabelPrint;
using Toast.Web.Features.LabelTemplate;
using Toast.Web.Features.Printer;
using Toast.Web.Features.Station;

namespace Toast.Web.Features.Package;

// Kết quả in tem: Ok = máy in đã nhận dữ liệu. PrinterId = máy trong danh sách đã dùng, Target = mô tả máy in (ghi vào box).
// Agent khác null (chế độ TCP / USB qua Node): server chỉ điền biến, trình duyệt phải gửi Agent tới agent trên máy trạm rồi
// báo kết quả (box: /toast/api/box/{id}/print-result). Lúc đó Ok = false, Error = null.
public record PrintOutcome(bool Ok, string? Error, string? PrinterName, int? PrinterId = null, string? Target = null, AgentPrintJob? Agent = null)
{
    public bool Pending => Agent != null;
}

/// <summary>Xử lý kiện dùng chung cho màn công nhân: tìm mẫu tem Box theo mã sản phẩm, mở kiện, đóng kiện + ghi log dashboard + in tem.</summary>
public sealed class PackageService(
    ToastDbContext db,
    PrinterLookup printers,
    WorkOrderReader workOrders,
    IOptionsMonitor<LabelTemplateOptions> templateOptions,
    IOptionsMonitor<LabelVariablesOptions> variables,
    IOptionsMonitor<PackageOptions> packageOptions,
    IOptionsMonitor<StationOptions> stationOptions)
{
    public const string BoxType = "Box";
    // Box in qua agent: lưu tạm lỗi này đến khi trình duyệt báo kết quả (trình duyệt tắt giữa chừng thì box hiện Lỗi in để in lại)
    public const string AgentPendingError = "Chưa nhận được kết quả in từ agent trên máy trạm. Kiểm tra tem, thiếu thì in lại.";

    // Công đoạn / dự án mặc định của mẫu tem (LabelTemplateOptions.Operations, mục IsDefault)
    string Operation => templateOptions.CurrentValue.Operations.FirstOrDefault(o => o.IsDefault)?.Value ?? "Toast";

    // Mẫu tem Box của sản phẩm: DefaultCode (mã sản phẩm) + Type = Box + Operation mặc định (unique index nên chỉ có 1)
    public Task<LabelTemplate.LabelTemplate?> FindBoxTemplateAsync(string defaultCode) => FindTemplateAsync(defaultCode, BoxType);

    // Mẫu tem của sản phẩm theo loại (Box / Plate / Carton), công đoạn mặc định: mỗi sản phẩm 1 mẫu / loại
    public Task<LabelTemplate.LabelTemplate?> FindTemplateAsync(string defaultCode, string type) =>
        db.LabelTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.DefaultCode == defaultCode && t.Type == type && t.Operation == Operation);

    // Biến của mẫu không có nguồn nào (không có giá trị trong mẫu, không tự điền được): còn biến này thì chưa cho quét
    public List<string> MissingVariables(LabelTemplate.LabelTemplate template) =>
        LabelVariableResolver.MissingKeys(template, variables.CurrentValue);

    public Task<Package?> FindOpenPackageAsync(string woName) =>
        db.Packages.FirstOrDefaultAsync(p => p.WoName == woName && p.Status == PackageStatus.Open);

    // Box dở mang sang lệnh mới của nhóm (phiên lệnh trước để dở): chỉ xét box mới nhất của nhóm (số box lớn nhất).
    // Box đó đã đóng, hoặc đang mở nhưng đã đủ / vượt pcs/box thì không mang sang (null): lệnh mới mở box mới.
    public async Task<Package?> FindCarryOverPackageAsync(string woGroup)
    {
        if (woGroup.Length == 0) return null;
        var latest = await db.Packages.Where(p => p.WoGroup == woGroup)
            .OrderByDescending(p => p.PackageNo).ThenByDescending(p => p.Id).FirstOrDefaultAsync();
        return latest is { Status: PackageStatus.Open } && latest.Qty < latest.Capacity ? latest : null;
    }

    // Kiện đang mở của WO, chưa có thì mở kiện mới với số thứ tự = số lớn nhất hiện có của nhóm lệnh + 1
    public async Task<Package> GetOrCreateOpenPackageAsync(string woName, string woGroup, Product.Product product)
    {
        var package = await FindOpenPackageAsync(woName);
        if (package != null) return package;

        int lastNo = await db.Packages.Where(p => p.WoGroup == woGroup || p.WoName == woName).MaxAsync(p => (int?)p.PackageNo) ?? 0;
        package = new Package
        {
            WoName = woName,
            WoGroup = woGroup,
            ProductId = product.ProductId,
            DefaultCode = product.DefaultCode,
            PackageNo = lastNo + 1,
            Capacity = product.PcsPerBox,
            Status = PackageStatus.Open,
            CreatedAt = DateTime.Now
        };
        db.Packages.Add(package);
        return package;
    }

    // Đóng kiện, ghi log dashboard (nếu bật, trừ WO thử) rồi in tem. Đóng + log lưu trước khi in (1 lần SaveChanges = 1 transaction)
    // để chờ máy in không giữ transaction. In lỗi kiện vẫn đóng (đã đủ hàng), lỗi lưu vào PrintError để in lại ở Menu → Box.
    public async Task<PrintOutcome> CloseAndPrintAsync(Package package, PrintTarget? target)
    {
        package.Status = PackageStatus.Closed;
        package.ClosedAt = DateTime.Now;
        package.UpdatedAt = DateTime.Now;
        package.SessionId = await OpenSessionIdAsync(package);
        var log = AddDashboardLogIfEnabled(package);
        await db.SaveChangesAsync();
        if (log != null) package.InputLogId = log.Id;

        var outcome = await PrintAsync(package, target);
        SaveOutcome(package, outcome);
        await db.SaveChangesAsync();
        return outcome;
    }

    // Ghi kết quả in vào box. In qua agent: chưa biết kết quả, lưu tạm AgentPendingError
    static void SaveOutcome(Package package, PrintOutcome outcome)
    {
        if (outcome.Target != null) { package.PrinterId = outcome.PrinterId; package.PrintTarget = Truncate(outcome.Target, 200); }
        package.PrintedAt = outcome.Ok ? DateTime.Now : null;
        package.PrintError = outcome.Ok ? null : outcome.Pending ? AgentPendingError : Truncate(outcome.Error, 500);
        package.UpdatedAt = DateTime.Now;
    }

    // Giá trị tem đã xác nhận của phiên: lần xác nhận mới nhất (KEY -> giá trị), null = không có
    public Task<Dictionary<string, string>?> ConfirmedValuesAsync(int? sessionId) => sessionId is int id
        ? db.SessionConfirms.AsNoTracking().Where(c => c.SessionId == id).OrderByDescending(c => c.Id)
            .Select(c => (Dictionary<string, string>?)c.Values).FirstOrDefaultAsync()
        : Task.FromResult<Dictionary<string, string>?>(null);

    // ===== In khi mất mạng (trạm in qua agent USB / TCP qua Node, máy in đang chọn, bằng mẫu lưu trên trình duyệt) =====

    public const string OfflineTargetPrefix = "In khi mất mạng (offline)";
    // Đánh dấu box in offline cần leader kiểm tra (số box / mẫu tem lệch): nằm trong PrintTarget
    public const string OfflineCheckMark = "⚠";

    // Phiên bản mẫu tem: đổi khi mẫu được sửa (trạm so để tải lại mẫu lưu trên trình duyệt)
    public static string TemplateVersion(LabelTemplate.LabelTemplate t) =>
        t.Id + "-" + (t.UpdatedAt ?? t.CreatedAt).Ticks;

    // Mẫu tem Box để trạm lưu trên trình duyệt: ZPL đã điền sẵn các biến không đổi trong phiên (WO, sản phẩm, giá trị đã xác nhận),
    // còn lại các biến theo box (số box, số pcs, ngày giờ đóng) để trạm tự điền lúc in offline. Thiếu biến bắt buộc: trả lỗi.
    public async Task<(object? label, string? error)> OfflineLabelAsync(string woName, int sessionId, Product.Product product, LabelTemplate.LabelTemplate template)
    {
        var config = variables.CurrentValue;
        var wo = await workOrders.GetAsync(woName) ?? new Dictionary<string, string>
        {
            ["name"] = woName, ["product_code"] = product.DefaultCode, ["product_name"] = product.Name
        };
        var confirmed = await ConfirmedValuesAsync(sessionId) ?? [];
        var dynamic = new List<object>();
        var fixedValues = new List<LabelVariable>();
        foreach (var v in LabelVariableResolver.Resolve(template, config, new LabelContext(wo, product)))
        {
            var def = config.Variables.FirstOrDefault(d => string.Equals(d.Key, v.Key, StringComparison.OrdinalIgnoreCase));
            if (def is { Source: "package" } || def is { Source: "system", Field: "now" })
            {
                dynamic.Add(new { key = v.Key, source = def.Source, field = def.Field, format = def.Format });
                continue;
            }
            string value = confirmed.TryGetValue(v.Key, out var c) && c.Length > 0 ? c : v.Value;
            if (value.Length == 0 && !v.Optional) return (null, $"Biến {{{v.Key}}} chưa có giá trị.");
            fixedValues.Add(new LabelVariable { Key = v.Key, Value = value });
        }
        if (ZplVariables.Normalize(template.ZplData, fixedValues, out _) is { } invalid) return (null, invalid);
        return (new
        {
            templateId = template.Id,
            version = TemplateVersion(template),
            zpl = ZplVariables.Apply(template.ZplData, fixedValues, blankEmpty: true),
            dynamic
        }, null);
    }

    // Box đã in offline ở trạm (gửi lên khi có mạng lại): đóng box theo giờ in offline, ghi đã in, không in lại.
    // Số box trên tem khác số server đánh, hoặc in bằng mẫu cũ: thêm dấu ⚠ vào PrintTarget để leader kiểm tra / in lại.
    public async Task CloseOfflineAsync(Package package, OfflinePrintedBox printedBox, string? currentVersion)
    {
        var now = DateTime.Now;
        // Giờ máy trạm vô lý (tương lai / quá cũ): dùng giờ server
        var closedAt = printedBox.ClosedAt is DateTime t && t <= now.AddMinutes(10) && t >= now.AddDays(-7) ? t : now;
        var notes = new List<string>();
        if (printedBox.BoxNo != package.PackageNo) notes.Add($"{OfflineCheckMark} tem in số box {printedBox.BoxNo}, server đánh số {package.PackageNo}");
        if (currentVersion != null && printedBox.Version != currentVersion) notes.Add($"{OfflineCheckMark} in bằng mẫu tem cũ");

        package.Status = PackageStatus.Closed;
        package.ClosedAt = closedAt;
        package.UpdatedAt = now;
        package.SessionId = await OpenSessionIdAsync(package);
        package.PrinterId = null;
        package.PrintTarget = Truncate(string.Join(" · ", new[] { OfflineTargetPrefix, printedBox.Printer ?? "" }.Concat(notes).Where(x => x.Length > 0)), 200);
        package.PrintedAt = closedAt;
        package.PrintError = null;
        var log = AddDashboardLogIfEnabled(package);
        await db.SaveChangesAsync();
        if (log != null) package.InputLogId = log.Id;
    }

    // Trình duyệt báo kết quả gửi tem tới agent (chế độ TCP / USB qua Node)
    public async Task ReportAgentResultAsync(Package package, bool ok, string? error)
    {
        package.PrintedAt = ok ? DateTime.Now : null;
        package.PrintError = ok ? null : Truncate(string.IsNullOrWhiteSpace(error) ? "Agent báo in lỗi." : error, 500);
        package.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();
    }

    // Phiên đang mở của lệnh (không có thì của nhóm lệnh) lúc đóng kiện. Không có phiên mở: null
    public Task<int?> OpenSessionIdAsync(Package package) => db.Sessions
        .Where(s => s.Status == SessionStatus.Open && (s.WoName == package.WoName || (package.WoGroup != "" && s.WoGroup == package.WoGroup)))
        .OrderByDescending(s => s.WoName == package.WoName).ThenByDescending(s => s.Id)
        .Select(s => (int?)s.Id).FirstOrDefaultAsync();

    // Kiện vừa đóng: thêm 1 dòng log dashboard nếu bật Toast:WriteDashboardLog (WO thử không ghi). Chưa SaveChanges:
    // lưu cùng lần với kiện, sau khi lưu thì gán package.InputLogId = log.Id.
    public ProductionInputLog? AddDashboardLogIfEnabled(Package package) =>
        packageOptions.CurrentValue.WriteDashboardLog && !stationOptions.CurrentValue.IsTestWo(package.WoName) ? AddDashboardLog(package) : null;

    // Sửa / xoá kiện thì sửa / xoá dòng log dashboard đã ghi lúc đóng (theo InputLogId), để dashboard đúng với số kiện:
    // kiện còn đóng: product_qty = số pcs mới; kiện bị xoá hoặc mở lại (Open): xoá hẳn dòng log (đóng lại thì ghi dòng mới).
    // Kiện đóng lúc công tắc tắt (InputLogId trống) thì không có gì để sửa. Chưa SaveChanges: lưu cùng lần với kiện.
    public async Task SyncDashboardLogAsync(Package package, bool deleted = false)
    {
        if (package.InputLogId is not int logId) return;
        var log = await db.ProductionInputLogs.FirstOrDefaultAsync(l => l.Id == logId);
        if (deleted || package.Status != PackageStatus.Closed)
        {
            if (log != null) db.ProductionInputLogs.Remove(log);
            package.InputLogId = null;
        }
        else if (log != null)
        {
            log.ProductQty = package.Qty;
        }
    }

    // 1 dòng SVN_ProductionInputLogs cho kiện vừa đóng, cùng dạng Twist ghi mỗi tem box (serial trống, product_type none).
    // wo_code = lệnh thật lúc đóng, master_wo_code = nhóm lệnh (tên gốc, VD NM/MO/06389)
    ProductionInputLog AddDashboardLog(Package package)
    {
        var log = new ProductionInputLog
        {
            ProductId = package.ProductId,
            ProductQty = package.Qty,
            DateFinished = package.ClosedAt,
            ProductType = "none",
            SerialCode = "",
            State = "Used",
            Status = ProductionInputLog.ToastStatus,
            WoCode = package.WoName,
            MasterWoCode = package.WoGroup.Length > 0 ? package.WoGroup : package.WoName
        };
        db.ProductionInputLogs.Add(log);
        return log;
    }

    // In lại tem box đã đóng (menu F2 → Box → In lại): mẫu tem hiện tại + giá trị của lần xác nhận phiên lúc đóng box
    // Lỗi (VD máy in tắt) thì giữ nguyên trạng thái in trước đó của box.
    public async Task<PrintOutcome> ReprintAsync(Package package, PrintTarget? target)
    {
        var outcome = await PrintAsync(package, target);
        if (outcome.Ok || outcome.Pending)
        {
            SaveOutcome(package, outcome);
            await db.SaveChangesAsync();
        }
        return outcome;
    }

    // Tem box: mẫu theo mã sản phẩm, biến điền theo sản phẩm + WO + kiện. Box có lần xác nhận phiên thì giá trị đã xác nhận
    // (PO, Lot, Part...) thắng giá trị đọc lại; ngày giờ theo giờ đóng box (in lại ra giống bản đầu).
    async Task<PrintOutcome> PrintAsync(Package package, PrintTarget? target)
    {
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == package.ProductId);
        var template = await FindBoxTemplateAsync(package.DefaultCode);
        if (template == null) return new(false, $"Chưa có mẫu tem Box cho {package.DefaultCode}.", null);

        // Không đọc được WO (Viindoo lỗi) thì vẫn có tên WO / mã / tên sản phẩm
        var wo = await workOrders.GetAsync(package.WoName) ?? new Dictionary<string, string>
        {
            ["name"] = package.WoName,
            ["product_code"] = package.DefaultCode,
            ["product_name"] = product?.Name ?? ""
        };
        // Giá trị tem đã xác nhận: lần xác nhận mới nhất của phiên đóng kiện (thắng giá trị đọc lại)
        var confirmed = await ConfirmedValuesAsync(package.SessionId);
        return await PrintTemplateAsync(template, target, 1, new LabelContext(wo, product, package, Now: package.ClosedAt), confirmed);
    }

    // In 1 mẫu tem. Biến điền theo ngữ cảnh (null = chỉ giá trị lưu trong mẫu + hệ thống, dùng cho in thủ công)
    // confirmed: giá trị đã xác nhận ở phiên (KEY -> giá trị), thắng giá trị tính lại
    // Server TCP: server gửi luôn. TCP / USB qua Node: trả ZPL (đã nhân số bản) trong Agent để trình duyệt gửi.
    public async Task<PrintOutcome> PrintTemplateAsync(LabelTemplate.LabelTemplate template, PrintTarget? target, int copies, LabelContext? context = null,
        IReadOnlyDictionary<string, string>? confirmed = null)
    {
        var values = LabelVariableResolver.Values(template, variables.CurrentValue, context ?? new LabelContext());
        foreach (var (key, value) in confirmed ?? new Dictionary<string, string>())
            if (values.ContainsKey(key) && value.Length > 0) values[key] = value;
        return await PrintValuesAsync(template, values, target, copies);
    }

    // In mẫu tem với giá trị biến cho sẵn (KEY -> giá trị). Biến bắt buộc còn trống: báo lỗi, không in.
    public async Task<PrintOutcome> PrintValuesAsync(LabelTemplate.LabelTemplate template, Dictionary<string, string> values, PrintTarget? target, int copies)
    {
        var (printer, printerError) = await printers.ResolveAsync(target);
        if (printer == null) return new(false, printerError, null);
        string? fillError = LabelVariableResolver.Fill(template.ZplData, values, out string zpl, LabelVariableResolver.OptionalKeys(variables.CurrentValue));
        if (fillError != null) return new(false, $"Mẫu tem {template.DefaultCode} · {template.Type}: {fillError}", printer.Name, printer.PrinterId, printer.Describe());

        if (printer.ViaAgent)
        {
            string all = string.Concat(Enumerable.Repeat(zpl, Math.Max(copies, 1)));
            var job = printer.Mode == PrintTarget.ModeUsb
                ? new AgentPrintJob("/print-usb", all, null, null, printer.UsbName)
                : new AgentPrintJob("/print", all, printer.Ip, printer.Port, null);
            return new(false, null, printer.Name, printer.PrinterId, printer.Describe(), job);
        }
        var result = await TcpPrintService.SendAsync(printer.Ip!, printer.Port, zpl, copies);
        return new(result.Ok, result.Error, printer.Name, printer.PrinterId, printer.Describe());
    }

    static string? Truncate(string? s, int max) => s == null || s.Length <= max ? s : s[..max];
}
