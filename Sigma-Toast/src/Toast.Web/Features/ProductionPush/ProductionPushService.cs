using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Toast.Web.Data;
using Toast.Web.Features.Package;
using Toast.Web.Features.Station;
using Toast.Web.Features.Viindoo;

namespace Toast.Web.Features.ProductionPush;

/// <summary>
/// Nhập KQSX lên Viindoo khi kết thúc phiên (bật bằng Toast:PushProduction trong appsettings) hoặc bấm Đẩy KQSX ở admin.
/// Số nhập = tổng pcs box đã đóng của lệnh (Toast_Package.WoName = tên lệnh của phiên), tối đa SL còn lại của lệnh; box dở không tính
/// (sang lệnh sau). Thành công: lệnh done trên Viindoo, tên lệnh trong phiên / box đổi theo Viindoo (VD 06384 → 06384-001), lần sau
/// mở WO vào lệnh backorder (-002) = phiên mới. Kết quả lưu trên phiên (PushStatus, PushQty, PushedAt, PushMessage).
/// Không nhập: WO thử, công tắc tắt, không có box đã đóng. Lỗi Viindoo: phiên vẫn kết thúc, admin bấm Đẩy KQSX để thử lại.
/// Lệnh đã done khi nhập (không ghi gì): đối chiếu với lần nhập trước của phiên (PushDetail). Lần trước Toast đã gửi lệnh hoàn thành
/// (kể cả khi lỗi) + số đã sản xuất = số Toast gửi + giờ hoàn thành lệch ≤ 2 phút → Toast đã nhập thành công (Done, VD mất kết nối
/// đúng lúc Viindoo xong); không khớp → bên khác đã hoàn thành (Mismatch "Lệch", Toast không tự xử lý).
/// </summary>
public sealed class ProductionPushService(ToastDbContext db, ViindooRpc viindoo, IOptionsMonitor<StationOptions> options)
{
    public bool IsEnabled => options.CurrentValue.PushProduction;

    // Lúc kết thúc phiên (phiên đã chuyển Ended, chưa lưu hoặc đã lưu đều được): ghi kết quả vào phiên rồi lưu
    public async Task OnSessionEndedAsync(Session session)
    {
        if (session.ViindooMoId == null)
            Set(session, PushStatus.Skipped, null, "WO thử: không nhập KQSX.");
        else if (!IsEnabled)
            Set(session, PushStatus.Off, null, "Nhập KQSX đang tắt (Toast:PushProduction).");
        else
        {
            await PushAsync(session);
            return;
        }
        await db.SaveChangesAsync();
    }

    // Nhập KQSX cho lệnh của phiên (gọi khi kết thúc phiên, hoặc admin bấm Đẩy KQSX với phiên Off / Error)
    public async Task PushAsync(Session session)
    {
        if (session.ViindooMoId is not int moId)
        {
            Set(session, PushStatus.Skipped, null, "WO thử: không nhập KQSX.");
            await db.SaveChangesAsync();
            return;
        }
        string oldName = session.WoName;
        int pcs = await db.Packages.Where(p => p.WoName == oldName && p.Status == PackageStatus.Closed).SumAsync(p => (int?)p.Qty) ?? 0;
        if (pcs <= 0)
        {
            Set(session, PushStatus.Skipped, 0, "Không có box đã đóng để nhập.");
            await db.SaveChangesAsync();
            return;
        }

        var prior = ParseTrace(session.PushDetail);
        // Bằng chứng lần gửi trước: lần thử cũ (trước khi có LastSent) thì lấy từ bước mark_done của nó
        var priorSent = prior?.LastSent ?? (prior?.Steps.LastOrDefault(x => x.Step == "mark_done") is { } md ? new PushSent(prior.PushQty, md.At) : null);
        var trace = new ProductionPushTrace { LastSent = priorSent };
        try
        {
            var r = await viindoo.PushProductionAsync(moId, pcs, trace);
            string msg = $"Đã nhập {r.Qty} pcs, lệnh {r.DoneName} hoàn thành" +
                (r.BackorderName != null ? $", phần còn lại sang {r.BackorderName}" : "") + "." +
                (r.Excess > 0 ? $" Thừa {r.Excess} pcs so với SL lệnh: không nhập." : "");
            Set(session, PushStatus.Done, r.Qty, msg);
            await RenameAsync(session, moId, oldName, r.DoneName);
        }
        catch (MoAlreadyDoneException done)
        {
            await ReconcileAsync(session, moId, oldName, priorSent, done.Info, trace);
        }
        catch (Exception ex)
        {
            Set(session, PushStatus.Error, null, ex.Message);
        }
        session.PushDetail = JsonSerializer.Serialize(trace, JsonOptions);
        await db.SaveChangesAsync();
    }

