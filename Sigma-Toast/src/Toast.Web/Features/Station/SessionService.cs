using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Toast.Web.Data;
using Toast.Web.Features.LabelPrint;
using Toast.Web.Features.Package;
using Toast.Web.Features.Printer;

namespace Toast.Web.Features.Station;

// Thông tin đã đọc khi mở WO: WO (Viindoo hoặc WO thử), quy cách, mẫu tem Box, cảnh báo
public sealed partial record WoContext(
    bool IsTest,
    Dictionary<string, string> Info,
    Product.Product? Product,
    LabelTemplate.LabelTemplate? Template,
    List<string> Warnings)
{
    public string WoName => Info["name"];

    // Nhóm lệnh: tên nhóm Viindoo (procurement_group_id); không có (WO thử) thì bỏ đuôi backorder "-001" của tên lệnh
    public string WoGroup => Info.GetValueOrDefault("procurement_group_name") is { Length: > 0 } g ? g : BaseName(WoName);

    // id lệnh trên Viindoo, null = WO thử / không đọc được
    public int? ViindooMoId => int.TryParse(Info.GetValueOrDefault("id"), out int id) && id > 0 ? id : null;

    [System.Text.RegularExpressions.GeneratedRegex(@"-\d+$")]
    private static partial System.Text.RegularExpressions.Regex BackorderSuffix();
    public static string BaseName(string woName) => BackorderSuffix().Replace(woName, "");
}

// 1 dòng trong danh sách kiểm tra của modal mở phiên. Status: ok | warn | error | info. error = không cho vào làm.
public sealed record CheckItem(string Status, string Text);

// Giá trị biến tem hiển thị để đối chiếu (biến lấy lúc in như số box, ngày giờ không hiện)
public sealed record CheckValue(string Key, string Label, string Value);

// ScanCode = mã quét pcs sẽ dùng (barcode cố định hoặc Lot ID của WO), null = chưa xác định được (đã có dòng error)
public sealed record SessionCheck(List<CheckItem> Items, List<CheckValue> Values, bool CanStart, string? ScanCode)
{
    public string? FirstError => Items.FirstOrDefault(i => i.Status == SessionService.Error)?.Text;
}

