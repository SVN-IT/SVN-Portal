using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Toast.Web.Features.LabelTemplate;

// Căn barcode Code 128 theo giá trị thật (chạy sau khi điền biến, dùng chung cho xem trước và in).
// Chỉ áp dụng cho field có comment ^FXFIT (máy in bỏ qua ^FX); field khác giữ nguyên ZPL gốc.
// Với field đánh dấu:
//   1. Mã hoá ngắn nhất: tự chèn >: / >; / >5 / >6 (chuỗi số nén 2 chữ số / ký tự như ZebraDesigner làm), không cần mode A.
//   2. Căn giữa trong ô khung: tường trái / phải = các đường dọc ^GB (hoặc cạnh hộp ^GB) bao quanh barcode, không có thì 0 / ^PW.
//   3. Không vừa ô (kể cả lề trắng 10 module mỗi bên theo chuẩn Code 128) thì giảm ^BY (độ rộng vạch nhỏ nhất) đến khi vừa.
// Bỏ qua (giữ nguyên) khi: không phải ^BC hướng N, mode khác N, dữ liệu còn biến {KEY}, có ký tự thoát ^FH,
// ký tự ngoài ASCII in được, đã có mã chuyển kiểu riêng (> ở giữa dữ liệu), hoặc không xác định được ô.
public static class ZplBarcodeFit
{
    const int QuietModules = 10;

    public static bool HasMarker(string? zpl) => zpl != null && Regex.IsMatch(zpl, @"\^FX\s*FIT\b", RegexOptions.IgnoreCase);

    public static string Apply(string zpl)
    {
        if (!HasMarker(zpl)) return zpl;
        return ZplLayout.LabelRegex.Replace(zpl, m => ApplyLabel(m.Value));
    }

    static string ApplyLabel(string label)
    {
        var layout = ZplLayout.Parse(label);

        // ^BY là giá trị mặc định kéo dài cho các field sau
        int byW = 2; string byRatio = "3"; int byH = 10;
        var edits = new List<(int Start, int Length, string Text)>();
        bool pending = false; // field có ^FXFIT mà không có dữ liệu (VD ^CI27^FXFIT^FS): đánh dấu cho field barcode ngay sau
        foreach (var f in layout.Fields)
        {
            bool marked = pending || f.Any(ZplLayout.IsFitMarker);
            pending = marked && !f.Any(c => c.Code is "FD" or "FV");
            var by = f.LastOrDefault(c => c.Code == "BY");
            if (by != null)
            {
                if (TryInt(Arg(by.Params, 0), out int w0) && w0 > 0) byW = w0;
                if (Arg(by.Params, 1) is { Length: > 0 } r) byRatio = r;
                if (TryInt(Arg(by.Params, 2), out int h0) && h0 > 0) byH = h0;
            }
            if (!marked || pending) continue;
            var fit = FitField(f, byW, byRatio, byH, layout);
            if (fit != null) edits.AddRange(fit);
        }

        if (edits.Count == 0) return label;
        var sb = new StringBuilder(label);
        foreach (var e in edits.OrderByDescending(e => e.Start))
            sb.Remove(e.Start, e.Length).Insert(e.Start, e.Text);
        return sb.ToString();
    }

    static List<(int, int, string)>? FitField(List<ZplLayout.Cmd> f, int byW, string byRatio, int byH, ZplLayout layout)
    {
        var bc = f.FirstOrDefault(c => c.Code == "BC");
        var pos = ZplLayout.Position(f);
        var fd = f.FirstOrDefault(c => c.Code == "FD");
        if (bc == null || pos == null || fd == null) return null;

        string orientation = Arg(bc.Params, 0).ToUpperInvariant();
        string mode = Arg(bc.Params, 5).ToUpperInvariant();
        if (orientation is not ("" or "N") || mode is not ("" or "N")) return null;
        if (!TryInt(Arg(pos.Params, 0), out int x) || !TryInt(Arg(pos.Params, 1), out int y)) return null;

        string raw = fd.Params;
        if (raw.Contains('{')) return null; // còn biến chưa điền (in khi mất mạng: trình duyệt điền sau)
        var fh = f.FirstOrDefault(c => c.Code == "FH");
        if (fh != null)
        {
            char esc = fh.Params.Length > 0 ? fh.Params[0] : '_';
            if (raw.Contains(esc)) return null;
        }
        string? data = StripStartCode(raw);
        if (data == null || data.Length == 0) return null;
        var encoded = Encode(data);
        if (encoded == null) return null;

        int height = TryInt(Arg(bc.Params, 1), out int bh) && bh > 0 ? bh : byH;
        int top = pos.Code == "FT" ? y - height : y;
        if (layout.FindCell(x, top, top + height) is not { } cellRange) return null;
        var (left, right) = cellRange;
        int cell = right - left;

        int modules = 11 * (encoded.Value.Symbols + 2) + 13;
        int w = byW;
        while (w > 1 && (modules + 2 * QuietModules) * w > cell) w--;
        int width = modules * w;
        int newX = left + Math.Max(0, (cell - width) / 2);

        var edits = new List<(int, int, string)>
        {
            (fd.Start, fd.Length, "^FD" + encoded.Value.Data),
            (pos.Start, pos.Length, "^" + pos.Code + ReplaceArg(pos.Params, 0, newX.ToString(CultureInfo.InvariantCulture)))
        };
        var byInField = f.LastOrDefault(c => c.Code == "BY" && c.Start < bc.Start);
        if (w != byW)
        {
            if (byInField != null)
                edits.Add((byInField.Start, byInField.Length, "^BY" + ReplaceArg(byInField.Params, 0, w.ToString(CultureInfo.InvariantCulture))));
            else
                edits.Add((bc.Start, 0, $"^BY{w},{byRatio},{byH}"));
        }
        return edits;
    }

