namespace SVN_Portal.Areas.TwistAdmin.Models
{
    // Các request của màn In tem (/twist/admin/print)

    public class LabelPrintResolveRequest
    {
        public int templateId { get; set; }
        // Thông tin WO đã tra trước đó (null = in không theo WO)
        public Dictionary<string, string>? wo { get; set; }
    }

    public class LabelPrintRenderRequest
    {
        public int templateId { get; set; }
        public Dictionary<string, string>? values { get; set; }
    }

    public class LabelPrintSendRequest : LabelPrintRenderRequest
    {
        // Chọn máy trong SVN_Printer_Info: server tự tra IP/Port theo ID
        public string? printerId { get; set; }
        // Hoặc nhập tay (khi printerId trống)
        public string? ip { get; set; }
        public int? port { get; set; }
        public int copies { get; set; } = 1;
    }

    public class LabelPrintTestRequest
    {
        public string? printerId { get; set; }
        public string? ip { get; set; }
        public int? port { get; set; }
    }

    // Máy in hiển thị trên màn In tem (chỉ thông tin cần thiết, không gửi ZPL_Temp...)
    public class LabelPrintPrinter
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Ip { get; set; } = "";
        public int Port { get; set; }
    }
}
