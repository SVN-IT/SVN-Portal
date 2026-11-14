namespace Toast.Web.Features.Printer;

/// <summary>
/// Cấu hình máy in gửi lên từ trình duyệt (modal "Cấu hình máy in" dùng chung, lưu localStorage toast.print.config):
/// Mode server = server gửi ZPL tới IP; node = trình duyệt gửi qua agent localhost:8021 tới IP; usb = agent in ra máy USB.
/// Source list = máy trong Toast_Printer (PrinterId); manual = IP / Port nhập tay.
/// </summary>
public class PrintTarget
{
    public const string ModeServer = "server", ModeNode = "node", ModeUsb = "usb";

    public string? Mode { get; set; } = ModeServer;
    public string? Source { get; set; } = "list";
    public string? PrinterId { get; set; }
    public string? Ip { get; set; }
    public int? Port { get; set; }
    public string? UsbName { get; set; }
}

/// <summary>Máy in đã tra xong. PrinterId chỉ có khi chọn máy trong danh sách.</summary>
public sealed record ResolvedPrintTarget(string Mode, int? PrinterId, string Name, string? Ip, int Port, string? UsbName)
{
    public bool ViaAgent => Mode != PrintTarget.ModeServer;

    static readonly Dictionary<string, string> ModeLabels = new()
    {
        [PrintTarget.ModeServer] = "Server TCP", [PrintTarget.ModeNode] = "TCP qua Node", [PrintTarget.ModeUsb] = "USB qua Node"
    };

    // Ghi vào Toast_Package / Toast_SessionConfirm để biết tem in ra đâu, VD "Server TCP · Máy line 2 · 10.10.99.52:9100"
    public string Describe() => Mode == PrintTarget.ModeUsb
        ? $"{ModeLabels[Mode]} · {Name}"
        : $"{ModeLabels[Mode]} · {Name} · {Ip}:{Port}";
}

/// <summary>Việc in trình duyệt phải gửi tới agent trên máy trạm (chế độ node / usb): Path = /print hoặc /print-usb.</summary>
public sealed record AgentPrintJob(string Path, string Zpl, string? PrinterIp, int? PrinterPort, string? PrinterName);
