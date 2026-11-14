namespace Toast.Web.Features.LabelTemplate;

/// <summary>Danh sách lựa chọn cho màn Mẫu tem, đọc từ mục LabelTemplateOptions trong appsettings.json.</summary>
public class LabelTemplateOptions
{
    public const string Section = "LabelTemplateOptions";
    public List<LabelTemplateOption> Types { get; set; } = [];
    public List<LabelTemplateOption> Operations { get; set; } = [];
    public List<LabelTemplateOption> Sizes { get; set; } = [];
}

public class LabelTemplateOption
{
    // Giá trị lưu vào DB
    public string Value { get; set; } = "";
    // Chú thích hiển thị kèm (VD: kích thước mm)
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
}
