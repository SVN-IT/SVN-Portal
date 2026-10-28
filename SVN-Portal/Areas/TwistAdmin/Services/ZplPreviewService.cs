using System.Globalization;
using System.Text.RegularExpressions;
using BinaryKits.Zpl.Label.Elements;
using BinaryKits.Zpl.Viewer;

namespace SVN_Portal.Areas.TwistAdmin.Services
{
    // Vẽ ZPL thành ảnh PNG để xem trước (offline, thư viện BinaryKits.Zpl.Viewer).
    // Chỉ là bản xem gần đúng: font và một số lệnh hiếm có thể khác máy in thật.
    public static class ZplPreviewService
    {
        // Mật độ điểm (dots/mm) của máy Zebra: 6 = 152 dpi, 8 = 203 dpi, 12 = 300 dpi, 24 = 600 dpi
        public static readonly int[] AllowedDpmm = { 6, 8, 12, 24 };
        public const int DefaultDpmm = 24; // 600 dpi
        const int MaxLabels = 20;         // 1 file ZPL có thể chứa nhiều tem (^XA..^XZ)
        const double DefaultWidthMm = 101.6, DefaultHeightMm = 152.4; // 4x6 inch
        const double MmPerInch = 25.4;
        const double MaxSideMm = 1016;    // 40 inch, chặn giá trị ^PW/^LL bất thường
        const double SizeToleranceInch = 0.1; // lệch dưới 0.1 inch coi như khớp Size

        // Lệnh cấu hình máy in, không ảnh hưởng hình tem nên không cần cảnh báo
        static readonly HashSet<string> IgnoredCommands = new(StringComparer.OrdinalIgnoreCase)
        {
            "^PW", "^LL", "^PQ", "^PR", "^MD", "^MN", "^MT", "^MM", "^JM", "^LS", "^LT", "~SD", "~TA", "^XB",
            "~CC", "~CD", "~CT" // đổi ký tự điều khiển, ZebraDesigner hay chèn ở đầu file
        };

        // Chỉ báo các mục đúng dạng lệnh ZPL (^XX / ~XX), bỏ mảnh vụn như "CT", "^" do parser tách ra
        static readonly Regex CommandRegex = new(@"^[\^~][A-Za-z0-9]{2}$");

        static readonly Regex SizeRegex = new(@"^\s*(\d+(?:\.\d+)?)\s*x\s*(\d+(?:\.\d+)?)\s*$", RegexOptions.IgnoreCase);
        // Khổ tem máy in thật dùng: ^PW = rộng, ^LL = dài (đơn vị điểm)
        static readonly Regex PrintWidthRegex = new(@"\^PW(\d+)", RegexOptions.IgnoreCase);
        static readonly Regex LabelLengthRegex = new(@"\^LL(\d+)", RegexOptions.IgnoreCase);

        public class Result
        {
            public List<string> Images { get; set; } = new();          // PNG base64
            public List<string> UnsupportedCommands { get; set; } = new();
            public int TotalLabels { get; set; }
            public double WidthMm { get; set; }
            public double HeightMm { get; set; }
            public int Dpmm { get; set; }
            // Mô tả khổ tem lấy từ đâu (ZPL hay cột Size)
            public string SizeSource { get; set; } = "";
            // Cảnh báo khi khổ trong ZPL khác Size đang chọn
            public string? SizeWarning { get; set; }
            // Phần tử không vẽ được (VD: mã vạch chứa ký tự không hợp lệ, biến {KEY} chưa điền) nên bị bỏ qua
            public List<string> SkippedElements { get; set; } = new();
        }

