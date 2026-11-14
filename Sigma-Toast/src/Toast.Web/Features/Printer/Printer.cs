namespace Toast.Web.Features.Printer;

/// <summary>
/// Máy in tem (bảng Toast_Printer). Máy in là thiết bị dùng chung, không gắn với dự án.
/// Trình duyệt chỉ nhớ Id; mỗi lần in đọc lại IP/Port mới nhất nên đổi IP máy in không phải chọn lại.
/// </summary>
public class Printer
{
    public const int DefaultPort = 9100;

    public int Id { get; set; }
    // Mã ngắn trong xưởng (VD L1-BOX), viết hoa, không trùng. Chép từ SVN_Printer_Info.ID_Printer khi chuyển dữ liệu
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Ip { get; set; } = "";
    public int Port { get; set; } = DefaultPort;
    // 203 / 300 / 600, trống = không rõ (dùng để cảnh báo khi mẫu tem khác DPI)
    public int? Dpi { get; set; }
    // Vị trí đặt máy (Line 1 Toast, Kho...)
    public string? Location { get; set; }
    // Ngừng dùng thay vì xoá: không hiện ở màn in / trạm quét, box cũ vẫn tham chiếu được
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Dữ liệu form Thêm/Sửa gửi lên từ /toast/admin/printer
public class PrinterRequest
{
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Ip { get; set; }
    public int Port { get; set; }
    public int? Dpi { get; set; }
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
}

// Thử kết nối từ form (chưa cần lưu)
public class PrinterTestRequest
{
    public string? Ip { get; set; }
    public int Port { get; set; }
}
