using System.Globalization;
using Toast.Web.Features.LabelTemplate;

namespace Toast.Web.Features.LabelPrint;

/// <summary>Từ điển biến tự điền khi in (file Configs/label-variables.json). Chuyển từ Twist label-variables.json.</summary>
public class LabelVariablesOptions
{
    public const string Section = "LabelVariables";
    public List<LabelVariableDefinition> Variables { get; set; } = [];
}

public class LabelVariableDefinition
{
    // Tên biến trong ZPL, không phân biệt hoa thường: {PO} = {po}
    public string Key { get; set; } = "";
    // Nhãn hiển thị trên ô nhập
    public string Label { get; set; } = "";
    // wo | product | package | system | manual
    public string Source { get; set; } = "manual";
    // Trường trong nguồn: wo (name, product_code, product_name, product_qty, origin),
    // product (default_code, name, barcode, pcs_per_box, outer_type, box_per_outer), package (no, qty), system (now, user)
    public string? Field { get; set; }
    // Định dạng ngày giờ cho system/now, VD: dd/MM/yyyy
    public string? Format { get; set; }
    // Được để trống: không chặn quét / in, biến in ra chuỗi rỗng
    public bool Optional { get; set; }
}

// Ngữ cảnh lúc in: WO (dạng ViindooRpc.GetWorkOrderAsync), sản phẩm, kiện, người in. Thiếu phần nào thì biến của nguồn đó để trống.
// Now: thời điểm cho biến ngày giờ (null = lúc in); in lại tem box dùng giờ đóng box để tem giống bản đầu.
public sealed record LabelContext(
    Dictionary<string, string>? Wo = null,
    Product.Product? Product = null,
    Package.Package? Package = null,
    string? UserName = null,
    DateTime? Now = null);

// Giá trị từng biến {KEY} khi in, theo thứ tự ưu tiên (mỗi sản phẩm 1 mẫu tem, tìm theo mã sản phẩm):
// 1. giá trị lưu sẵn trong mẫu  2. nguồn tự động khai báo ở LabelVariables: wo / product / package / system  3. còn lại nhập tay.
// Chuyển từ Twist LabelVariableService (bỏ nguồn sku / po_eu).
public static class LabelVariableResolver
{
    public const string SourceTemplate = "template";
    public const string SourceManual = "manual";
    static readonly string[] AutoSources = ["wo", "product", "package", "system"];

    public class VariableValue
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        // template | wo | product | package | system | manual
        public string Source { get; set; } = SourceManual;
        public string Value { get; set; } = "";
        public bool Optional { get; set; }
    }

    public static List<VariableValue> Resolve(LabelTemplate.LabelTemplate template, LabelVariablesOptions config, LabelContext context)
    {
        var result = new List<VariableValue>();
        foreach (string key in ZplVariables.ExtractKeys(template.ZplData))
        {
            var def = Definition(config, key);
            string saved = Saved(template.Variables, key);
            var item = new VariableValue
            {
                Key = key,
                Label = def?.Label is { Length: > 0 } label ? label : key,
                Source = def?.Source ?? SourceManual,
                Optional = def?.Optional ?? false
            };

            if (saved.Length > 0) { item.Source = SourceTemplate; item.Value = saved; }
            else if (def != null) item.Value = AutoValue(def, context);
            result.Add(item);
        }
        return result;
    }

    // Giá trị để điền ZPL (Key -> Value)
    public static Dictionary<string, string> Values(LabelTemplate.LabelTemplate template, LabelVariablesOptions config, LabelContext context) =>
        Resolve(template, config, context).ToDictionary(v => v.Key, v => v.Value, StringComparer.OrdinalIgnoreCase);

    // Biến không có nguồn nào (không có giá trị trong mẫu, cũng không tự điền được) -> phải nhập tay.
    // Dùng để chặn in tự động trước khi quét.
    public static List<string> MissingKeys(LabelTemplate.LabelTemplate template, LabelVariablesOptions config) =>
        ZplVariables.ExtractKeys(template.ZplData)
            .Where(key => Saved(template.Variables, key).Length == 0
                && !(Definition(config, key) is { } def && (def.Optional || AutoSources.Contains(def.Source))))
            .ToList();

    // Tên các biến được để trống
    public static HashSet<string> OptionalKeys(LabelVariablesOptions config) =>
        config.Variables.Where(v => v.Optional).Select(v => v.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

    static LabelVariableDefinition? Definition(LabelVariablesOptions config, string key) =>
        config.Variables.FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase));

    static string Saved(List<LabelVariable>? list, string key) =>
        list?.FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase))?.Value?.Trim() ?? "";

    static string AutoValue(LabelVariableDefinition def, LabelContext c)
    {
        var p = c.Product;
        return def.Source switch
        {
            "wo" => c.Wo != null && def.Field != null ? c.Wo.GetValueOrDefault(def.Field) ?? "" : "",
            "product" when p != null => def.Field switch
            {
                "default_code" => p.DefaultCode,
                "name" => p.Name,
                "barcode" => p.Barcode,
                "pcs_per_box" => p.PcsPerBox.ToString(CultureInfo.InvariantCulture),
                "outer_type" => p.OuterType ?? "",
                "box_per_outer" => p.BoxPerOuter?.ToString(CultureInfo.InvariantCulture) ?? "",
                _ => ""
            },
            "package" when c.Package != null => def.Field switch
            {
                "no" => c.Package.PackageNo.ToString(CultureInfo.InvariantCulture),
                "qty" => c.Package.Qty.ToString(CultureInfo.InvariantCulture),
                _ => ""
            },
            "system" => def.Field == "user"
                ? c.UserName ?? ""
                : (c.Now ?? DateTime.Now).ToString(string.IsNullOrEmpty(def.Format) ? "dd/MM/yyyy" : def.Format, CultureInfo.InvariantCulture),
            _ => ""
        };
    }

    // Điền giá trị vào ZPL khi in. Trả về lỗi (null = hợp lệ): còn biến bắt buộc trống hoặc giá trị chứa ^ / ~.
    // Biến trong optionalKeys được để trống (in ra chuỗi rỗng).
    public static string? Fill(string zpl, Dictionary<string, string>? values, out string filledZpl, ISet<string>? optionalKeys = null)
    {
        filledZpl = zpl;
        var input = (values ?? []).Select(kv => new LabelVariable { Key = kv.Key, Value = kv.Value ?? "" });
        string? error = ZplVariables.Normalize(zpl, input, out var normalized);
        if (error != null) return error;

        var missing = normalized.Where(v => v.Value.Length == 0 && optionalKeys?.Contains(v.Key) != true)
            .Select(v => "{" + v.Key + "}").ToList();
        if (missing.Count > 0) return "Còn biến chưa nhập: " + string.Join(", ", missing) + ".";

        filledZpl = ZplVariables.Apply(zpl, normalized, blankEmpty: true);
        return null;
    }
}
