using System.Globalization;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using SVN_Portal.Services.Configurations;

namespace SVN_Portal.Areas.TwistAdmin.Services
{
    // Tìm biến {KEY} trong ZPL, tự điền theo từ điển (Configs/label-variables.json) và thay giá trị vào ZPL trước khi in.
    public class LabelVariableService
    {
        // {KEY}: chữ cái hoặc _ ở đầu, sau đó chữ/số/_. Không phân biệt hoa thường.
        static readonly Regex VariableRegex = new(@"\{([A-Za-z_][A-Za-z0-9_]*)\}");
        // Ký tự mở lệnh ZPL: có trong giá trị sẽ phá vỡ mẫu tem
        static readonly char[] ZplControlChars = { '^', '~' };

        readonly LabelVariablesConfig config;
        readonly string webRootPath;

        public LabelVariableService(LabelVariablesConfig config, string webRootPath)
        {
            this.config = config;
            this.webRootPath = webRootPath;
        }

        public class VariableValue
        {
            public string Key { get; set; } = "";
            public string Label { get; set; } = "";
            public string Source { get; set; } = "manual";
            public string Value { get; set; } = "";
            public bool Optional { get; set; }
        }

        // Danh sách tên biến xuất hiện trong ZPL, theo thứ tự, không trùng (không phân biệt hoa thường)
        public static List<string> ExtractKeys(string zpl)
        {
            var keys = new List<string>();
            foreach (Match m in VariableRegex.Matches(zpl ?? ""))
            {
                string key = m.Groups[1].Value;
                if (!keys.Any(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase)))
                    keys.Add(key);
            }
            return keys;
        }

        // wo: thông tin WO (có thể null khi in không theo WO). productCode: mã SP để tra file JSON của Twist.
        public List<VariableValue> Resolve(string zpl, Dictionary<string, string>? wo, string? productCode, string? userName)
        {
            Dictionary<string, List<string>>? skuData = null;
            Dictionary<string, string>? poData = null;

            var result = new List<VariableValue>();
            foreach (string key in ExtractKeys(zpl))
            {
                var def = config.Variables.FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase));
                var item = new VariableValue
                {
                    Key = key,
                    Label = def?.Label is { Length: > 0 } label ? label : key,
                    Source = def?.Source ?? "manual",
                    Optional = def?.Optional ?? false
                };

                if (def != null)
                {
                    switch (def.Source)
                    {
                        case "wo":
                            item.Value = Get(wo, def.Field);
                            break;
                        case "sku":
                            skuData ??= ReadJson<Dictionary<string, List<string>>>("twist_sku_alias.json");
                            item.Value = SkuField(skuData, productCode, def.Field);
                            break;
                        case "po_eu":
                            poData ??= ReadJson<Dictionary<string, string>>("twist_infoPO_EU.json");
                            item.Value = Get(poData, productCode);
                            break;
                        case "system":
                            item.Value = def.Field == "user"
                                ? userName ?? ""
                                : DateTime.Now.ToString(string.IsNullOrEmpty(def.Format) ? "dd/MM/yyyy" : def.Format, CultureInfo.InvariantCulture);
                            break;
                    }
                }
                result.Add(item);
            }
            return result;
        }

        // Thay giá trị vào ZPL. Trả về lỗi (null = hợp lệ): biến bắt buộc còn trống hoặc giá trị chứa ^ / ~
        public string? Apply(string zpl, Dictionary<string, string>? values, out string filledZpl)
        {
            filledZpl = zpl;
            var lookup = new Dictionary<string, string>(values ?? new(), StringComparer.OrdinalIgnoreCase);

            var missing = new List<string>();
            foreach (string key in ExtractKeys(zpl))
            {
                lookup.TryGetValue(key, out string? value);
                value = (value ?? "").Trim();
                var def = config.Variables.FirstOrDefault(v => string.Equals(v.Key, key, StringComparison.OrdinalIgnoreCase));

                if (value.Length == 0 && !(def?.Optional ?? false))
                {
                    missing.Add(def?.Label is { Length: > 0 } label ? label : key);
                    continue;
                }
                if (value.IndexOfAny(ZplControlChars) >= 0)
                    return $"Giá trị của {{{key}}} không được chứa ký tự ^ hoặc ~.";
                lookup[key] = value;
            }
            if (missing.Count > 0)
                return "Còn biến chưa nhập: " + string.Join(", ", missing) + ".";

            filledZpl = VariableRegex.Replace(zpl, m => lookup.TryGetValue(m.Groups[1].Value, out string? v) ? v : m.Value);
            return null;
        }

        static string Get(Dictionary<string, string>? data, string? key) =>
            data != null && key != null && data.TryGetValue(key, out string? v) ? v ?? "" : "";

        // twist_sku_alias.json: mã SP -> [barcode, tên SKU, PCS/hộp]
        static string SkuField(Dictionary<string, List<string>>? data, string? productCode, string? field)
        {
            if (data == null || productCode == null || !data.TryGetValue(productCode, out var arr)) return "";
            int index = field switch { "barcode" => 0, "sku" => 1, "pcsPerBox" => 2, _ => -1 };
            return index >= 0 && index < arr.Count ? arr[index] : "";
        }

        T? ReadJson<T>(string fileName) where T : class
        {
            try
            {
                string path = Path.Combine(webRootPath, "data", fileName);
                return File.Exists(path) ? JsonConvert.DeserializeObject<T>(File.ReadAllText(path)) : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
