using System.Text.RegularExpressions;

namespace Toast.Web.Features.LabelTemplate;

// Biến {KEY} trong ZPL: tìm, chuẩn hoá danh sách giá trị, thay vào ZPL. Chuyển từ Twist LabelVariableService.
public static class ZplVariables
{
    // {KEY}: chữ cái hoặc _ ở đầu, sau đó chữ/số/_. Không phân biệt hoa thường.
    static readonly Regex VariableRegex = new(@"\{([A-Za-z_][A-Za-z0-9_]*)\}");
    // Ký tự mở lệnh ZPL: có trong giá trị sẽ phá vỡ mẫu tem
    static readonly char[] ZplControlChars = ['^', '~'];

    // Danh sách tên biến xuất hiện trong ZPL, theo thứ tự, không trùng (không phân biệt hoa thường)
    public static List<string> ExtractKeys(string? zpl)
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

    // Chỉ giữ biến có trong ZPL, theo thứ tự xuất hiện, giá trị đã trim. Trả về lỗi nếu giá trị chứa ^ hoặc ~.
    public static string? Normalize(string zpl, IEnumerable<LabelVariable>? input, out List<LabelVariable> result)
    {
        var lookup = (input ?? [])
            .Where(v => !string.IsNullOrEmpty(v.Key))
            .GroupBy(v => v.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (g.Last().Value ?? "").Trim(), StringComparer.OrdinalIgnoreCase);

        result = [];
        foreach (string key in ExtractKeys(zpl))
        {
            string value = lookup.GetValueOrDefault(key) ?? "";
            if (value.IndexOfAny(ZplControlChars) >= 0)
                return $"Giá trị của {{{key}}} không được chứa ký tự ^ hoặc ~.";
            result.Add(new LabelVariable { Key = key, Value = value });
        }
        return null;
    }

    // Thay các biến có giá trị vào ZPL; biến trống giữ nguyên chữ {KEY} (xem trước),
    // blankEmpty = true thì biến có trong danh sách mà trống được thay bằng chuỗi rỗng (in biến Optional)
    public static string Apply(string zpl, IEnumerable<LabelVariable>? values, bool blankEmpty = false)
    {
        var lookup = (values ?? [])
            .Where(v => !string.IsNullOrEmpty(v.Key) && (blankEmpty || !string.IsNullOrEmpty(v.Value)))
            .GroupBy(v => v.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (g.Last().Value ?? "").Trim(), StringComparer.OrdinalIgnoreCase);
        string filled = VariableRegex.Replace(zpl, m => lookup.TryGetValue(m.Groups[1].Value, out string? v) ? v : m.Value);
        // Barcode đánh dấu ^FXFIT: mã hoá ngắn nhất + căn giữa ô theo giá trị thật (không đánh dấu thì giữ nguyên)
        return ZplBarcodeFit.Apply(filled);
    }
}
