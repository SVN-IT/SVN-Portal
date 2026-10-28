namespace SVN_Portal.Services.Configurations
{
    // Từ điển biến {KEY} trong mẫu ZPL cho màn In tem (/twist/admin/print), đọc từ Configs/label-variables.json
    public class LabelVariablesConfig
    {
        public List<LabelVariableDefinition> Variables { get; set; } = new();
    }

    public class LabelVariableDefinition
    {
        // Tên biến trong ZPL, không phân biệt hoa thường: {PO} = {po}
        public string Key { get; set; } = "";
        // Nhãn hiển thị trên ô nhập
        public string Label { get; set; } = "";
        // wo | sku | po_eu | system | manual
        public string Source { get; set; } = "manual";
        // Trường trong nguồn: wo (name, product_code, product_name, product_qty, origin), sku (barcode, sku, pcsPerBox), system (now, user)
        public string? Field { get; set; }
        // Định dạng ngày giờ cho system/now, VD: dd/MM/yyyy
        public string? Format { get; set; }
        // true: được phép để trống khi in
        public bool Optional { get; set; }
    }
}
