using System.Globalization;
using System.Text.RegularExpressions;
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

        // Lệnh cấu hình máy in, không ảnh hưởng hình tem nên không cần cảnh báo
        static readonly HashSet<string> IgnoredCommands = new(StringComparer.OrdinalIgnoreCase)
        {
            "^PW", "^LL", "^PQ", "^PR", "^MD", "^MN", "^MT", "^MM", "^JM", "^LS", "^LT", "~SD", "~TA", "^XB"
        };

        static readonly Regex SizeRegex = new(@"^\s*(\d+(?:\.\d+)?)\s*x\s*(\d+(?:\.\d+)?)\s*$", RegexOptions.IgnoreCase);

        public class Result
        {
            public List<string> Images { get; set; } = new();          // PNG base64
            public List<string> UnsupportedCommands { get; set; } = new();
            public int TotalLabels { get; set; }
            public double WidthMm { get; set; }
            public double HeightMm { get; set; }
            public int Dpmm { get; set; }
        }

        // size dạng "4x6" (inch). Không đọc được thì dùng 4x6. dpmm ngoài danh sách cho phép thì dùng 600 dpi.
        public static Result Render(string zpl, string? size, int? dpmm)
        {
            var (widthMm, heightMm) = ParseSizeMm(size);
            int density = dpmm.HasValue && AllowedDpmm.Contains(dpmm.Value) ? dpmm.Value : DefaultDpmm;

            IPrinterStorage storage = new PrinterStorage();
            var analyzer = new ZplAnalyzer(storage);
            var drawer = new ZplElementDrawer(storage);
            var info = analyzer.Analyze(zpl);

            var result = new Result
            {
                TotalLabels = info.LabelInfos.Length,
                WidthMm = widthMm,
                HeightMm = heightMm,
                Dpmm = density,
                UnsupportedCommands = (info.UnknownCommands ?? Array.Empty<string>())
                    .Select(c => c.Length > 3 ? c.Substring(0, 3) : c)
                    .Where(c => !IgnoredCommands.Contains(c))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList()
            };

            foreach (var label in info.LabelInfos.Take(MaxLabels))
            {
                byte[] png = drawer.Draw(label.ZplElements, widthMm, heightMm, density);
                result.Images.Add(Convert.ToBase64String(png));
            }
            return result;
        }

        static (double widthMm, double heightMm) ParseSizeMm(string? size)
        {
            var m = SizeRegex.Match(size ?? "");
            if (m.Success
                && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double w)
                && double.TryParse(m.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double h)
                && w > 0 && h > 0 && w <= 20 && h <= 40)
            {
                return (w * MmPerInch, h * MmPerInch);
            }
            return (DefaultWidthMm, DefaultHeightMm);
        }
    }
}