        // size dạng "4x6" (inch). dpmm ngoài danh sách cho phép thì dùng 600 dpi.
        // Khổ tem: ưu tiên ^PW/^LL trong ZPL (giống máy in), thiếu thì lấy theo size, size không đọc được thì 4x6.
        public static Result Render(string zpl, string? size, int? dpmm)
        {
            int density = dpmm.HasValue && AllowedDpmm.Contains(dpmm.Value) ? dpmm.Value : DefaultDpmm;
            var sizeMm = ParseSizeMm(size);
            int? pwDots = LastDots(PrintWidthRegex, zpl);
            int? llDots = LastDots(LabelLengthRegex, zpl);

            double widthMm = DotsToMm(pwDots, density) ?? sizeMm?.widthMm ?? DefaultWidthMm;
            double heightMm = DotsToMm(llDots, density) ?? sizeMm?.heightMm ?? DefaultHeightMm;

            IPrinterStorage storage = new PrinterStorage();
            var analyzer = new ZplAnalyzer(storage);
            var drawer = new ZplElementDrawer(storage);
            var info = analyzer.Analyze(zpl);

            // File xuất từ ZebraDesigner/BarTender thường có 1 khối ^XA..^XZ chỉ chứa lệnh cấu hình máy in
            // (~JS, ^PO, ^JU, ^CI...). Máy in không in ra gì cho khối đó nên bỏ qua, chỉ giữ tem có phần tử để vẽ.
            var labels = info.LabelInfos
                .Where(l => l.ZplElements.Any(e => e is ZplPositionedElementBase))
                .ToList();

            var result = new Result
            {
                TotalLabels = labels.Count,
                WidthMm = widthMm,
                HeightMm = heightMm,
                Dpmm = density,
                SizeSource = DescribeSource(pwDots, llDots, sizeMm != null ? size!.Trim() : null),
                SizeWarning = BuildSizeWarning(pwDots, llDots, density, size, sizeMm),
                UnsupportedCommands = (info.UnknownCommands ?? Array.Empty<string>())
                    .Select(c => c.Length > 3 ? c.Substring(0, 3) : c)
                    .Where(c => CommandRegex.IsMatch(c) && !IgnoredCommands.Contains(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            foreach (var label in labels.Take(MaxLabels))
            {
                byte[] png;
                try
                {
                    png = drawer.Draw(label.ZplElements, widthMm, heightMm, density);
                }
                catch
                {
                    // 1 phần tử lỗi làm hỏng cả tem: vẽ thử từng phần tử, bỏ phần tử lỗi rồi vẽ lại phần còn lại
                    var drawable = SkipBrokenElements(drawer, label.ZplElements, result.SkippedElements);
                    if (drawable.Count == 0) throw;
                    png = drawer.Draw(drawable, widthMm, heightMm, density);
                }
                result.Images.Add(Convert.ToBase64String(png));
            }
            return result;
        }

        // Vẽ thử từng phần tử trên khổ nhỏ, mật độ thấp (chỉ để kiểm tra lỗi, nhanh), trả về các phần tử vẽ được
        static List<ZplElementBase> SkipBrokenElements(ZplElementDrawer drawer, IEnumerable<ZplElementBase> elements, List<string> skipped)
        {
            const double ProbeSizeMm = 25.4;
            const int ProbeDpmm = 6;
            var ok = new List<ZplElementBase>();
            foreach (var element in elements)
            {
                try
                {
                    drawer.Draw(new[] { element }, ProbeSizeMm, ProbeSizeMm, ProbeDpmm);
                    ok.Add(element);
                }
                catch (Exception ex)
                {
                    if (!skipped.Contains(ex.Message)) skipped.Add(ex.Message);
                }
            }
            return ok;
        }

        // Lấy giá trị cuối cùng (khối tem thật thường nằm sau khối cấu hình)
        static int? LastDots(Regex regex, string zpl)
        {
            var matches = regex.Matches(zpl);
            for (int i = matches.Count - 1; i >= 0; i--)
            {
                if (int.TryParse(matches[i].Groups[1].Value, out int dots) && dots > 0)
                    return dots;
            }
            return null;
        }

        static double? DotsToMm(int? dots, int dpmm)
        {
            if (!dots.HasValue) return null;
            double mm = (double)dots.Value / dpmm;
            return mm <= MaxSideMm ? mm : null;
        }

        static string DescribeSource(int? pwDots, int? llDots, string? size)
        {
            if (pwDots.HasValue && llDots.HasValue) return $"theo ZPL (^PW{pwDots} ^LL{llDots})";
            if (pwDots.HasValue) return $"rộng theo ZPL (^PW{pwDots}), dài theo Size {size ?? "4x6"}";
            if (llDots.HasValue) return $"dài theo ZPL (^LL{llDots}), rộng theo Size {size ?? "4x6"}";
            return size != null ? $"theo Size {size}" : "mặc định 4x6 (chưa chọn Size)";
        }

        // Cảnh báo khi ZPL khai báo đủ ^PW + ^LL mà khác Size đang chọn. Gợi ý DPI khác nếu với DPI đó thì khớp Size.
        static string? BuildSizeWarning(int? pwDots, int? llDots, int density, string? size, (double widthMm, double heightMm)? sizeMm)
        {
            if (!pwDots.HasValue || !llDots.HasValue || sizeMm == null) return null;
            if (Matches(pwDots.Value, llDots.Value, density, sizeMm.Value)) return null;

            string msg = $"ZPL khai báo khổ {Inch(pwDots.Value, density)} × {Inch(llDots.Value, density)} inch ở {Dpi(density)}, " +
                         $"khác Size đang chọn {size!.Trim()}.";

            var otherDpmm = AllowedDpmm.Where(d => d != density && Matches(pwDots.Value, llDots.Value, d, sizeMm.Value)).ToList();
            if (otherDpmm.Count > 0)
                msg += $" Nếu mẫu này dành cho máy {string.Join(" / ", otherDpmm.Select(Dpi))} thì khổ khớp với Size.";
            else
                msg += " Kiểm tra lại Size của mẫu tem hoặc DPI của máy in.";
            return msg;
        }

        // So khớp theo inch với DPI danh nghĩa (600/300/203/152) - phần mềm thiết kế tem tính ^PW/^LL theo con số này
        static bool Matches(int pwDots, int llDots, int dpmm, (double widthMm, double heightMm) size) =>
            Math.Abs(DotsToInch(pwDots, dpmm) - size.widthMm / MmPerInch) <= SizeToleranceInch
            && Math.Abs(DotsToInch(llDots, dpmm) - size.heightMm / MmPerInch) <= SizeToleranceInch;

        static double DotsToInch(int dots, int dpmm) => dots / (double)NominalDpi(dpmm);

        static string Inch(int dots, int dpmm) =>
            Math.Round(DotsToInch(dots, dpmm), 2).ToString("0.##", CultureInfo.InvariantCulture);

        // Máy "600 dpi" thực tế là 24 dots/mm (609.6 dpi), nhưng tài liệu và phần mềm thiết kế đều gọi theo số danh nghĩa
        static int NominalDpi(int dpmm) => dpmm switch
        {
            6 => 152,
            8 => 203,
            12 => 300,
            24 => 600,
            _ => (int)Math.Round(dpmm * MmPerInch)
        };

        static string Dpi(int dpmm) => NominalDpi(dpmm) + " dpi";

        static (double widthMm, double heightMm)? ParseSizeMm(string? size)
        {
            var m = SizeRegex.Match(size ?? "");
            if (m.Success
                && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double w)
                && double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double h)
                && w > 0 && h > 0 && w <= 20 && h <= 40)
            {
                return (w * MmPerInch, h * MmPerInch);
            }
            return null;
        }
    }
}
