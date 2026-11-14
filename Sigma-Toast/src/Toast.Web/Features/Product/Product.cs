namespace Toast.Web.Features.Product;

/// <summary>Quy cách sản phẩm (bảng Toast_Product). Nối với WO trên Viindoo bằng ProductId.</summary>
public class Product
{
    public int Id { get; set; }
    // product.product id trên Viindoo (product_id[0] của WO): khoá nối, không đổi khi đổi tên / mã
    public int ProductId { get; set; }
    // Mã sản phẩm (default_code), dùng tra mẫu tem theo LabelTemplate.DefaultCode
    public string DefaultCode { get; set; } = "";
    public string Name { get; set; } = "";
    // Mã quét pcs lấy từ đâu (ScanSources): Barcode = mã cố định ở cột Barcode; LotId = Lot ID của WO (đơn bán Viindoo, đổi theo đơn)
    public string ScanSource { get; set; } = ScanSources.Barcode;
    // Barcode dán trên pcs (chỉ dùng khi ScanSource = Barcode). Có thể trùng giữa 2 sản phẩm (VD bản EN / EU dùng chung mã)
    public string Barcode { get; set; } = "";
    public int PcsPerBox { get; set; }
    // Cấp đóng gói thứ 2: Carton / Plate, trống = chỉ có box
    public string? OuterType { get; set; }
    public int? BoxPerOuter { get; set; }
    // x_quantity_per_code trên Viindoo lần đọc gần nhất (để đối chiếu với PcsPerBox)
    public int? ViindooQtyPerCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Nguồn mã quét pcs của sản phẩm
public static class ScanSources
{
    public const string Barcode = "Barcode";
    public const string LotId = "LotId";
    public static readonly string[] All = [Barcode, LotId];
}

// Dữ liệu form Thêm/Sửa gửi lên từ /toast/admin/product
public class ProductRequest
{
    public int ProductId { get; set; }
    public string? DefaultCode { get; set; }
    public string? Name { get; set; }
    public string? ScanSource { get; set; }
    public string? Barcode { get; set; }
    public int PcsPerBox { get; set; }
    public string? OuterType { get; set; }
    public int? BoxPerOuter { get; set; }
    public int? ViindooQtyPerCode { get; set; }
}
