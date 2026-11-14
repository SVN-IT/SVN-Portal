using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Toast.Web.Features.LabelTemplate;

// Công cụ "Chuẩn hoá ZPL" ở màn Mẫu tem: ZPL xuất từ ZebraDesigner (giá trị mẫu) -> thay giá trị mẫu bằng {BIẾN} + căn giữa ô.
//   Chữ:     căn giữa bằng ^FB<rộng ô>,1,0,C của chính ZPL (máy in tự căn), x = mép trái ô, cuối dữ liệu thêm \& như ZebraDesigner.
//   Barcode: thay dữ liệu (bỏ >: >; >5 >6), căn giữa = thêm ^FXFIT (Toast căn lúc in, xem ZplBarcodeFit; chỉ Code 128).
// Chỉ sửa field người dùng chọn; phần còn lại giữ nguyên từng ký tự. Không đụng DB.
public static class ZplNormalizer
{
    static readonly Regex KeyRegex = new(@"^[A-Za-z_][A-Za-z0-9_]*$");
    static readonly Regex SingleVariable = new(@"^\{([A-Za-z_][A-Za-z0-9_]*)\}$");

    public sealed class Field
    {
        // Vị trí lệnh ^FD trong ZPL (định danh field giữa 2 lần gọi analyze / apply cùng 1 ZPL)
        public int Id { get; set; }
        public string Kind { get; set; } = "text"; // text | barcode
        public string Symbology { get; set; } = ""; // barcode: "Code 128", "^BQ"...
        public string Sample { get; set; } = "";
        // Biến khi toàn bộ dữ liệu là {KEY}
        public string? Variable { get; set; }
        // Dữ liệu có {KEY} lẫn chữ khác (VD "Model: {MODEL}") -> không đổi biến được
        public bool MixedVariable { get; set; }
        public bool Centered { get; set; }
        public bool CanCenter { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public string? Note { get; set; }
    }

    public sealed class Change
    {
        public int Id { get; set; }
        public string? Variable { get; set; }
        public bool Center { get; set; }
    }

    // Field đã đọc kèm lệnh gốc để sửa
    sealed record Parsed(Field Field, List<ZplLayout.Cmd> Cmds, ZplLayout Layout);

    public static List<Field> Analyze(string zpl) => Parse(zpl).Select(p => p.Field).ToList();

    public static (string Zpl, List<string> Warnings) Apply(string zpl, IEnumerable<Change> changes)
    {
        var parsed = Parse(zpl).ToDictionary(p => p.Field.Id);
        var warnings = new List<string>();
        var edits = new List<(int Start, int Length, string Text)>();
        foreach (var change in changes)
        {
            if (!parsed.TryGetValue(change.Id, out var p))
            {
                warnings.Add("Không tìm thấy nội dung cần sửa (ZPL đã đổi?), bấm Đọc lại.");
                continue;
            }
            string? variable = string.IsNullOrWhiteSpace(change.Variable) ? null : change.Variable.Trim().Trim('{', '}').Trim();
            if (variable != null && !KeyRegex.IsMatch(variable))
            {
                warnings.Add($"Tên biến \"{change.Variable}\" không hợp lệ (chữ cái / số / _, không bắt đầu bằng số).");
                continue;
            }
            if (variable != null && (p.Field.MixedVariable || string.Equals(variable, p.Field.Variable, StringComparison.OrdinalIgnoreCase)))
                variable = null;
            bool center = change.Center && !p.Field.Centered;
            if (variable == null && !center) continue;

            if (p.Field.Kind == "barcode") EditBarcode(p, variable, center, edits, warnings);
            else EditText(p, variable, center, edits, warnings);
        }

        if (edits.Count == 0) return (zpl, warnings);
        var sb = new StringBuilder(zpl);
        foreach (var e in edits.OrderByDescending(e => e.Start).ThenByDescending(e => e.Length))
            sb.Remove(e.Start, e.Length).Insert(e.Start, e.Text);
        return (sb.ToString(), warnings);
    }

    // ===== Đọc =====

