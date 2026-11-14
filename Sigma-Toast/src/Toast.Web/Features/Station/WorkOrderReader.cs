using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Toast.Web.Data;
using Toast.Web.Features.Viindoo;

namespace Toast.Web.Features.Station;

/// <summary>
/// Đọc thông tin WO (ViindooRpc.GetWorkOrderAsync: name, product_id, product_code, product_name, product_qty, origin) cho màn công nhân.
/// WO thử (Toast:TestWo) không gọi Viindoo. Kết quả được nhớ trong RAM để lúc đóng box điền biến tem theo WO
/// mà không phải gọi Viindoo lại mỗi box (chỉ đọc).
/// </summary>
public sealed class WorkOrderReader(
    ViindooRpc viindoo,
    ToastDbContext db,
    IMemoryCache cache,
    IOptionsMonitor<StationOptions> options)
{
    static readonly TimeSpan CacheTime = TimeSpan.FromHours(12);

    static string CacheKey(string wo) => "toast-wo:" + wo.ToUpperInvariant();

    // Key chứa các cảnh báo khi lấy dữ liệu tem (PO / Lot / mã khách), mỗi dòng 1 cảnh báo
    public const string WarningsKey = "viindoo_warnings";

    // Đọc mới (lúc mở WO) và cập nhật bộ nhớ. WO thật: gộp thêm dữ liệu tem (ViindooRpc.GetLabelDataAsync:
    // customer_part_number, qty_per_code, sale_order, po, lot_id); lỗi phần này chỉ thành cảnh báo, WO vẫn mở được.
    // allowClosed = true (chỉ màn nháp /toastv2): đọc cả WO đã hoàn thành / huỷ, và không nhớ vào RAM (để /toast không dùng nhầm).
    public async Task<(Dictionary<string, string>? info, string? error)> ReadAsync(string wo, bool allowClosed = false)
    {
        bool isTest = options.CurrentValue.IsTestWo(wo);
        var (info, error) = isTest ? await TestWorkOrderAsync(wo) : await viindoo.GetWorkOrderAsync(wo, allowClosed);
        if (info == null) return (info, error);

        if (!isTest)
        {
            List<string> warnings;
            try
            {
                (var data, warnings) = await viindoo.GetLabelDataAsync(info);
                foreach (var (k, v) in data) info[k] = v;
            }
            catch (Exception ex)
            {
                warnings = ["Không lấy được dữ liệu tem (mã khách / PO / Lot) từ Viindoo: " + ex.Message];
            }
            info[WarningsKey] = string.Join("\n", warnings);
        }
        if (!allowClosed) cache.Set(CacheKey(wo), info, CacheTime);
        return (info, null);
    }

    // Lúc đóng box: lấy trong bộ nhớ, chưa có (VD app vừa khởi động lại) thì đọc lại. Lỗi trả null.
    public async Task<Dictionary<string, string>?> GetAsync(string wo)
    {
        if (cache.TryGetValue(CacheKey(wo), out Dictionary<string, string>? info)) return info;
        try
        {
            return (await ReadAsync(wo)).info;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Thông tin WO thử, cùng dạng ViindooRpc.GetWorkOrderAsync trả về (sản phẩm lấy từ Toast_Product, không gọi Viindoo)
    async Task<(Dictionary<string, string>? info, string? error)> TestWorkOrderAsync(string wo)
    {
        var test = options.CurrentValue.TestWo;
        var product = await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.ProductId == test.ProductId);
        if (product == null)
            return (null, $"WO thử {wo}: chưa có sản phẩm thử. Vào Quản trị → Sản phẩm, thêm sản phẩm với Viindoo product ID {test.ProductId}.");
        return (new Dictionary<string, string>
        {
            ["name"] = wo,
            ["product_id"] = product.ProductId.ToString(),
            ["product_code"] = product.DefaultCode,
            ["product_name"] = product.Name,
            ["product_qty"] = test.ProductQty.ToString(),
            ["origin"] = "WO thử",
            ["lot_id"] = test.LotId.Trim()
        }, null);
    }
}
