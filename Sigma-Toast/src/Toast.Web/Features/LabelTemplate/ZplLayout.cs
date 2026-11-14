using System.Globalization;
using System.Text.RegularExpressions;

namespace Toast.Web.Features.LabelTemplate;

// Đọc cấu trúc 1 tem ZPL (^XA..^XZ): lệnh, field (đến ^FS), đường dọc của khung (^GB), khổ ^PW.
// Dùng chung cho ZplBarcodeFit (căn barcode lúc in) và ZplNormalizer (công cụ chuẩn hoá ở màn Mẫu tem).
public sealed class ZplLayout
{
    public static readonly Regex LabelRegex = new(@"\^XA.*?\^XZ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
    // Lệnh ZPL: ^ hoặc ~ + 2 ký tự; tham số kéo đến lệnh kế tiếp
    static readonly Regex CommandRegex = new(@"[\^~]([A-Za-z@][A-Za-z0-9@])([^\^~]*)", RegexOptions.Singleline);
    static readonly Regex FitRegex = new(@"^\s*FIT\b", RegexOptions.IgnoreCase);

    // Start: vị trí trong chuỗi đã đọc (cộng offset của tem nếu đọc cả file)
    public sealed record Cmd(string Code, string Params, int Start, int Length);

    public sealed record VLine(int Left, int Right, int Top, int Bottom);

    public List<List<Cmd>> Fields { get; } = [];
    public List<VLine> Lines { get; } = [];
    public int? PrintWidth { get; private set; }

    public static ZplLayout Parse(string label, int offset = 0)
    {
        var layout = new ZplLayout();
        var current = new List<Cmd>();
        foreach (Match m in CommandRegex.Matches(label))
        {
            var c = new Cmd(m.Groups[1].Value.ToUpperInvariant(), m.Groups[2].Value, m.Index + offset, m.Length);
            current.Add(c);
            if (c.Code == "PW" && TryInt(Arg(c.Params, 0), out int pw) && pw > 0) layout.PrintWidth = pw;
            if (c.Code == "FS") { layout.Fields.Add(current); current = []; }
        }

        // Đường dọc từ ^GB (field có ^FO hoặc ^FT + ^GB); hộp ^GB cho 2 cạnh trái / phải
        foreach (var f in layout.Fields)
        {
            var gb = f.FirstOrDefault(c => c.Code == "GB");
            var pos = Position(f);
            if (gb == null || pos == null) continue;
            if (!TryInt(Arg(pos.Params, 0), out int x) || !TryInt(Arg(pos.Params, 1), out int y)) continue;
            int t = TryInt(Arg(gb.Params, 2), out int tt) && tt > 0 ? tt : 1;
            int w = TryInt(Arg(gb.Params, 0), out int ww) ? Math.Max(ww, t) : t;
            int h = TryInt(Arg(gb.Params, 1), out int hh) ? Math.Max(hh, t) : t;
            if (pos.Code == "FT") y -= h; // ^FT của hình = góc dưới trái
            if (h <= t) continue; // đường ngang
            if (w <= t) layout.Lines.Add(new VLine(x, x + w, y, y + h));
            else
            {
                layout.Lines.Add(new VLine(x, x + t, y, y + h));
                layout.Lines.Add(new VLine(x + w - t, x + w, y, y + h));
            }
        }
        return layout;
    }

    // Ô chứa phần tử: đường dọc gần nhất bên trái / phải của mép trái x, chồng lên khoảng cao top..bottom.
    // Không có đường bên trái = 0, bên phải = ^PW. Không xác định được = null.
    public (int Left, int Right)? FindCell(int x, int top, int bottom)
    {
        var overlapping = Lines.Where(l => l.Top < bottom && l.Bottom > top).ToList();
        int left = overlapping.Where(l => l.Right <= x).Select(l => l.Right).DefaultIfEmpty(0).Max();
        int? right = overlapping.Where(l => l.Left > x).Select(l => (int?)l.Left).Min() ?? PrintWidth;
        return right == null || right <= left ? null : (left, right.Value);
    }

    public static Cmd? Position(List<Cmd> field) => field.LastOrDefault(c => c.Code is "FO" or "FT");

    public static bool IsFitMarker(Cmd c) => c.Code == "FX" && FitRegex.IsMatch(c.Params);

    public static string Arg(string p, int i)
    {
        var parts = p.Split(',');
        return i < parts.Length ? parts[i].Trim() : "";
    }

    // Thay tham số thứ i, giữ phần xuống dòng / khoảng trắng phía sau tham số cuối
    public static string ReplaceArg(string p, int i, string value)
    {
        var parts = p.Split(',').ToList();
        while (parts.Count <= i) parts.Add("");
        string trailing = i == parts.Count - 1 ? parts[i][parts[i].TrimEnd().Length..] : "";
        parts[i] = value + trailing;
        return string.Join(",", parts);
    }

    public static bool TryInt(string s, out int v) => int.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
}
