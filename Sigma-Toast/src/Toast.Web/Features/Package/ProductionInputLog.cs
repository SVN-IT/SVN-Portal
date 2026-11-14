namespace Toast.Web.Features.Package;

/// <summary>
/// 1 dòng bảng có sẵn dbo.SVN_ProductionInputLogs (của SVN-Portal, dùng chung). Toast chỉ thêm dòng khi đóng kiện.
/// SP SVN_Sync_Production_By_Hour_1s cộng product_qty theo product_id (ProductMapping) và khung giờ date_finished
/// để ra sản lượng Toast trên Sigma-Dashboard. Migration không tạo / sửa bảng này.
/// </summary>
public class ProductionInputLog
{
    // Giá trị status của dòng Toast ghi: SP dashboard không lọc status nên vẫn được cộng,
    // còn các job đồng bộ Viindoo chỉ lấy 'Not synchronized' nên không đẩy từng kiện lên Viindoo
    public const string ToastStatus = "Toast";

    public int Id { get; set; }
    public int? Level { get; set; }
    public int? ProductId { get; set; }
    public decimal? ProductQty { get; set; }
    public DateTime? DateFinished { get; set; }
    public string? ProductType { get; set; }
    public string? SerialCode { get; set; }
    public string? ComponentList { get; set; }
    public string? State { get; set; }
    public string? ApiFunction { get; set; }
    public string? ApiParameters { get; set; }
    public string? Status { get; set; }
    public string? WoCode { get; set; }
    public string? MasterWoCode { get; set; }
    public decimal? TotalQty { get; set; }
    public decimal? RemainQty { get; set; }
    public string? ConsumedWoCode { get; set; }
}
