namespace Toast.Web.Features.LabelPrint;

// Các request của màn In tem (/toast/admin/print). Chuyển từ Twist LabelPrintRequests.cs

public class LabelPrintResolveRequest
{
    public int TemplateId { get; set; }
    // Thông tin WO đã tra trước đó (null = in không theo WO)
    public Dictionary<string, string>? Wo { get; set; }
}

public class LabelPrintRenderRequest
{
    public int TemplateId { get; set; }
    // Giá trị biến đang nhập: { "PO": "123", ... }
    public Dictionary<string, string>? Values { get; set; }
}

public class LabelPrintSendRequest : LabelPrintRenderRequest
{
    // Chọn máy trong Toast_Printer: server tự tra IP/Port theo Id
    public string? PrinterId { get; set; }
    // Hoặc nhập tay (khi PrinterId trống)
    public string? Ip { get; set; }
    public int? Port { get; set; }
    public int Copies { get; set; } = 1;
}

public class LabelPrintTestRequest
{
    public string? PrinterId { get; set; }
    public string? Ip { get; set; }
    public int? Port { get; set; }
}
