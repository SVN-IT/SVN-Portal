namespace SVN_Portal.Areas.TwistAdmin.Models
{
    // Yêu cầu vẽ xem trước mẫu tem: po (nếu có) sẽ thay vào {PO} giống lúc in thật
    public class ZplPreviewRequest
    {
        public string? zplData { get; set; }
        public string? size { get; set; }
        public string? po { get; set; }
        // Mật độ điểm dots/mm (6, 8, 12, 24). Bỏ trống = 24 (600 dpi)
        public int? dpmm { get; set; }
    }
}
