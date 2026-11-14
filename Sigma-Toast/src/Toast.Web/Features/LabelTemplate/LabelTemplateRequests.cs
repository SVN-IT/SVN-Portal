namespace Toast.Web.Features.LabelTemplate;

// Dữ liệu form Thêm/Sửa mẫu tem gửi lên từ /toast/admin/label-template
public class LabelTemplateRequest
{
    public string? DefaultCode { get; set; }
    public string? Type { get; set; }
    public string? Operation { get; set; }
    public string? Size { get; set; }
    public int Dpi { get; set; }
    public string? ZplData { get; set; }
    public List<LabelVariable>? Variables { get; set; }
}

public class LabelTemplateLockRequest
{
    public bool Locked { get; set; }
}

// Yêu cầu vẽ xem trước mẫu tem: biến có giá trị được thay vào ZPL trước khi vẽ
public class ZplPreviewRequest
{
    public string? ZplData { get; set; }
    public string? Size { get; set; }
    public List<LabelVariable>? Variables { get; set; }
    // Mật độ điểm dots/mm (6, 8, 12, 24). Bỏ trống = 24 (600 dpi)
    public int? Dpmm { get; set; }
}

// Công cụ Chuẩn hoá ZPL (ZplNormalizer): đọc danh sách chữ / barcode, áp thay đổi
public class ZplNormalizeRequest
{
    public string? ZplData { get; set; }
    public List<ZplNormalizer.Change>? Changes { get; set; }
}
