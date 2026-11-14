using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Toast.Web.Data;

namespace Toast.Web.Features.Printer;

/// <summary>Máy in đang dùng (Toast_Printer, IsActive) cho màn In tem và màn công nhân.</summary>
public sealed class PrinterLookup(ToastDbContext db)
{
    public static readonly Regex Ipv4Regex = new(@"^((25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(25[0-5]|2[0-4]\d|1?\d?\d)$");

    public Task<List<Printer>> ListAsync() =>
        db.Printers.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();

    // Tra cấu hình máy in của trình duyệt. Máy trong danh sách tra lại IP / Port mới nhất ở DB (không tin IP lưu trên trình duyệt).
    public async Task<(ResolvedPrintTarget? target, string? error)> ResolveAsync(PrintTarget? t)
    {
        string mode = (t?.Mode ?? PrintTarget.ModeServer).Trim().ToLowerInvariant();
        if (mode is not (PrintTarget.ModeServer or PrintTarget.ModeNode or PrintTarget.ModeUsb))
            return (null, "Phương thức in không hợp lệ.");
        if (t == null) return (null, "Chưa chọn máy in.");

        if (mode == PrintTarget.ModeUsb)
        {
            string usb = (t.UsbName ?? "").Trim();
            return usb.Length == 0 ? (null, "Chưa chọn máy in USB.") : (new(mode, null, usb, null, 0, usb), null);
        }
        if (string.Equals(t.Source, "manual", StringComparison.OrdinalIgnoreCase))
        {
            string ip = (t.Ip ?? "").Trim();
            int port = t.Port ?? Printer.DefaultPort;
            if (!Ipv4Regex.IsMatch(ip)) return (null, "Địa chỉ IP máy in không hợp lệ.");
            if (port < 1 || port > 65535) return (null, "Port máy in phải từ 1 đến 65535.");
            return (new(mode, null, "Máy in nhập tay", ip, port, null), null);
        }
        var printer = await FindAsync(t.PrinterId);
        return printer == null
            ? (null, "Chưa chọn máy in, hoặc máy in đã bị xoá / ngừng dùng.")
            : (new(mode, printer.Id, printer.Name, printer.Ip, printer.Port, null), null);
    }

    // id gửi lên từ trình duyệt dạng chữ (giá trị ô chọn / localStorage). Máy đã ngừng dùng coi như không có.
    public async Task<Printer?> FindAsync(string? id) =>
        int.TryParse(id, out int printerId)
            ? await db.Printers.AsNoTracking().FirstOrDefaultAsync(p => p.Id == printerId && p.IsActive)
            : null;
}
