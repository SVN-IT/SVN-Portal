using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Toast.Web.Features.LabelTemplate;
using Toast.Web.Features.Package;
using Toast.Web.Features.Printer;
using Toast.Web.Features.Product;
using Toast.Web.Features.Station;

namespace Toast.Web.Data;

/// <summary>
/// DbContext của Toast. Bảng nằm trong svn_pentaho.dbo với tiền tố Toast_ để không đụng bảng SVN_* hiện có.
/// </summary>
public class ToastDbContext(DbContextOptions<ToastDbContext> options) : DbContext(options)
{
    public const string TablePrefix = "Toast_";

    public DbSet<LabelTemplate> LabelTemplates => Set<LabelTemplate>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<Printer> Printers => Set<Printer>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionConfirm> SessionConfirms => Set<SessionConfirm>();
    // Bảng có sẵn của SVN-Portal (dùng chung), Toast chỉ thêm dòng; không nằm trong migration
    public DbSet<ProductionInputLog> ProductionInputLogs => Set<ProductionInputLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema("dbo");

        b.Entity<LabelTemplate>(e =>
        {
            e.Property(x => x.DefaultCode).HasMaxLength(50);
            e.Property(x => x.Type).HasMaxLength(50);
            e.Property(x => x.Operation).HasMaxLength(50);
            e.Property(x => x.Size).HasMaxLength(20);
            e.Property(x => x.Dpi).HasDefaultValue(600);
            e.Property(x => x.IsLocked).HasDefaultValue(false);
            // Danh sách biến lưu thành 1 cột JSON (nvarchar(max)), không cần bảng riêng
            e.Property(x => x.Variables)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<List<LabelVariable>>(v, (JsonSerializerOptions?)null) ?? new List<LabelVariable>(),
                    new ValueComparer<List<LabelVariable>>(
                        (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
                        v => JsonSerializer.Deserialize<List<LabelVariable>>(JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)!))
                .HasDefaultValue(new List<LabelVariable>());
            e.HasIndex(x => new { x.DefaultCode, x.Type, x.Operation }).IsUnique();
        });

        b.Entity<Product>(e =>
        {
            e.Property(x => x.DefaultCode).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(255);
            e.Property(x => x.ScanSource).HasMaxLength(20).HasDefaultValue(ScanSources.Barcode);
            e.Property(x => x.Barcode).HasMaxLength(50);
            e.Property(x => x.OuterType).HasMaxLength(20);
            e.HasIndex(x => x.ProductId).IsUnique();
            e.HasIndex(x => x.DefaultCode);
            // Không unique: 1 barcode có thể dùng cho 2 sản phẩm (VD bản EN / EU)
            e.HasIndex(x => x.Barcode);
        });

        b.Entity<Package>(e =>
        {
            e.Property(x => x.WoName).HasMaxLength(50);
            // Mặc định '' để chép được dữ liệu cũ (DevSqlite); dữ liệu cũ được điền nhóm từ tên lệnh (011 / DevSqlite.FillWoGroup)
            e.Property(x => x.WoGroup).HasMaxLength(50).HasDefaultValue("");
            e.HasIndex(x => new { x.WoGroup, x.Status });
            e.Property(x => x.DefaultCode).HasMaxLength(50);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.PrintError).HasMaxLength(500);
            e.Property(x => x.PrintTarget).HasMaxLength(200);
            e.HasIndex(x => new { x.WoName, x.PackageNo }).IsUnique();
            e.HasIndex(x => new { x.WoName, x.Status });
            // Máy in đã in box thì không xoá được (chỉ Ngừng dùng)
            e.HasOne<Printer>().WithMany().HasForeignKey(x => x.PrinterId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Session>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Session>(e =>
        {
            e.Property(x => x.WoName).HasMaxLength(50);
            e.Property(x => x.WoGroup).HasMaxLength(50).HasDefaultValue("");
            e.Property(x => x.DefaultCode).HasMaxLength(50);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.EndedBy).HasMaxLength(50);
            e.Property(x => x.PushStatus).HasMaxLength(20);
            e.Property(x => x.PushMessage).HasMaxLength(500);
            e.Property(x => x.DeviceId).HasMaxLength(64);
            e.HasIndex(x => new { x.WoName, x.Status });
            e.HasIndex(x => new { x.WoGroup, x.Status });
        });

        b.Entity<SessionConfirm>(e =>
        {
            e.Property(x => x.EmployeeCode).HasMaxLength(20);
            e.Property(x => x.EmployeeName).HasMaxLength(200);
            e.Property(x => x.EmployeePhone).HasMaxLength(50);
            e.Property(x => x.PrintTarget).HasMaxLength(200);
            e.Property(x => x.ScanCode).HasMaxLength(100);
            // Giá trị biến tem lưu thành 1 cột JSON (nvarchar(max))
            e.Property(x => x.Values)
                .HasConversion(
                    v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                    v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions?)null) ?? new Dictionary<string, string>(),
                    new ValueComparer<Dictionary<string, string>>(
                        (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) == JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
                        v => new Dictionary<string, string>(v)));
            e.HasOne<Session>().WithMany().HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<Printer>().WithMany().HasForeignKey(x => x.PrinterId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.EmployeeCode);
        });

        b.Entity<Printer>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(50);
            e.Property(x => x.Name).HasMaxLength(100);
            e.Property(x => x.Ip).HasMaxLength(50);
            e.Property(x => x.Port).HasDefaultValue(Printer.DefaultPort);
            e.Property(x => x.Location).HasMaxLength(100);
            e.Property(x => x.IsActive).HasDefaultValue(true);
            e.HasIndex(x => x.Code).IsUnique();
        });

        foreach (var entity in b.Model.GetEntityTypes())
            entity.SetTableName(TablePrefix + entity.ClrType.Name);

        // Đặt sau vòng lặp trên để giữ đúng tên bảng có sẵn (không có tiền tố Toast_)
        b.Entity<ProductionInputLog>(e =>
        {
            e.ToTable("SVN_ProductionInputLogs", t => t.ExcludeFromMigrations());
            e.Property(x => x.Id).HasColumnName("id");
            e.Property(x => x.Level).HasColumnName("level");
            e.Property(x => x.ProductId).HasColumnName("product_id");
            e.Property(x => x.ProductQty).HasColumnName("product_qty").HasPrecision(18, 4);
            e.Property(x => x.DateFinished).HasColumnName("date_finished").HasColumnType("datetime");
            e.Property(x => x.ProductType).HasColumnName("product_type");
            e.Property(x => x.SerialCode).HasColumnName("serial_code");
            e.Property(x => x.ComponentList).HasColumnName("component_list");
            e.Property(x => x.State).HasColumnName("state");
            e.Property(x => x.ApiFunction).HasColumnName("API_function");
            e.Property(x => x.ApiParameters).HasColumnName("API_parameters");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.WoCode).HasColumnName("wo_code");
            e.Property(x => x.MasterWoCode).HasColumnName("master_wo_code");
            e.Property(x => x.TotalQty).HasColumnName("total_qty").HasPrecision(18, 4);
            e.Property(x => x.RemainQty).HasColumnName("remain_qty").HasPrecision(18, 4);
            e.Property(x => x.ConsumedWoCode).HasColumnName("consumed_wo_code");
        });
    }
}