/// <summary>
/// Mở phiên làm việc trên màn công nhân: đọc WO, kiểm tra đủ điều kiện (lệnh mở, BoM, quy cách, barcode, mẫu tem, biến tem)
/// rồi lưu phiên + lần xác nhận (mã nhân viên, máy in, giá trị tem). Mỗi lệnh (backorder, theo id Viindoo) 1 phiên Open, mở lại thì vào
/// phiên cũ. Lệnh sau của nhóm (-002 sau -001) = phiên mới; box dở đi theo nhóm lệnh sang phiên mới.
/// </summary>
public sealed partial class SessionService(
    ToastDbContext db,
    WorkOrderReader workOrders,
    PackageService packages,
    PrinterLookup printers,
    EmployeeDirectory employees,
    ProductionPush.ProductionPushService push,
    IOptionsMonitor<StationOptions> options,
    IOptionsMonitor<LabelVariablesOptions> variables)
{
    public const string Ok = "ok", Warn = "warn", Error = "error", Info = "info";
    public const string UnknownEmployeeError = "Nhân viên không tồn tại.";
    const string WoPrefix = "NM/MO/";

    // Mã nhân viên: SVN + 4 chữ số (VD SVN0215), không phân biệt hoa thường
    [GeneratedRegex(@"^SVN\d{4}$", RegexOptions.IgnoreCase)]
    private static partial Regex EmployeeCodeRegex();

    public static string NormalizeEmployeeCode(string? code) => (code ?? "").Trim().ToUpperInvariant();
    public static bool IsValidEmployeeCode(string? code) => EmployeeCodeRegex().IsMatch(NormalizeEmployeeCode(code));

    // "06229" -> "NM/MO/06229"; "nm/mo/02533-002" -> "NM/MO/02533-002"
    public static string NormalizeWo(string? wo)
    {
        string s = (wo ?? "").Trim().ToUpperInvariant();
        if (s.Length == 0) return "";
        return s.StartsWith(WoPrefix) ? s : WoPrefix + s;
    }

    // Đọc WO + quy cách + mẫu tem. Lỗi (không tìm thấy, đã xong / huỷ, sai tên...) thì trả error.
    public async Task<(WoContext? ctx, string? error)> LoadAsync(string? wo)
    {
        string expected = NormalizeWo(wo);
        if (expected.Length == 0) return (null, "Vui lòng nhập mã WO.");

        bool isTest = options.CurrentValue.IsTestWo(expected);
        var (info, error) = await workOrders.ReadAsync(expected);
        if (info == null) return (null, error);

        // ViindooRpc tìm đúng tên, hoặc (tên gốc đã tách backorder) lệnh đang mở mới nhất của nhóm cùng tên. Kiểm tra lại cho chắc.
        string requested = info.GetValueOrDefault("requested_name") ?? info["name"];
        if (!string.Equals(info["name"], expected, StringComparison.OrdinalIgnoreCase) && !string.Equals(requested, expected, StringComparison.OrdinalIgnoreCase))
            return (null, $"Viindoo trả về lệnh {info["name"]}, không khớp {expected}. Nhập đầy đủ mã lệnh (VD 02533-002).");
        if (!int.TryParse(info["product_id"], out int productId))
            return (null, "WO không có product_id hợp lệ.");

        // Tên gốc đã tách backorder: Toast tự dùng lệnh đang mở mới nhất của nhóm, không cảnh báo (người dùng bỏ 09/11/2026)
        var warnings = new List<string>();

        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == productId);
        // Mẫu tem tìm theo mã sản phẩm của Toast, chưa khai báo sản phẩm thì theo mã trên WO (để modal báo đủ cả 2 lỗi một lượt)
        string templateCode = product?.DefaultCode ?? info["product_code"];
        var template = templateCode.Length == 0 ? null : await packages.FindBoxTemplateAsync(templateCode);
        if (product != null)
        {
            if (!string.Equals(info["product_code"], product.DefaultCode, StringComparison.OrdinalIgnoreCase))
                warnings.Add($"Mã sản phẩm trên Viindoo ({info["product_code"]}) khác mã đã lưu trong Toast ({product.DefaultCode}).");
        }
        var ctx = new WoContext(isTest, info, product, template, warnings);
        if (await FindStaleOpenOfGroupAsync(ctx) is { } stale)
            warnings.Add($"Phiên #{stale.Id} của lệnh trước {stale.WoName} (cùng nhóm) vẫn đang mở. Vào làm sẽ kết thúc phiên đó và mở phiên mới cho {ctx.WoName}.");
        return (ctx, null);
    }

    // Danh sách kiểm tra hiển thị trong modal. Có dòng error thì không cho vào làm.
    public SessionCheck Check(WoContext ctx)
    {
        var info = ctx.Info;
        var items = new List<CheckItem>();
        var values = new List<CheckValue>();

        string qty = decimal.TryParse(info.GetValueOrDefault("product_qty"), NumberStyles.Number, CultureInfo.InvariantCulture, out var q)
            ? q.ToString("#,0.##", CultureInfo.GetCultureInfo("vi-VN")) : info.GetValueOrDefault("product_qty") ?? "";
        items.Add(new(Ok, ctx.IsTest ? $"WO thử {ctx.WoName} · {qty} pcs (không gọi Viindoo)" : $"Lệnh {ctx.WoName} đang mở · {qty} pcs"));

        if (ctx.IsTest) items.Add(new(Info, "WO thử: không kiểm tra BoM."));
        else if (info.GetValueOrDefault("bom") is { Length: > 0 } bom) items.Add(new(Ok, "BoM: " + bom));
        else items.Add(new(Error, "Lệnh chưa có BoM trên Viindoo."));

        // Kiểm tra sản phẩm và mẫu tem độc lập: thiếu cả hai thì báo cả hai một lượt
        var p = ctx.Product;
        string? scanCode = null;
        if (p == null)
        {
            items.Add(new(Error, $"Sản phẩm {info["product_code"]} (ID {info["product_id"]}) chưa khai báo sản phẩm. Vào Quản trị → Sản phẩm để thêm."));
        }
        else
        {
            items.Add(p.PcsPerBox > 0
                ? new(Ok, $"Sản phẩm: {p.DefaultCode} · {p.Name} · {p.PcsPerBox} pcs/box")
                : new(Error, $"Sản phẩm {p.DefaultCode} chưa có Pcs / box. Vào Quản trị → Sản phẩm để điền."));
            if (PackingCheck(ctx, p) is { } packing) items.Add(packing);
            CheckItem scan;
            (scan, scanCode) = ScanCheck(ctx, p);
            items.Add(scan);
        }

        var t = ctx.Template;
        string code = p?.DefaultCode ?? info["product_code"];
        if (t == null)
        {
            items.Add(new(Error, $"Chưa có mẫu tem Box cho mã sản phẩm {code}. Vào Quản trị → Mẫu tem để thêm."));
        }
        else
        {
            items.Add(new(Ok, $"Mẫu tem Box · {t.Size} · {t.Dpi} dpi"));
            var missing = new List<string>();
            foreach (var v in LabelVariableResolver.Resolve(t, variables.CurrentValue, new LabelContext(info, p)))
            {
                // Số box, ngày giờ, người in: chỉ có lúc in. Chưa khai báo sản phẩm: biến lấy từ sản phẩm đã nằm trong lỗi chưa khai báo sản phẩm
                if (v.Source is "package" or "system" || (p == null && v.Source == "product")) continue;
                if (v.Value.Length > 0) values.Add(new(v.Key, v.Label, v.Value));
                else if (!v.Optional) missing.Add($"{{{v.Key}}} ({v.Label})");
            }
            if (missing.Count > 0)
                items.Add(new(Error, "Tem còn thiếu giá trị: " + string.Join(", ", missing) + ". Điền ở Quản trị → Mẫu tem hoặc trên Viindoo."));
        }

        foreach (string w in ctx.Warnings) items.Add(new(Warn, w));
        foreach (string w in (info.GetValueOrDefault(WorkOrderReader.WarningsKey) ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            items.Add(new(Warn, w));

        bool canStart = items.All(i => i.Status != Error);
        return new(items, values, canStart, canStart ? scanCode : null);
    }

    // Tiêu chuẩn đóng gói: x_quantity_per_code trên Viindoo phải > 0 và bằng Pcs / box của Toast (WO thử không đối chiếu)
    static CheckItem? PackingCheck(WoContext ctx, Product.Product p)
    {
        if (ctx.IsTest) return new(Info, "WO thử: không đối chiếu Pcs / box với Viindoo.");
        if (p.PcsPerBox <= 0) return null; // đã báo lỗi chưa có Pcs / box
        if (!int.TryParse(ctx.Info.GetValueOrDefault("qty_per_code"), out int viindoo) || viindoo <= 0)
            return new(Error, $"Viindoo chưa có tiêu chuẩn đóng gói (x_quantity_per_code) cho {p.DefaultCode}, hoặc không đọc được. Điền trên Viindoo (lớn hơn 0).");
        return viindoo == p.PcsPerBox
            ? new(Ok, $"Tiêu chuẩn đóng gói Viindoo: {viindoo} pcs/box (khớp Toast)")
            : new(Error, $"Pcs / box của Toast ({p.PcsPerBox}) khác Viindoo ({viindoo}). Sửa cho khớp ở Quản trị → Sản phẩm hoặc trên Viindoo.");
    }

    // Mã quét pcs: barcode cố định của sản phẩm, hoặc Lot ID của WO (đơn bán) với sản phẩm quét theo Lot
    static (CheckItem item, string? code) ScanCheck(WoContext ctx, Product.Product p)
    {
        if (p.ScanSource == Product.ScanSources.LotId)
        {
            string lot = (ctx.Info.GetValueOrDefault("lot_id") ?? "").Trim();
            return lot.Length > 0
                ? (new(Ok, "Mã quét pcs = Lot ID: " + lot), lot)
                : (new(Error, "Không xác định được Lot ID của WO (xem cảnh báo đơn bán). Sản phẩm quét theo Lot ID nên chưa mở được phiên."), null);
        }
        string barcode = p.Barcode.Trim();
        return barcode.Length > 0
            ? (new(Ok, "Barcode pcs: " + barcode), barcode)
            : (new(Error, $"Sản phẩm {p.DefaultCode} chưa khai báo barcode pcs."), null);
    }

    // Mã máy trạm (localStorage), tối đa 64 ký tự; trống = không theo dõi máy
    public static string? NormalizeDeviceId(string? deviceId) =>
        string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim() is var d && d.Length > 64 ? d[..64] : deviceId.Trim();

    // Mã quét pcs của lần xác nhận gần nhất trong phiên (null = lần xác nhận trước khi có cột ScanCode)
    public Task<string?> ScanCodeAsync(int sessionId) =>
        db.SessionConfirms.AsNoTracking().Where(c => c.SessionId == sessionId)
            .OrderByDescending(c => c.Id).Select(c => c.ScanCode).FirstOrDefaultAsync();

    // Phiên đang mở theo tên lệnh hiện tại (lúc quét: trạm giữ tên lệnh của phiên)
    public Task<Session?> FindOpenAsync(string woName) =>
        db.Sessions.FirstOrDefaultAsync(s => s.WoName == woName && s.Status == SessionStatus.Open);

    // Phiên đang mở của đúng lệnh này: theo id lệnh Viindoo (không đổi khi lệnh đổi tên); WO thử (không có id) theo tên lệnh
    IQueryable<Session> OpenOfWo(WoContext ctx) => ctx.ViindooMoId is int moId
        ? db.Sessions.Where(s => s.ViindooMoId == moId && s.Status == SessionStatus.Open)
        : db.Sessions.Where(s => s.ViindooMoId == null && s.WoName == ctx.WoName && s.Status == SessionStatus.Open);

    // Phiên còn mở của lệnh khác trong cùng nhóm (VD phiên lệnh trước quên kết thúc rồi lệnh đã sang backorder)
    Task<Session?> FindStaleOpenOfGroupAsync(WoContext ctx)
    {
        var all = db.Sessions.Where(s => s.WoGroup == ctx.WoGroup && s.Status == SessionStatus.Open);
        var others = ctx.ViindooMoId is int moId
            ? all.Where(s => s.ViindooMoId != moId)
            : all.Where(s => s.ViindooMoId != null || s.WoName != ctx.WoName);
        return others.OrderBy(s => s.Id).FirstOrDefaultAsync();
    }

    // Xác nhận vào làm: kiểm tra lại ở server, chưa có phiên Open thì mở phiên mới, rồi lưu lần xác nhận
    public async Task<(WoContext? ctx, Session? session, string? error)> StartAsync(SessionStartRequest req)
    {
        if (!req.Confirmed) return (null, null, "Chưa tick xác nhận đã kiểm tra thông tin.");
        string employee = NormalizeEmployeeCode(req.EmployeeCode);
        if (!IsValidEmployeeCode(employee)) return (null, null, "Mã nhân viên phải có dạng SVNxxxx (x là số), VD SVN0215.");
        // Mã phải có trong danh sách nhân viên Viindoo. Không đọc được danh sách (Viindoo lỗi) thì cho qua để không dừng chuyền
        var (info, listAvailable) = await employees.FindAsync(employee);
        if (info == null && listAvailable) return (null, null, UnknownEmployeeError);
        var (printer, printerError) = await printers.ResolveAsync(req.Target);
        if (printer == null) return (null, null, printerError);

        var (ctx, error) = await LoadAsync(req.Wo);
        if (ctx == null) return (null, null, error);
        var check = Check(ctx);
        if (!check.CanStart) return (ctx, null, check.FirstError);

        var now = DateTime.Now;
        var session = await OpenOfWo(ctx).FirstOrDefaultAsync();
        if (session == null)
        {
            // Phiên của lệnh trước trong nhóm còn mở (lệnh đó đã xong / sang backorder, không mở lại được để kết thúc): kết thúc luôn
            while (await FindStaleOpenOfGroupAsync(ctx) is { } stale)
            {
                stale.Status = SessionStatus.Ended;
                stale.EndedAt = now;
                stale.EndedBy = "Tự động: mở phiên lệnh " + ctx.WoName;
                await db.SaveChangesAsync();
            }
            session = new Session
            {
                WoName = ctx.WoName,
                WoGroup = ctx.WoGroup,
                ViindooMoId = ctx.ViindooMoId,
                ProductId = ctx.Product!.ProductId,
                DefaultCode = ctx.Product.DefaultCode,
                Status = SessionStatus.Open,
                StartedAt = now
            };
            db.Sessions.Add(session);
            await db.SaveChangesAsync();
        }
        else if (session.WoName != ctx.WoName)
        {
            // Cùng lệnh (cùng id) nhưng Viindoo đã đổi tên: cập nhật tên cho khớp
            session.WoName = ctx.WoName;
        }

        // Máy xác nhận vào làm giữ phiên: lô pcs chưa gửi của máy giữ trước bị từ chối (không cộng thừa). Cùng máy mở phiên mới của
        // nhóm (lệnh sau backorder): tiếp số thứ tự quét từ phiên trước để lô gửi lại không bị cộng đôi
        string? device = NormalizeDeviceId(req.DeviceId);
        if (device != null && session.DeviceId != device)
        {
            int currentId = session.Id;
            session.LastScanSeq = await db.Sessions.AsNoTracking()
                .Where(x => x.WoGroup == ctx.WoGroup && x.DeviceId == device && x.Id != currentId)
                .OrderByDescending(x => x.Id).Select(x => x.LastScanSeq).FirstOrDefaultAsync();
            session.DeviceId = device;
        }

        // Box dở của lệnh trước trong nhóm (phiên trước để dở): chuyển sang lệnh hiện tại để quét tiếp (box chỉ đóng khi quét đủ)
        var openBox = await packages.FindCarryOverPackageAsync(ctx.WoGroup);
        if (openBox != null && openBox.WoName != ctx.WoName)
        {
            openBox.WoName = ctx.WoName;
            openBox.UpdatedAt = now;
        }
        await db.SaveChangesAsync();

        var warnings = check.Items.Where(i => i.Status == Warn).Select(i => i.Text).ToList();
        db.SessionConfirms.Add(new SessionConfirm
        {
            SessionId = session.Id,
            EmployeeCode = employee,
            EmployeeName = info?.Name is { Length: > 0 } name ? name : null,
            EmployeePhone = info?.Phone is { Length: > 0 } phone ? phone : null,
            PrinterId = printer.PrinterId,
            PrintTarget = printer.Describe(),
            ConfirmedAt = now,
            Values = check.Values.ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase),
            Warnings = warnings.Count == 0 ? null : string.Join("\n", warnings),
            ScanCode = check.ScanCode
        });
        await db.SaveChangesAsync();
        return (ctx, session, null);
    }

    // Kết thúc phiên (menu F2 ở trạm cần PIN, hoặc admin). Box đang dở: closeOpenBox = đóng thiếu + in tem; không thì giữ box mở cho
    // phiên sau. Bật công tắc thì nhập KQSX lên Viindoo (ProductionPushService), kết quả trên session.PushStatus / PushMessage.
    public async Task<(Session? session, PrintOutcome? printed, int? closedBoxId, string? error)> EndAsync(int sessionId, bool closeOpenBox, PrintTarget? printer, string endedBy)
    {
        var session = await db.Sessions.FirstOrDefaultAsync(s => s.Id == sessionId);
        if (session == null || session.Status != SessionStatus.Open) return (null, null, null, "Phiên không còn mở (đã kết thúc hoặc không tồn tại).");

        PrintOutcome? printed = null;
        var openBox = await packages.FindOpenPackageAsync(session.WoName);
        int? closedBoxId = null;
        if (closeOpenBox && openBox is { Qty: > 0 })
        {
            printed = await packages.CloseAndPrintAsync(openBox, printer);
            closedBoxId = openBox.Id;
        }

        session.Status = SessionStatus.Ended;
        session.EndedAt = DateTime.Now;
        session.EndedBy = endedBy;
        await db.SaveChangesAsync();
        // Nhập KQSX lên Viindoo (nếu bật công tắc). Lỗi không làm hỏng việc kết thúc phiên: ghi vào PushStatus / PushMessage
        await push.OnSessionEndedAsync(session);
        return (session, printed, closedBoxId, null);
    }

    // Tự kết thúc các phiên Open không hoạt động từ `limit` trở lên (SessionAutoEndService gọi mỗi phút). Lần lượt từng phiên, như
    // kết thúc tay (box dở giữ lại, nhập KQSX nếu bật công tắc); lỗi 1 phiên không chặn phiên khác. Trả về các phiên đã kết thúc.
    public async Task<List<Session>> EndInactiveAsync(TimeSpan limit, DateTime now, string endedBy, Action<Session, Exception>? failed = null)
    {
        var open = await db.Sessions.AsNoTracking().Where(s => s.Status == SessionStatus.Open).ToListAsync();
        if (open.Count == 0) return [];
        var ids = open.Select(s => s.Id).ToList();
        var lastConfirm = (await db.SessionConfirms.AsNoTracking().Where(c => ids.Contains(c.SessionId))
                .Select(c => new { c.SessionId, c.ConfirmedAt }).ToListAsync())
            .GroupBy(c => c.SessionId).ToDictionary(g => g.Key, g => g.Max(c => c.ConfirmedAt));
        var lastBox = await LastBoxActivityAsync(open.Select(s => s.WoName).Distinct().ToList());

        var ended = new List<Session>();
        foreach (var s in open)
        {
            var last = LastActivity(s, lastBox.GetValueOrDefault(s.WoName), lastConfirm.TryGetValue(s.Id, out var c) ? c : null);
            if (now - last < limit) continue;
            try
            {
                var (session, _, _, _) = await EndAsync(s.Id, closeOpenBox: false, printer: null, endedBy);
                if (session != null) ended.Add(session);
            }
            catch (Exception ex)
            {
                // Bỏ thay đổi dở của phiên lỗi, làm tiếp phiên khác; lần kiểm tra sau thử lại (phiên vẫn Open nếu chưa lưu)
                db.ChangeTracker.Clear();
                failed?.Invoke(s, ex);
            }
        }
        return ended;
    }

    // Lần cập nhật box mới nhất (quét / đóng / sửa) theo lệnh
    public async Task<Dictionary<string, DateTime>> LastBoxActivityAsync(List<string> woNames) =>
        woNames.Count == 0 ? [] : (await db.Packages.AsNoTracking().Where(p => woNames.Contains(p.WoName))
                .Select(p => new { p.WoName, At = p.UpdatedAt ?? p.ClosedAt ?? p.CreatedAt }).ToListAsync())
            .GroupBy(p => p.WoName).ToDictionary(g => g.Key, g => g.Max(p => p.At));

    // Lần hoạt động cuối của phiên đang mở = muộn nhất của: mở phiên, box mới nhất của lệnh, lần xác nhận cuối (cột Inactive ở admin)
    public static DateTime LastActivity(Session s, DateTime box, DateTime? confirm) =>
        new[] { s.StartedAt, box, confirm ?? DateTime.MinValue }.Max();

    // Tóm tắt phiên đang mở của đúng lệnh này cho modal mở phiên
    public async Task<object?> SummaryAsync(WoContext ctx)
    {
        var session = await OpenOfWo(ctx).AsNoTracking().FirstOrDefaultAsync();
        return session == null ? null : await SummaryAsync(session, ctx.WoName);
    }

    // Tóm tắt phiên đang mở cho màn làm việc (theo tên lệnh đang quét), null = chưa có
    public async Task<object?> SummaryAsync(string woName)
    {
        var session = await db.Sessions.AsNoTracking().FirstOrDefaultAsync(s => s.WoName == woName && s.Status == SessionStatus.Open);
        return session == null ? null : await SummaryAsync(session, woName);
    }

    async Task<object> SummaryAsync(Session session, string woName)
    {
        var last = await db.SessionConfirms.AsNoTracking().Where(c => c.SessionId == session.Id)
            .OrderByDescending(c => c.Id).Select(c => new { c.EmployeeCode, c.ConfirmedAt, c.ScanCode }).FirstOrDefaultAsync();
        var box = await db.Packages.AsNoTracking().FirstOrDefaultAsync(b => b.WoName == woName && b.Status == PackageStatus.Open);
        var openBox = box == null ? null : new { boxNo = box.PackageNo, box.Qty, box.Capacity };
        return new
        {
            session.Id,
            session.StartedAt,
            employeeCode = last?.EmployeeCode,
            confirmedAt = last?.ConfirmedAt,
            scanCode = last?.ScanCode,
            // Máy đang giữ phiên: trạm khác mở WO thì modal báo "phiên đang ở máy khác"
            deviceId = session.DeviceId,
            openBox
        };
    }
}
