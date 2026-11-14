using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Toast.Web.Data;

/// <summary>
/// Chỉ dùng cho lệnh dotnet ef: luôn sinh migration/script cho SQL Server (server thật),
/// kể cả khi máy dev chạy SQLite. Không kết nối DB.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ToastDbContext>
{
    public ToastDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<ToastDbContext>()
            .UseSqlServer("Server=.;Database=design_time_only", sql => sql.MigrationsHistoryTable("Toast__EFMigrationsHistory", "dbo"))
            .Options);
}
