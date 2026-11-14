using System.Text.Json.Nodes;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Toast.Web.Features.Viindoo;

/// <summary>
/// Bảng dự án cho màn tra cứu (mục ViindooProjects trong appsettings.json). Name = tên dự án trong nhóm sản phẩm Viindoo
/// ("Toast - F/G" -> Toast). Customer = khách thật (Viindoo không có, ghi tay). SubTypes = từ khoá trong tên sản phẩm để chia nhỏ.
/// </summary>
public class ViindooProjectsOptions
{
    public const string Section = "ViindooProjects";
    public List<ViindooProjectInfo> Projects { get; set; } = [];
}

public class ViindooProjectInfo
{
    public string Name { get; set; } = "";
    public string Customer { get; set; } = "";
    // Căn cứ để biết khách (VD file tài liệu), chỉ để ghi chú
    public string? Note { get; set; }
    public List<string> SubTypes { get; set; } = [];
}

// Phân loại 1 sản phẩm: dự án + loại (F/G / WIP / Raw MAT...) từ nhóm sản phẩm, khách từ bảng dự án, loại con từ tên sản phẩm
public sealed record ProductClass(string Project, string Kind, string Customer, string SubType);

public sealed record ViindooCategory(int Id, string Name, string Project, string Kind);

/// <summary>
/// Phân loại theo nhóm sản phẩm (product.category) của Viindoo: tên dạng "&lt;Dự án&gt; - &lt;Loại&gt;"
/// (VD "Toast - F/G", "Sandford- WIP", "Nguyên vật liệu TOAST", "Twist FG"). Danh sách nhóm nhớ RAM 30 phút.
/// </summary>
public sealed class ViindooCatalog(ViindooRpc viindoo, IMemoryCache cache, IOptionsMonitor<ViindooProjectsOptions> options)
{
    public const string KindFg = "F/G", KindWip = "WIP", KindRaw = "Raw MAT";

    public async Task<List<ViindooCategory>> CategoriesAsync()
    {
        return (await cache.GetOrCreateAsync("viindoo-categories", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            var rows = await viindoo.SearchReadAsync("product.category", [], ["name", "complete_name"], limit: 1000);
            return rows.Select(r =>
            {
                string name = ViindooRpc.Str(r!["name"]);
                var (project, kind) = Split(name);
                return new ViindooCategory(r["id"]!.GetValue<int>(), name, project, kind);
            }).ToList();
        }))!;
    }

    // Id các nhóm thuộc dự án / loại (trống = không lọc). null = không lọc gì.
    public async Task<List<int>?> CategoryIdsAsync(string? project, string? kind)
    {
        if (string.IsNullOrWhiteSpace(project) && string.IsNullOrWhiteSpace(kind)) return null;
        return (await CategoriesAsync())
            .Where(c => (string.IsNullOrWhiteSpace(project) || string.Equals(c.Project, project, StringComparison.OrdinalIgnoreCase))
                && (string.IsNullOrWhiteSpace(kind) || string.Equals(c.Kind, kind, StringComparison.OrdinalIgnoreCase)))
            .Select(c => c.Id).ToList();
    }

    // Dự án có trên Viindoo (từ nhóm) kèm khách trong bảng cấu hình, cho ô lọc
    public async Task<List<object>> ProjectsAsync()
    {
        var names = (await CategoriesAsync()).Select(c => c.Project).Where(p => p.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p).ToList();
        return names.Select(n => (object)new { name = n, customer = Info(n)?.Customer ?? "" }).ToList();
    }

    public ProductClass Classify(JsonNode? categ, string productName)
    {
        string categName = ViindooRpc.Str((categ as JsonArray)?[1]);
        // complete_name dạng "All / Saleable": lấy phần cuối
        if (categName.Contains(" / ")) categName = categName[(categName.LastIndexOf(" / ") + 3)..];
        var (project, kind) = Split(categName);
        var info = Info(project);
        string sub = info?.SubTypes.FirstOrDefault(k => productName.Contains(k, StringComparison.OrdinalIgnoreCase)) ?? "";
        return new ProductClass(info?.Name ?? project, kind, info?.Customer ?? "", sub);
    }

    ViindooProjectInfo? Info(string project) =>
        options.CurrentValue.Projects.FirstOrDefault(p => string.Equals(p.Name, project, StringComparison.OrdinalIgnoreCase));

    // Tách tên nhóm thành (dự án, loại). Không nhận ra thì dự án = cả tên, loại trống.
    public static (string project, string kind) Split(string name)
    {
        name = name.Trim();
        static string Kind(string k)
        {
            k = k.Trim();
            if (k.Equals("FG", StringComparison.OrdinalIgnoreCase) || k.Equals("F/G", StringComparison.OrdinalIgnoreCase)) return KindFg;
            if (k.Equals("WIP", StringComparison.OrdinalIgnoreCase)) return KindWip;
            if (k.Equals("Raw MAT", StringComparison.OrdinalIgnoreCase)) return KindRaw;
            return k;
        }
        foreach (var (prefix, kind) in new[] { ("Nguyên vật liệu ", KindRaw), ("Bán thành phẩm ", KindWip), ("Thành phẩm ", KindFg) })
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                string rest = name[prefix.Length..].Trim();
                // "Thành phẩm Injection Toast" -> Injection Toast
                return (rest, kind);
            }
        int dash = name.IndexOf('-');
        if (dash > 0) return (name[..dash].Trim(), Kind(name[(dash + 1)..]));
        // "Twist FG"
        int space = name.LastIndexOf(' ');
        if (space > 0 && Kind(name[(space + 1)..]) is KindFg or KindWip) return (name[..space].Trim(), Kind(name[(space + 1)..]));
        return (name, "");
    }
}