    // Bỏ mã bắt đầu >: (B) / >; (C) / >9 (A) ở đầu. Còn dấu > khác (chuyển kiểu riêng, ký tự đặc biệt) thì không xử lý.
    static string? StripStartCode(string raw)
    {
        string d = raw;
        if (d.Length >= 2 && d[0] == '>' && d[1] is ':' or ';' or '9') d = d[2..];
        return d.Contains('>') ? null : d;
    }

    // Mã hoá Code 128 ít ký tự nhất bằng kiểu B (ASCII 32..126) và C (2 chữ số). Trả về chuỗi ^FD kiểu Zebra + số ký tự dữ liệu
    // (không tính start, check, stop). Quy hoạch động theo vị trí + kiểu đang dùng; đổi kiểu tốn 1 ký tự.
    public static (string Data, int Symbols)? Encode(string data)
    {
        if (data.Any(ch => ch < 32 || ch > 126)) return null;
        int n = data.Length;
        const int B = 0, C = 1, Inf = int.MaxValue / 2;
        // cost[i, s] = số ký tự tối thiểu để mã hoá data[i..] khi đang ở kiểu s
        var cost = new int[n + 1, 2];
        var next = new (int pos, int set, bool sw)[n + 1, 2];
        cost[n, B] = cost[n, C] = 0;
        for (int i = n - 1; i >= 0; i--)
        {
            bool pair = i + 1 < n && char.IsAsciiDigit(data[i]) && char.IsAsciiDigit(data[i + 1]);
            // đang ở B: mã 1 ký tự ở B, hoặc đổi sang C (nếu có cặp số)
            int stayB = 1 + cost[i + 1, B];
            int toC = pair ? 2 + cost[i + 2, C] : Inf;
            (cost[i, B], next[i, B]) = stayB <= toC ? (stayB, (i + 1, B, false)) : (toC, (i + 2, C, true));
            // đang ở C: mã cặp số ở C, hoặc đổi sang B
            int stayC = pair ? 1 + cost[i + 2, C] : Inf;
            int toB = 2 + cost[i + 1, B];
            (cost[i, C], next[i, C]) = stayC <= toB ? (stayC, (i + 2, C, false)) : (toB, (i + 1, B, true));
        }
        bool canStartC = n >= 2 && char.IsAsciiDigit(data[0]) && char.IsAsciiDigit(data[1]);
        int startB = 1 + cost[1, B];
        int startC = canStartC ? 1 + cost[2, C] : Inf;

        var sb = new StringBuilder();
        int pos, set;
        if (startC < startB) { sb.Append(">;").Append(data, 0, 2); pos = 2; set = C; }
        else { sb.Append(">:"); AppendB(sb, data[0]); pos = 1; set = B; }
        while (pos < n)
        {
            var (np, ns, sw) = next[pos, set];
            if (sw) sb.Append(ns == C ? ">5" : ">6");
            if (ns == C) sb.Append(data, pos, 2); else AppendB(sb, data[pos]);
            pos = np; set = ns;
        }
        return (sb.ToString(), Math.Min(startB, startC));
    }

    static void AppendB(StringBuilder sb, char ch) => sb.Append(ch == '>' ? "><" : ch.ToString());

    static string Arg(string p, int i) => ZplLayout.Arg(p, i);
    static string ReplaceArg(string p, int i, string v) => ZplLayout.ReplaceArg(p, i, v);
    static bool TryInt(string s, out int v) => ZplLayout.TryInt(s, out v);
}
