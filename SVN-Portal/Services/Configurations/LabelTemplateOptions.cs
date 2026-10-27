namespace SVN_Portal.Services.Configurations
{
    // Danh sách lựa chọn cho màn hình quản lý mẫu tem, đọc từ Configs/label-template-options.json
    public class LabelTemplateOptions
    {
        public List<LabelTemplateOption> Types { get; set; } = new();
        public List<LabelTemplateOption> Operations { get; set; } = new();
        public List<LabelTemplateOption> Sizes { get; set; } = new();
    }

    public class LabelTemplateOption
    {
        // Giá trị lưu vào DB
        public string Value { get; set; } = "";
        // Chú thích hiển thị kèm (VD: kích thước mm)
        public string? Description { get; set; }
        public bool IsDefault { get; set; }
    }
}