    static List<Parsed> Parse(string zpl)
    {
        var result = new List<Parsed>();
        foreach (Match m in ZplLayout.LabelRegex.Matches(zpl ?? ""))
        {
            var layout = ZplLayout.Parse(m.Value, m.Index);
            bool pendingFit = false;
            foreach (var f in layout.Fields)
            {
                bool fit = pendingFit || f.Any(ZplLayout.IsFitMarker);
                var fd = f.FirstOrDefault(c => c.Code == "FD");
                pendingFit = fit && fd == null && !f.Any(c => c.Code == "FV");
                var pos = ZplLayout.Position(f);
                if (fd == null || pos == null || fd.Params.Length == 0) continue;
                if (f.Any(c => c.Code is "GB" or "GF" or "GC" or "GD" or "GE")) continue;

                var barcode = f.FirstOrDefault(c => c.Code.StartsWith('B') && c.Code != "BY");
                var field = new Field
                {
                    Id = fd.Start,
                    Kind = barcode != null ? "barcode" : "text",
                    X = ZplLayout.TryInt(ZplLayout.Arg(pos.Params, 0), out int x) ? x : 0,
                    Y = ZplLayout.TryInt(ZplLayout.Arg(pos.Params, 1), out int y) ? y : 0
                };
                string data = fd.Params;
                if (barcode != null)
                {
                    bool code128 = barcode.Code == "BC";
                    field.Symbology = code128 ? "Code 128" : "^" + barcode.Code;
                    field.Centered = fit;
                    field.Sample = code128 ? DecodeCode128(data) ?? data : data;
                    string orientation = ZplLayout.Arg(barcode.Params, 0).ToUpperInvariant();
                    string mode = ZplLayout.Arg(barcode.Params, 5).ToUpperInvariant();
                    field.CanCenter = code128 && orientation is "" or "N" && mode is "" or "N";
                    if (!code128) field.Note = "Chỉ căn giữa được barcode Code 128.";
                    else if (!field.CanCenter) field.Note = "Chỉ căn giữa được barcode không xoay, mode N.";
                }
                else
                {
                    var fb = f.FirstOrDefault(c => c.Code == "FB");
                    field.Centered = fb != null && ZplLayout.Arg(fb.Params, 3).Equals("C", StringComparison.OrdinalIgnoreCase);
                    field.Sample = fb != null ? StripLineBreak(data, Escape(f)) : data;
                    var font = f.FirstOrDefault(c => c.Code.StartsWith('A'));
                    // ^A0N,92,96 -> Code "A0" (font 0), tham số "N,92,96": hướng = tham số đầu
                    string orientation = font == null ? "" : ZplLayout.Arg(font.Params, 0).ToUpperInvariant();
                    field.CanCenter = orientation is "" or "N";
                    if (!field.CanCenter) field.Note = "Chỉ căn giữa được chữ không xoay.";
                }
                var whole = SingleVariable.Match(field.Sample.Trim());
                if (whole.Success) field.Variable = whole.Groups[1].Value;
                else field.MixedVariable = field.Sample.Contains('{') && Regex.IsMatch(field.Sample, @"\{[A-Za-z_][A-Za-z0-9_]*\}");
                result.Add(new Parsed(field, f, layout));
            }
        }
        return result;
    }

