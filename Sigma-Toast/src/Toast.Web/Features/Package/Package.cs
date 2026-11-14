namespace Toast.Web.Features.Package;

/// <summary>
/// 1 kiện đóng gói (bảng Toast_Package), hiện tại là box. Server giữ số pcs đã quét để refresh / đổi máy không mất.
/// Mỗi WO có tối đa 1 kiện Open; đủ Capacity pcs thì chuyển Closed, in tem và ghi 1 dòng SVN_ProductionInputLogs.
/// </summary>
public class Package
{
    public int Id { get; set; }
    public string WoName { get; set; } = "";
    // Nhóm lệnh (= Toast_Session.WoGroup, tên gốc VD NM/MO/06230). Lệnh mới của nhóm (06230-002 sau -001): box mới nhất của nhóm
    // còn Open và chưa đủ pcs/box thì chuyển sang lệnh mới quét tiếp; số box đánh liên tục theo nhóm
    public string WoGroup { get; set; } = "";
    public int ProductId { get; set; }
    public string DefaultCode { get; set; } = "";
    // Số thứ tự kiện trong nhóm lệnh (1, 2, 3...): kiện mới = số lớn nhất hiện có của nhóm + 1. Chỉ dùng nội bộ, tem chưa in số này.
    public int PackageNo { get; set; }
    public int Qty { get; set; }
    // Số pcs / box tại lúc mở kiện (chụp từ Toast_Product, đổi quy cách sau đó không ảnh hưởng kiện này)
    public int Capacity { get; set; }
    public string Status { get; set; } = PackageStatus.Open;
    // In tự động: máy in (Toast_Printer.Id), thời điểm in thành công, lỗi nếu có
    public int? PrinterId { get; set; }
    // Tem in ra đâu (Server TCP / TCP qua Node / USB qua Node · máy · IP:port), có cả máy USB / IP nhập tay không có PrinterId
    public string? PrintTarget { get; set; }
    public DateTime? PrintedAt { get; set; }
    public string? PrintError { get; set; }
    // id dòng SVN_ProductionInputLogs ghi lúc đóng kiện (cho dashboard). Chỉ để tra cứu khi sửa tay trong DB;
    // sửa / xoá kiện sau đó không đồng bộ sang dòng log.
    public int? InputLogId { get; set; }
    // Phiên đóng kiện (Toast_Session): kiện thuộc phiên nào. Tem in theo giá trị của lần xác nhận mới nhất của phiên đó
    public int? SessionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public static class PackageStatus
{
    public const string Open = "Open";
    public const string Closed = "Closed";
    public static readonly string[] All = [Open, Closed];
}

// Toast:WriteDashboardLog: đóng kiện thì ghi SVN_ProductionInputLogs (dashboard lấy sản lượng Toast từ bảng này).
// Mặc định tắt để chạy song song với màn "Nhập kết quả sản xuất" cũ mà không bị cộng đôi.
public class PackageOptions
{
    public const string Section = "Toast";
    public bool WriteDashboardLog { get; set; }
}

// Box trạm đã in lúc mất mạng (qua agent USB / TCP qua Node, mẫu tem lưu trên trình duyệt): gửi kèm lô quét khi có mạng lại.
// EndSeq = số thứ tự lần quét làm đủ box (server chỉ dùng box có EndSeq > số đã nhận, theo thứ tự). ClosedAt = giờ máy trạm.
// Dùng cả cho ScanRequest.Boxes (box chưa đồng bộ, có thể sửa / xoá / đóng thiếu ở trạm khi mất kết nối): Qty = số pcs của box,
// Printed = trạm đã in tem (false = server đóng rồi in bù)
public class OfflinePrintedBox
{
    public int BoxNo { get; set; }
    public int Qty { get; set; }
    public bool Printed { get; set; } = true;
    public DateTime? ClosedAt { get; set; }
    public string? Printer { get; set; }
    public string? Version { get; set; }
    public long EndSeq { get; set; }
}
