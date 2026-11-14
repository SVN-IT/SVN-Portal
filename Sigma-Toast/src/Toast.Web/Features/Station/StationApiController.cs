using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Toast.Web.Data;
using Toast.Web.Features.Package;
using Toast.Web.Features.Printer;

namespace Toast.Web.Features.Station;

// API của màn công nhân /toast (không cần đăng nhập admin). Kiện (Toast_Package) hiện là box nên API / màn hình vẫn gọi là box. Chỉnh số pcs, đóng / xoá box, in thủ công cần PIN leader.
// Luồng: nhập WO -> modal mở phiên (SessionService: kiểm tra lệnh, BoM, quy cách, mẫu tem, biến tem; tick + mã nhân viên)
//        -> quét barcode cộng vào box đang mở (chỉ khi WO có phiên Open) -> đủ pcs/box thì đóng box và in tem.
[AllowAnonymous]
[Route("toast/api")]
public sealed class StationApiController(
    ToastDbContext db,
    WorkOrderReader workOrders,
    PackageService packages,
    PrinterLookup printers,
    SessionService sessions,
    StationLabelService labels,
    IOptionsMonitor<StationOptions> options,
    ClientLogService clientLog) : Controller
{
    const int RecentBoxes = 20;
    // Tối đa pcs trong 1 lô quét (trạm gửi khi đủ box; mất mạng lâu có thể dồn nhiều box)
    const int MaxScanBatch = 100000;
    const int MaxCopies = 100;

    // ===== Máy in =====

    [HttpGet("printers")]
    public async Task<IActionResult> Printers() => Json(new
    {
        success = true,
        printers = (await printers.ListAsync()).Select(p => new { p.Id, p.Code, p.Name, p.Ip, p.Port, p.Location })
    });

    // ===== Mở WO / phiên =====

    // Trạng thái WO đang làm (tải lại sau khi quét lỗi mạng...). Vào làm phải qua session/check + session/start.
    [HttpGet("wo")]
    public async Task<IActionResult> OpenWorkOrder(string wo)
    {
        var (ctx, error) = await sessions.LoadAsync(wo);
        if (ctx == null) return Json(new { success = false, error });
        if (ctx.Product == null)
            return Json(new { success = false, error = $"Sản phẩm {ctx.Info["product_code"]} (ID {ctx.Info["product_id"]}) chưa khai báo sản phẩm. Vào Quản trị → Sản phẩm để thêm." });
        return Json(new { success = true, wo = WoView(ctx), warnings = ctx.Warnings, state = await BuildState(ctx.WoName, ctx.Product, ctx.Template) });
    }

    // Modal mở phiên: danh sách kiểm tra (ok / warn / error / info), giá trị tem, phiên đang mở (nếu có thì vào lại phiên đó)
    [HttpGet("session/check")]
    public async Task<IActionResult> CheckSession(string wo)
    {
        var (ctx, error) = await sessions.LoadAsync(wo);
        if (ctx == null) return Json(new { success = false, error });
        var check = sessions.Check(ctx);
        return Json(new
        {
            success = true,
            wo = WoView(ctx),
            items = check.Items,
            values = check.Values,
            canStart = check.CanStart,
            session = await sessions.SummaryAsync(ctx)
        });
    }

    // Xác nhận (tick + mã nhân viên + máy in) rồi vào làm: mở phiên mới hoặc vào lại phiên đang mở của WO
    [HttpPost("session/start"), ValidateAntiForgeryToken]
    public async Task<IActionResult> StartSession([FromBody] SessionStartRequest req)
    {
        var (ctx, session, error) = await sessions.StartAsync(req);
        // field = "employee": lỗi ở ô mã nhân viên (màn tô đỏ ô đó)
        if (session == null || ctx == null)
            return Json(new { success = false, error, field = error == SessionService.UnknownEmployeeError ? "employee" : null });
        return Json(new { success = true, wo = WoView(ctx), warnings = ctx.Warnings, state = await BuildState(ctx.WoName, ctx.Product, ctx.Template) });
    }

    static object WoView(WoContext ctx) => new
    {
        name = ctx.WoName,
        productCode = ctx.Info["product_code"],
        productName = ctx.Info["product_name"],
        productQty = ctx.Info["product_qty"],
        origin = ctx.Info["origin"],
        // Nhóm lệnh (tên gốc): phiên kết thúc / đã nhập KQSX thì mở lại theo tên này để ra lệnh đang mở của nhóm
        group = ctx.WoGroup,
        isTest = ctx.IsTest
    };

    // ===== Mạng / in khi mất mạng =====

    // Trạm kiểm tra còn ghi được dữ liệu (icon mạng): gọi được server VÀ server kết nối được DB (tối đa 3 giây).
    // Kèm giờ server để so đồng hồ máy trạm. Không cache.
    [HttpGet("ping")]
    public async Task<IActionResult> Ping()
    {
        Response.Headers.CacheControl = "no-store";
        bool dbOk;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            // Kết nối riêng (không qua EF): mất DB thì mỗi lần ping không in khối lỗi EF ra log server.
            // Chạy hẳn 1 câu: kết nối lấy từ pool có thể đã chết mà mở vẫn "được"
            // Chuỗi kết nối gốc (GetConnectionString): ConnectionString của kết nối SQL Server đã mở bị bỏ mật khẩu
            string cs = db.Database.GetConnectionString() is { Length: > 0 } c ? c : throw new InvalidOperationException("Chưa cấu hình DB.");
            await using var conn = (System.Data.Common.DbConnection)Activator.CreateInstance(db.Database.GetDbConnection().GetType(), cs)!;
            await conn.OpenAsync(cts.Token);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync(cts.Token);
            dbOk = true;
        }
        catch (Exception)
        {
            dbOk = false;
        }
        return Json(new { success = dbOk, db = dbOk, now = DateTime.Now, error = dbOk ? null : "Server không kết nối được cơ sở dữ liệu." });
    }

    // Log lỗi từ trình duyệt trạm (gửi theo lô, mất mạng thì gửi sau): ghi file logs/client-yyyyMMdd.log để đọc khi debug
    [HttpPost("client-log"), ValidateAntiForgeryToken]
    public IActionResult ClientLog([FromBody] ClientLogRequest req)
    {
        // Ghi file lỗi (thiếu quyền...): trả 503 để trạm giữ log, gửi lại sau
        if (clientLog.Write(req, HttpContext.Connection.RemoteIpAddress?.ToString())) return Json(new { success = true });
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return Json(new { success = false, error = "Server chưa ghi được log: " + clientLog.LastWriteError });
    }

    // Mẫu tem Box của phiên đang mở để trạm lưu trên trình duyệt, dùng in qua agent (USB / TCP qua Node) khi mất mạng
    [HttpGet("label/offline")]
    public async Task<IActionResult> OfflineLabel(string? wo)
    {
        string name = NormalizeWo(wo);
        var session = await sessions.FindOpenAsync(name);
        if (session == null) return Json(new { success = false, error = $"WO {name} không có phiên làm việc đang mở." });
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == session.ProductId);
        var template = product == null ? null : await packages.FindBoxTemplateAsync(product.DefaultCode);
        if (product == null || template == null) return Json(new { success = false, error = "Chưa có quy cách hoặc mẫu tem Box." });
        var (label, error) = await packages.OfflineLabelAsync(name, session.Id, product, template);
        return label == null ? Json(new { success = false, error, now = DateTime.Now })
            : Json(new { success = true, sessionId = session.Id, label, now = DateTime.Now });
    }

    // ===== Quét =====

    [HttpPost("scan"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Scan([FromBody] ScanRequest req)
    {
        string wo = NormalizeWo(req.Wo);
        string barcode = (req.Barcode ?? "").Trim();
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == req.ProductId);
        if (wo.Length == 0 || product == null) return Json(new { success = false, error = "Chưa mở WO hoặc sản phẩm không còn quy cách." });
        if (barcode.Length == 0) return Json(new { success = false, error = "Chưa có mã quét." });

        var template = await packages.FindBoxTemplateAsync(product.DefaultCode);
        if (ScanBlocker(product, template) is { } blocked) return Json(new { success = false, error = blocked });
        var session = await sessions.FindOpenAsync(wo);
        // Không có phiên mở (chưa mở, hoặc đã kết thúc: tự động khi không hoạt động / admin): lần quét không tính, màn trạm mở lại
        // modal xác nhận phiên
        if (session == null)
            return Json(new { success = false, needSession = true, error = $"WO {wo} không có phiên làm việc đang mở (chưa mở hoặc đã kết thúc). Xác nhận lại để làm tiếp." });

        // Máy khác đã vào làm phiên này (xác nhận ở máy khác): lô của máy này bị từ chối để không cộng thừa
        string? device = SessionService.NormalizeDeviceId(req.DeviceId);
        if (device != null && session.DeviceId != null && session.DeviceId != device)
            return Json(new { success = false, otherDevice = true, error = $"Phiên làm việc của WO {wo} đã chuyển sang máy khác." });

        // Mã đúng = mã quét của lần xác nhận gần nhất (barcode cố định hoặc Lot ID của WO). Xác nhận cũ chưa lưu mã:
        // sản phẩm barcode cố định thì dùng barcode hiện tại, quét theo Lot thì bắt xác nhận lại
        string? expected = await sessions.ScanCodeAsync(session.Id)
            ?? (product.ScanSource == Product.ScanSources.Barcode ? product.Barcode : null);
        if (string.IsNullOrWhiteSpace(expected))
            return Json(new { success = false, error = $"Phiên của WO {wo} chưa có mã quét đã xác nhận. Nhập lại WO và xác nhận thông tin." });

        // Sai mã: chỉ báo "Mã sai" (đỏ), không tính, công nhân vẫn quét tiếp được. Không phân biệt hoa / thường
        if (!string.Equals(barcode, expected.Trim(), StringComparison.OrdinalIgnoreCase))
            return Json(new { success = true, result = "wrong", message = "Mã sai.", state = await BuildState(wo, product, template) });

        // Số pcs của lô: bỏ phần đã nhận (số thứ tự <= LastScanSeq của phiên) để gửi lại không bị cộng đôi
        int count = req.Count;
        long? received = session.LastScanSeq;
        if (req.FromSeq is long from && req.ToSeq is long to)
        {
            if (to < from || to - from + 1 != req.Count) return Json(new { success = false, error = "Lô quét không hợp lệ." });
            long already = session.LastScanSeq is long last && last >= from ? Math.Min(last, to) - from + 1 : 0;
            count = (int)(req.Count - already);
            if (session.LastScanSeq is not long l || to > l) session.LastScanSeq = to;
        }
        if (count < 0 || count > MaxScanBatch) return Json(new { success = false, error = "Lô quét không hợp lệ." });
        if (req.Boxes != null && req.FromSeq != null)
            return await ScanBoxes(req, wo, product, template, session, received);

        // Box trạm đã in lúc mất mạng (qua agent): theo thứ tự lần quét làm đủ box, bỏ box thuộc phần server đã nhận
        var offline = req.FromSeq == null ? [] : (req.OfflineBoxes ?? [])
            .Where(o => received is not long r || o.EndSeq > r).OrderBy(o => o.EndSeq).ToList();
        string? templateVersion = template == null ? null : PackageService.TemplateVersion(template);

        // Cộng vào box Open (chưa có thì mở box mới), đủ box thì đóng + in tem rồi sang box sau (lô gửi lúc mất mạng có thể đủ nhiều box)
        var closed = new List<object>();
        PrintOutcome? printed = null;
        int? closedBoxId = null, closedBoxNo = null;
        while (count > 0)
        {
            var box = await packages.GetOrCreateOpenPackageAsync(wo, session.WoGroup, product);
            int take = Math.Min(count, Math.Max(1, box.Capacity - box.Qty));
            box.Qty += take;
            box.UpdatedAt = DateTime.Now;
            count -= take;
            if (box.Qty < box.Capacity) break;
            if (offline.Count > 0)
            {
                // Đã in ở trạm lúc mất mạng: đóng theo giờ in offline, không in lại
                var printedBox = offline[0];
                offline.RemoveAt(0);
                await packages.CloseOfflineAsync(box, printedBox, templateVersion);
                closed.Add(new { boxId = box.Id, boxNo = box.PackageNo, printed = (PrintOutcome?)null, offline = true, printedBoxNo = (int?)printedBox.BoxNo });
            }
            else
            {
                printed = await packages.CloseAndPrintAsync(box, req.Target);
                closed.Add(new { boxId = box.Id, boxNo = box.PackageNo, printed, offline = false, printedBoxNo = (int?)null });
            }
            // Id chỉ có sau khi box mới được lưu (lúc đóng)
            closedBoxId = box.Id;
            closedBoxNo = box.PackageNo;
        }
        await db.SaveChangesAsync();

        return Json(new { success = true, result = "ok", closedBoxId, closedBoxNo, printed, closed, state = await BuildState(wo, product, template) });
    }

    // Lô có danh sách box chưa đồng bộ (trạm tự giữ lúc mất kết nối, có thể đã sửa / xoá / đóng thiếu): đóng từng box đúng số pcs theo thứ tự
    // (box trạm đã in thì ghi nhận, không in lại; chưa in thì server in bù), rồi box đang quét = OpenQty. LastScanSeq đã cập nhật ở Scan.
    // received = số thứ tự đã nhận trước lô này: cả lô đã nhận thì bỏ qua; box có EndSeq <= received đã đóng ở lần gửi trước
    async Task<IActionResult> ScanBoxes(ScanRequest req, string wo, Product.Product product, LabelTemplate.LabelTemplate? template,
        Session session, long? received)
    {
        if (received is long r && req.ToSeq <= r)
            return Json(new { success = true, result = "ok", closed = Array.Empty<object>(), state = await BuildState(wo, product, template) });
        var boxes = req.Boxes!.Where(b => received is not long rr || b.EndSeq > rr).ToList();
        if (boxes.Any(b => b.Qty < 1 || b.Qty > MaxScanBatch) || req.OpenQty is < 0 or > MaxScanBatch)
            return Json(new { success = false, error = "Lô quét không hợp lệ." });
        string? templateVersion = template == null ? null : PackageService.TemplateVersion(template);

        var closed = new List<object>();
        PrintOutcome? printed = null;
        int? closedBoxId = null, closedBoxNo = null;
        foreach (var item in boxes)
        {
            var box = await packages.GetOrCreateOpenPackageAsync(wo, session.WoGroup, product);
            box.Qty = item.Qty;
            box.UpdatedAt = DateTime.Now;
            if (item.Printed)
            {
                await packages.CloseOfflineAsync(box, item, templateVersion);
                closed.Add(new { boxId = box.Id, boxNo = box.PackageNo, printed = (PrintOutcome?)null, offline = true, printedBoxNo = (int?)item.BoxNo });
            }
            else
            {
                printed = await packages.CloseAndPrintAsync(box, req.Target);
                closed.Add(new { boxId = box.Id, boxNo = box.PackageNo, printed, offline = false, printedBoxNo = (int?)null });
            }
            closedBoxId = box.Id;
            closedBoxNo = box.PackageNo;
        }
        // Box đang quét: số tuyệt đối từ trạm. 0 pcs (trạm đã xoá box đang quét / chưa quét) = như Xoá box: bỏ box đang mở
        // (quét pcs tiếp theo mới mở box mới)
        int openQty = req.OpenQty ?? 0;
        var open = openQty > 0
            ? await packages.GetOrCreateOpenPackageAsync(wo, session.WoGroup, product)
            : await packages.FindOpenPackageAsync(wo);
        if (open != null && openQty == 0) db.Packages.Remove(open);
        else if (open != null && open.Qty != openQty)
        {
            open.Qty = openQty;
            open.UpdatedAt = DateTime.Now;
        }
        await db.SaveChangesAsync();
        return Json(new { success = true, result = "ok", closedBoxId, closedBoxNo, printed, closed, state = await BuildState(wo, product, template) });
    }

    // ===== Chỉnh box (PIN) =====

    // Đổi số pcs của box (đang mở hoặc đã đóng). Box đang mở mà đủ pcs/box thì đóng và in luôn.
    [HttpPost("box/{id:int}/qty"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetQty(int id, [FromBody] BoxQtyRequest req)
    {
        if (CheckPin(req.Pin) is { } pinError) return Json(new { success = false, error = pinError });
        if (req.Qty < 1) return Json(new { success = false, error = "Số pcs phải từ 1 trở lên. Muốn bỏ box thì xoá box." });

        var box = await db.Packages.FirstOrDefaultAsync(p => p.Id == id);
        if (box == null) return Json(new { success = false, error = "Không tìm thấy box." });

        box.Qty = req.Qty;
        box.UpdatedAt = DateTime.Now;
        PrintOutcome? printed = null;
        if (box.Status == PackageStatus.Open && box.Qty >= box.Capacity)
            printed = await packages.CloseAndPrintAsync(box, req.Target);
        else
        {
            await packages.SyncDashboardLogAsync(box); // box đã đóng: sửa luôn số pcs ở dòng log dashboard
            await db.SaveChangesAsync();
        }
        return Json(new { success = true, boxId = box.Id, message = $"Box {box.PackageNo}: {box.Qty} pcs", printed, state = await BuildStateFor(box) });
    }

    // Đóng box đang mở dù chưa đủ pcs (VD box cuối của WO) rồi in tem
    [HttpPost("box/{id:int}/close"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id, [FromBody] PinRequest req)
    {
        if (CheckPin(req.Pin) is { } pinError) return Json(new { success = false, error = pinError });
        var box = await db.Packages.FirstOrDefaultAsync(p => p.Id == id);
        if (box == null || box.Status != PackageStatus.Open) return Json(new { success = false, error = "Không có box đang mở." });
        if (box.Qty < 1) return Json(new { success = false, error = "Box chưa có pcs nào." });

        var printed = await packages.CloseAndPrintAsync(box, req.Target);
        return Json(new { success = true, boxId = box.Id, message = $"Đã đóng box {box.PackageNo} ({box.Qty} pcs)", printed, state = await BuildStateFor(box) });
    }

    [HttpPost("box/{id:int}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, [FromBody] PinRequest req)
    {
        if (CheckPin(req.Pin) is { } pinError) return Json(new { success = false, error = pinError });
        var box = await db.Packages.FirstOrDefaultAsync(p => p.Id == id);
        if (box == null) return Json(new { success = false, error = "Không tìm thấy box." });

        await packages.SyncDashboardLogAsync(box, deleted: true); // xoá luôn dòng log dashboard của box
        db.Packages.Remove(box);
        await db.SaveChangesAsync();
        return Json(new { success = true, message = $"Đã xoá box {box.PackageNo}", state = await BuildStateFor(box) });
    }

    // In lại tem box đã đóng: mẫu tem hiện tại + giá trị đã xác nhận ở phiên lúc đóng box
    [HttpPost("box/{id:int}/reprint"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reprint(int id, [FromBody] PinRequest req)
    {
        if (CheckPin(req.Pin) is { } pinError) return Json(new { success = false, error = pinError });
        var box = await db.Packages.FirstOrDefaultAsync(p => p.Id == id);
        if (box == null) return Json(new { success = false, error = "Không tìm thấy box." });
        if (box.Status != PackageStatus.Closed) return Json(new { success = false, error = $"Box {box.PackageNo} đang quét, chưa có tem để in lại." });

        var printed = await packages.ReprintAsync(box, req.Target);
        bool ok = printed.Ok || printed.Pending;
        return Json(new
        {
            success = ok,
            boxId = box.Id,
            printed,
            message = ok ? $"Đã gửi in lại tem box {box.PackageNo} · {printed.PrinterName}" : null,
            error = ok ? null : printed.Error,
            state = await BuildStateFor(box)
        });
    }

    // ===== Menu F2 (PIN) =====

    // Mở khoá menu: chỉ kiểm tra PIN, các thao tác sau vẫn gửi kèm PIN
    [HttpPost("pin"), ValidateAntiForgeryToken]
    public IActionResult VerifyPin([FromBody] PinRequest req) =>
        Json(CheckPin(req.Pin) is { } error ? new { success = false, error = (string?)error } : new { success = true, error = (string?)null });

    // Kết thúc phiên. closeOpenBox = đóng box đang dở (thiếu) và in tem; không thì giữ box cho phiên sau
    [HttpPost("session/{id:int}/end"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EndSession(int id, [FromBody] EndSessionRequest req)
    {
        if (CheckPin(req.Pin) is { } pinError) return Json(new { success = false, error = pinError });
        var (session, printed, closedBoxId, error) = await sessions.EndAsync(id, req.CloseOpenBox, req.Target, "Trạm (PIN leader)");
        if (session == null) return Json(new { success = false, error });
        return Json(new
        {
            success = true, message = $"Đã kết thúc phiên #{session.Id} · {session.WoName}", printed, boxId = closedBoxId,
            push = new { status = session.PushStatus, message = session.PushMessage }
        });
    }

    // Trình duyệt báo kết quả gửi tem box tới agent (TCP / USB qua Node). Không cần PIN: chỉ ghi đã in / lỗi in của box.
    [HttpPost("box/{id:int}/print-result"), ValidateAntiForgeryToken]
    public async Task<IActionResult> PrintResult(int id, [FromBody] PrintResultRequest req)
    {
        var box = await db.Packages.FirstOrDefaultAsync(p => p.Id == id);
        if (box == null || box.Status != PackageStatus.Closed) return Json(new { success = false, error = "Không tìm thấy box đã đóng." });
        await packages.ReportAgentResultAsync(box, req.Ok, req.Error);
        return Json(new { success = true, state = await BuildStateFor(box) });
    }

    // Modal Cấu hình máy in → Kiểm tra kết nối (Server TCP): chỉ mở / đóng kết nối, không in
    [HttpPost("test-connection"), ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection([FromBody] PrintTarget req)
    {
        var (printer, error) = await printers.ResolveAsync(req);
        if (printer == null || printer.Ip == null) return Json(new { success = false, error = error ?? "Chỉ kiểm tra được máy in TCP." });
        var result = await LabelPrint.TcpPrintService.TestAsync(printer.Ip, printer.Port);
        return Json(new { success = result.Ok, error = result.Error, elapsedMs = result.ElapsedMs });
    }

    // ===== Menu → In tem (PIN): chọn mẫu tem, WO (không bắt buộc) để điền dữ liệu; trong hoặc ngoài phiên (StationLabelService) =====

    // Mẫu tem (+ WO): giá trị biến gợi ý. Chỉ đọc, không cần PIN (in mới cần).
    [HttpGet("label/context")]
    public async Task<IActionResult> GetLabelContext(int? templateId, string? wo) => Json(await labels.ContextAsync(templateId, wo));

    // Đọc WO: thông tin WO + các mẫu tem của sản phẩm (giống màn In tem admin)
    [HttpGet("label/wo")]
    public async Task<IActionResult> GetLabelWo(string? wo) => Json(await labels.WorkOrderAsync(wo));

    // Tìm mẫu tem theo mã sản phẩm
    [HttpGet("label/templates")]
    public async Task<IActionResult> SearchLabelTemplates(string? q) => Json(new { success = true, templates = await labels.SearchTemplatesAsync(q) });

    // Xem trước tem (giống màn Mẫu tem / In tem): biến có giá trị được thay vào ZPL
    [HttpPost("label/preview"), ValidateAntiForgeryToken]
    public IActionResult PreviewLabel([FromBody] LabelTemplate.ZplPreviewRequest req) => Json(LabelTemplate.ZplPreviewService.PreviewResponse(req));

    // In mẫu tem với giá trị người in đã xem / sửa. ZPL lấy lại từ DB theo templateId.
    [HttpPost("label/print"), ValidateAntiForgeryToken]
    public async Task<IActionResult> PrintLabel([FromBody] StationLabelPrintRequest req)
    {
        if (CheckPin(req.Pin) is { } pinError) return Json(new { success = false, error = pinError });
        if (req.Copies < 1 || req.Copies > MaxCopies) return Json(new { success = false, error = $"Số bản phải từ 1 đến {MaxCopies}." });
        var template = await db.LabelTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == req.TemplateId);
        if (template == null) return Json(new { success = false, error = "Không tìm thấy mẫu tem." });

        var values = (req.Values ?? []).ToDictionary(kv => kv.Key, kv => (kv.Value ?? "").Trim(), StringComparer.OrdinalIgnoreCase);
        var printed = await packages.PrintValuesAsync(template, values, req.Target, req.Copies);
        bool ok = printed.Ok || printed.Pending;
        return Json(new
        {
            success = ok,
            printed,
            message = ok ? $"Đã gửi {req.Copies} bản tem {template.DefaultCode} · {template.Type} tới {printed.PrinterName}" : null,
            error = ok ? null : printed.Error
        });
    }

    // ===== Helpers =====

    // "06229" -> "NM/MO/06229"; "nm/mo/02533-002" -> "NM/MO/02533-002"
    internal static string NormalizeWo(string? wo) => SessionService.NormalizeWo(wo);

    // Lý do chưa quét được (null = quét được): chưa có mẫu tem Box theo mã sản phẩm, hoặc mẫu có biến không lấy được giá trị lúc in tự động
    string? ScanBlocker(Product.Product? product, LabelTemplate.LabelTemplate? template)
    {
        if (product == null) return "Sản phẩm không còn quy cách.";
        if (template == null)
            return $"Chưa có mẫu tem Box cho mã sản phẩm {product.DefaultCode}. Vào Quản trị → Mẫu tem, thêm mẫu có Mã sản phẩm = {product.DefaultCode}, Loại tem = {PackageService.BoxType}.";
        var missing = packages.MissingVariables(template);
        return missing.Count == 0 ? null
            : $"Mẫu tem {template.DefaultCode} còn biến chưa có giá trị: {string.Join(", ", missing.Select(k => "{" + k + "}"))}. Vào Quản trị → Mẫu tem để điền.";
    }

    string? CheckPin(string? pin)
    {
        if (!options.CurrentValue.RequireLeaderPin) return null;
        string expected = options.CurrentValue.LeaderPin ?? "";
        if (expected.Length == 0) return "Chưa cấu hình PIN leader (Toast:LeaderPin).";
        return string.Equals((pin ?? "").Trim(), expected, StringComparison.Ordinal) ? null : "PIN leader không đúng.";
    }

    async Task<object> BuildStateFor(Package.Package box)
    {
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == box.ProductId);
        var template = product == null ? null : await packages.FindBoxTemplateAsync(product.DefaultCode);
        return await BuildState(box.WoName, product, template);
    }

    // Trạng thái hiển thị trên màn công nhân: sản phẩm, mẫu tem Box, box đang mở, tổng pcs / box, các box gần nhất
    async Task<object> BuildState(string wo, Product.Product? product, LabelTemplate.LabelTemplate? template)
    {
        var all = db.Packages.AsNoTracking().Where(b => b.WoName == wo);
        var open = await all.FirstOrDefaultAsync(b => b.Status == PackageStatus.Open);
        int totalPcs = await all.SumAsync(b => (int?)b.Qty) ?? 0;
        int closedBoxes = await all.CountAsync(b => b.Status == PackageStatus.Closed);
        var recent = await all.OrderByDescending(b => b.PackageNo).Take(RecentBoxes)
            .Select(b => new { b.Id, boxNo = b.PackageNo, b.Qty, b.Capacity, b.Status, b.PrintedAt, b.PrintError, b.ClosedAt, b.PrintTarget })
            .ToListAsync();

        return new
        {
            woName = wo,
            product = product == null ? null : new { product.ProductId, product.DefaultCode, product.Name, product.ScanSource, product.Barcode, product.PcsPerBox },
            // version: đổi khi mẫu được sửa (trạm tải lại mẫu lưu để in offline)
            template = template == null ? null : new { template.Id, template.DefaultCode, template.Type, template.Size, template.Dpi, version = PackageService.TemplateVersion(template) },
            canScan = ScanBlocker(product, template) == null,
            blocker = ScanBlocker(product, template),
            openBox = open == null ? null : new { open.Id, boxNo = open.PackageNo, open.Qty, open.Capacity },
            totalPcs,
            closedBoxes,
            session = await sessions.SummaryAsync(wo),
            boxes = recent
        };
    }
}