    // Dữ liệu Code 128 kiểu Zebra -> giá trị thật: bỏ >: >; >9 >5 >6 >7, >< = dấu >. Có mã khác (FNC...) thì null.
    static string? DecodeCode128(string data)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] != '>') { sb.Append(data[i]); continue; }
            if (i + 1 >= data.Length) return null;
            char c = data[++i];
            if (c == '<') sb.Append('>');
            else if (c is not (':' or ';' or '9' or '5' or '6' or '7')) return null;
        }
        return sb.ToString();
    }

    // Ký tự thoát của ^FH (mặc định _), null khi field không có ^FH
    static char? Escape(List<ZplLayout.Cmd> f)
    {
        var fh = f.FirstOrDefault(c => c.Code == "FH");
        return fh == null ? null : fh.Params.Length > 0 ? fh.Params[0] : '_';
    }

    // Đuôi xuống dòng của ^FB mà ZebraDesigner thêm: \& (không ^FH) hoặc <esc>5C& (có ^FH)
    static string LineBreak(char? esc) => esc == null ? "\\&" : esc + "5C&";

    static string StripLineBreak(string data, char? esc)
    {
        foreach (string suffix in new[] { LineBreak(esc), "\\&" })
            if (data.EndsWith(suffix, StringComparison.Ordinal)) return data[..^suffix.Length];
        return data;
    }

    // ===== Sửa =====

    static void EditBarcode(Parsed p, string? variable, bool center, List<(int, int, string)> edits, List<string> warnings)
    {
        var f = p.Cmds;
        var fd = f.First(c => c.Code == "FD");
        if (center && !p.Field.CanCenter)
        {
            warnings.Add($"Barcode \"{p.Field.Sample}\": {p.Field.Note}");
            center = false;
        }
        string? data = variable != null ? "{" + variable + "}" : null;
        if (center && data == null)
        {
            // căn giữa giá trị cố định: ghi lại dạng chưa mã hoá để ^FXFIT tự mã hoá
            string? plain = DecodeCode128(fd.Params);
            if (plain == null)
            {
                warnings.Add($"Barcode \"{p.Field.Sample}\": dữ liệu có mã đặc biệt, không căn giữa được.");
                center = false;
            }
            else if (plain != fd.Params) data = plain;
        }
        if (data != null) edits.Add((fd.Start, fd.Length, "^FD" + data));
        if (center)
        {
            var bc = f.First(c => c.Code.StartsWith('B') && c.Code != "BY");
            var anchor = f.FirstOrDefault(c => c.Code == "BY" && c.Start < bc.Start)
                ?? f.Where(c => c.Code is "FO" or "FT" && c.Start < bc.Start).FirstOrDefault()
                ?? bc;
            edits.Add((anchor.Start, 0, "^FXFIT"));
        }
    }

    static void EditText(Parsed p, string? variable, bool center, List<(int, int, string)> edits, List<string> warnings)
    {
        var f = p.Cmds;
        var fd = f.First(c => c.Code == "FD");
        var fb = f.FirstOrDefault(c => c.Code == "FB");
        var pos = ZplLayout.Position(f)!;
        char? esc = Escape(f);
        string label = Short(p.Field.Sample);

        if (center && !p.Field.CanCenter)
        {
            warnings.Add($"Chữ \"{label}\": {p.Field.Note}");
            center = false;
        }

        bool addBlock = false;
        if (center && fb == null)
        {
            var font = f.FirstOrDefault(c => c.Code.StartsWith('A'));
            int height = 30;
            if (font != null)
            {
                // ^A0N,92,96 -> Code "A0", tham số "N,92,96"; ^AAN,... tương tự
                string h = ZplLayout.Arg(font.Params, 1);
                if (ZplLayout.TryInt(h, out int hh) && hh > 0) height = hh;
            }
            int top = pos.Code == "FT" ? p.Field.Y - height : p.Field.Y;
            if (p.Layout.FindCell(p.Field.X, top, top + height) is { } cell)
            {
                addBlock = true;
                var anchor = f.FirstOrDefault(c => c.Code == "FH" && c.Start < fd.Start) ?? fd;
                edits.Add((anchor.Start, 0, $"^FB{cell.Right - cell.Left},1,0,C"));
                edits.Add((pos.Start, pos.Length, "^" + pos.Code + ZplLayout.ReplaceArg(pos.Params, 0, cell.Left.ToString(CultureInfo.InvariantCulture))));
            }
            else
            {
                warnings.Add($"Chữ \"{label}\": không tìm thấy ô khung (đường kẻ dọc 2 bên), không căn giữa được.");
                center = false;
            }
        }
        else if (center && fb != null)
        {
            edits.Add((fb.Start, fb.Length, "^FB" + ZplLayout.ReplaceArg(fb.Params, 3, "C")));
        }

        bool block = fb != null || addBlock;
        if (variable == null && !addBlock) return;
        string content = variable != null ? "{" + variable + "}" : p.Field.Sample;
        edits.Add((fd.Start, fd.Length, "^FD" + content + (block ? LineBreak(esc) : "")));
    }

    static string Short(string s) => s.Length > 30 ? s[..30] + "…" : s;
}