    static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    static readonly TimeSpan FinishTolerance = TimeSpan.FromMinutes(2);

    // Lệnh đã done khi Toast định nhập: Toast đã nhập lần trước (mất kết quả) hay bên khác đã hoàn thành. Là Toast khi số đã sản xuất
    // khớp và giờ hoàn thành lệch ≤ 2 phút so với: lần Toast gửi lệnh hoàn thành (LastSent), hoặc giờ kết thúc phiên (Toast nhập ngay
    // khi kết thúc phiên; giờ này không bị ghi đè dù chi tiết lần thử bị thay) với số = tổng pcs box đã đóng.
    async Task ReconcileAsync(Session session, int moId, string oldName, PushSent? sent, MoDoneInfo mo, ProductionPushTrace trace)
    {
        bool Near(DateTime at) => mo.DateFinished is DateTime f && (f - at).Duration() <= FinishTolerance;
        bool bySent = sent != null && sent.Qty > 0 && mo.QtyProduced == sent.Qty && Near(sent.At);
        bool byEnd = !bySent && session.EndedAt is DateTime ended && trace.ToastQty > 0 && mo.QtyProduced == trace.ToastQty && Near(ended);
        string when = mo.DateFinished?.ToString("HH:mm dd/MM/yyyy") ?? "?";
        string by = mo.DoneBy.Length > 0 ? mo.DoneBy : "?";
        string note = $"Viindoo: {mo.Name} hoàn thành {mo.QtyProduced:0.##} pcs lúc {mo.DateFinished:HH:mm:ss dd/MM/yyyy} bởi {by}. " +
            (sent != null ? $"Toast gửi hoàn thành lệnh {sent.Qty} pcs lúc {sent.At:HH:mm:ss dd/MM/yyyy}. " : "Không có lần Toast gửi hoàn thành lệnh. ") +
            $"Phiên kết thúc lúc {session.EndedAt:HH:mm:ss dd/MM/yyyy}, Toast có {trace.ToastQty} pcs.";
        (trace.Mo.NameAfter, trace.Mo.StateAfter, trace.Backorder) = (mo.Name, "done", mo.Backorder);

        if (bySent || byEnd)
        {
            int qty = (int)mo.QtyProduced;
            trace.PushQty = qty;
            trace.Steps.Add(new PushStep("reconcile", DateTime.Now, true,
                Note: note + (bySent ? " Khớp lần Toast gửi." : " Khớp giờ kết thúc phiên.")));
            Set(session, PushStatus.Done, qty, $"Đã nhập {qty} pcs (xác nhận lại từ Viindoo): lệnh {mo.Name} hoàn thành lúc {when}" +
                (mo.Backorder != null ? $", phần còn lại ở {mo.Backorder}" : "") + ".");
            await RenameAsync(session, moId, oldName, mo.Name);
        }
        else
        {
            trace.Steps.Add(new PushStep("reconcile", DateTime.Now, false, "Số lượng / giờ hoàn thành không khớp lần nhập nào của Toast.", Note: note));
            Set(session, PushStatus.Mismatch, null, $"Lệnh {mo.Name} đã được hoàn thành bởi {by} lúc {when} với {mo.QtyProduced:0.##} pcs, " +
                $"không khớp lần nhập nào của Toast. Toast có {trace.ToastQty} pcs: cần kiểm tra.");
        }
    }

    // Viindoo đổi tên lệnh (06384 → 06384-001): đổi theo ở box đã đóng + các phiên của lệnh (box dở giữ tên, sang lệnh sau)
    async Task RenameAsync(Session session, int moId, string oldName, string newName)
    {
        if (newName.Length == 0 || newName == oldName) return;
        foreach (var box in await db.Packages.Where(p => p.WoName == oldName && p.Status == PackageStatus.Closed).ToListAsync())
            box.WoName = newName;
        foreach (var s in await db.Sessions.Where(s => s.ViindooMoId == moId).ToListAsync())
            s.WoName = newName;
        session.WoName = newName;
    }

    static ProductionPushTrace? ParseTrace(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<ProductionPushTrace>(json, JsonOptions); }
        catch (JsonException) { return null; }
    }

    static void Set(Session s, string status, int? qty, string message)
    {
        s.PushStatus = status;
        s.PushQty = qty;
        s.PushedAt = DateTime.Now;
        s.PushMessage = message.Length > 500 ? message[..500] : message;
    }
}
