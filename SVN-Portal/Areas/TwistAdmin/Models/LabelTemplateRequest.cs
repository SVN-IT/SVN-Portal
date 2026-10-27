namespace SVN_Portal.Areas.TwistAdmin.Models
{
    // Dữ liệu form Thêm/Sửa mẫu tem gửi lên từ /twist/admin/label-template
    public class LabelTemplateRequest
    {
        public string? partNumber { get; set; }
        public string? type { get; set; }
        public string? operation { get; set; }
        public string? size { get; set; }
        public string? zplData { get; set; }
    }
}
