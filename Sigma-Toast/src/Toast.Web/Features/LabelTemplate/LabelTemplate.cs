namespace Toast.Web.Features.LabelTemplate;

/// <summary>Mẫu tem ZPL (bảng Toast_LabelTemplate). Chuyển từ bảng SVN_label_template của Twist.</summary>
public class LabelTemplate
{
    public int Id { get; set; }
    public string DefaultCode { get; set; } = "";
    public string Type { get; set; } = "";
    // Phân biệt dự án (Toast, ...)
    public string Operation { get; set; } = "";
    public string Size { get; set; } = "";
    // DPI máy in mà ZPL được thiết kế cho (152 / 203 / 300 / 600)
    public int Dpi { get; set; } = 600;
    public string ZplData { get; set; } = "";
    // Giá trị các biến {KEY} trong ZPL, lưu dạng JSON. Value trống = điền lúc in.
    public List<LabelVariable> Variables { get; set; } = [];
    // Khoá: không cho sửa / xoá. Bật tắt tuỳ ý.
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class LabelVariable
{
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}
