using Microsoft.Extensions.Caching.Memory;
using Toast.Web.Features.Viindoo;

namespace Toast.Web.Features.Station;

/// <summary>
/// Danh sách nhân viên có mã SVN (đọc Viindoo bằng ViindooRpc.GetEmployeesAsync, chỉ đọc), nhớ trong RAM 1 giờ:
/// nhân viên mới thêm trên Viindoo hiện sau tối đa 1 giờ. Đọc lỗi thì coi như danh sách rỗng (vẫn nhập tay được), 1 phút sau thử lại.
/// Dùng khi mở phiên: kiểm tra mã nhân viên có tồn tại và lưu tên / điện thoại vào Toast_SessionConfirm.
/// </summary>
public sealed class EmployeeDirectory(ViindooRpc viindoo, IMemoryCache cache, ILogger<EmployeeDirectory> logger)
{
    const string CacheKey = "toast-employees";
    static readonly TimeSpan CacheTime = TimeSpan.FromHours(1);
    static readonly TimeSpan RetryTime = TimeSpan.FromMinutes(1);

    // Mã (SVN0902) -> nhân viên
    public async Task<IReadOnlyDictionary<string, ViindooEmployee>> GetAsync()
    {
        if (cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, ViindooEmployee>? list) && list != null) return list;
        try
        {
            list = (await viindoo.GetEmployeesAsync()).ToDictionary(e => e.Code, StringComparer.OrdinalIgnoreCase);
            cache.Set(CacheKey, list, CacheTime);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không đọc được danh sách nhân viên từ Viindoo");
            list = new Dictionary<string, ViindooEmployee>();
            cache.Set(CacheKey, list, RetryTime);
        }
        return list;
    }

    // Nhân viên theo mã. listAvailable = false: chưa đọc được danh sách (Viindoo lỗi / rỗng) nên không kiểm tra được mã
    public async Task<(ViindooEmployee? employee, bool listAvailable)> FindAsync(string code)
    {
        var list = await GetAsync();
        return (list.GetValueOrDefault(code), list.Count > 0);
    }
}
