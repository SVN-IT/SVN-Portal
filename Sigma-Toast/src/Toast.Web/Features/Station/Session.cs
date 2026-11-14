namespace Toast.Web.Features.Station;

/// <summary>
/// Phiên làm việc của 1 lệnh (backorder) trên màn công nhân (bảng Toast_Session). Mỗi lệnh (theo id Viindoo) chỉ có 1 phiên Open:
/// trạm nào mở lệnh đó cũng vào chung phiên đang mở. Lệnh tách backorder sau mỗi lần nhập KQSX (06230 -> 06230-001 done
/// + 06230-002 mới): lệnh mới = phiên mới (phiên lệnh trước còn mở thì tự kết thúc); box dở đi theo nhóm lệnh (WoGroup) sang phiên mới.
/// Tắt máy / refresh / đổi máy thì phiên vẫn mở, mở lại WO phải xác nhận lại (thêm 1 dòng Toast_SessionConfirm).
/// </summary>
public class Session
{
    public int Id { get; set; }
    // Tên lệnh của phiên (giống Toast_Package.WoName)
    public string WoName { get; set; } = "";
    // Nhóm lệnh = tên gốc (procurement_group_id trên Viindoo, VD NM/MO/06230); WO thử = tên WO
    public string WoGroup { get; set; } = "";
    // id lệnh (mrp.production) trên Viindoo, null = WO thử. Khoá của phiên (1 phiên Open / lệnh); nhập KQSX theo id này (tên lệnh có thể đổi)
    public int? ViindooMoId { get; set; }
    public int ProductId { get; set; }
    public string DefaultCode { get; set; } = "";
    public string Status { get; set; } = SessionStatus.Open;
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? EndedBy { get; set; }
    // Nhập KQSX lên Viindoo lúc kết thúc phiên (ProductionPushService): PushStatus null = chưa kết thúc / chưa xử lý
    public string? PushStatus { get; set; }
    public int? PushQty { get; set; }
    public DateTime? PushedAt { get; set; }
    public string? PushMessage { get; set; }
    // Chi tiết lần nhập gần nhất (JSON ProductionPushTrace: lệnh trước / sau, số lượng, tiêu hao NVL, các bước + lỗi)
    public string? PushDetail { get; set; }
    // Máy đang giữ phiên (mã ngẫu nhiên lưu trong localStorage của trình duyệt trạm). Trạm đếm pcs ở trình duyệt, gửi lên theo lô khi
    // đủ box: server chỉ nhận lô của máy đang giữ phiên; máy khác vào làm (xác nhận) thì giữ phiên, lô cũ của máy trước bị từ chối
    public string? DeviceId { get; set; }
    // Số thứ tự lần quét cuối đã nhận của máy đang giữ phiên: gửi lại cùng lô (mạng chập chờn) không bị cộng đôi
    public long? LastScanSeq { get; set; }
}

public static class PushStatus
{
    public const string Done = "Done";       // đã nhập, lệnh done trên Viindoo
    public const string Error = "Error";     // lỗi, admin bấm Đẩy KQSX để thử lại
    public const string Off = "Off";         // công tắc tắt lúc kết thúc phiên, admin có thể bấm Đẩy KQSX sau
    public const string Skipped = "Skipped"; // không cần nhập (WO thử, không có box đã đóng)
    public const string Mismatch = "Mismatch"; // lệnh đã được hoàn thành trên Viindoo không phải từ Toast: cần kiểm tra
}

public static class SessionStatus
{
    public const string Open = "Open";
    public const string Ended = "Ended";
}

/// <summary>
/// 1 lần xác nhận thông tin trước khi làm (bảng Toast_SessionConfirm): lần mở phiên và mỗi lần vào lại. Lưu để truy vết
/// ai xác nhận, ở máy in nào, giá trị tem lúc đó (biến lấy từ mẫu / WO / sản phẩm) và các cảnh báo đã thấy.
/// </summary>
public class SessionConfirm
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    // Mã nhân viên dạng SVNxxxx (x là số)
    public string EmployeeCode { get; set; } = "";
    // Tên / điện thoại (work_phone) của nhân viên trên Viindoo lúc xác nhận; null = mã nhập tay không có trong danh sách Viindoo
    public string? EmployeeName { get; set; }
    public string? EmployeePhone { get; set; }
    // Máy in trong danh sách (null nếu USB / IP nhập tay); PrintTarget = mô tả máy in của trạm lúc xác nhận
    public int? PrinterId { get; set; }
    public string? PrintTarget { get; set; }
    public DateTime ConfirmedAt { get; set; }
    // Giá trị biến tem đã xác nhận (KEY -> giá trị), không gồm biến lấy lúc in (số box, ngày giờ)
    public Dictionary<string, string> Values { get; set; } = [];
    // Các cảnh báo hiển thị lúc xác nhận, mỗi dòng 1 cảnh báo
    public string? Warnings { get; set; }
    // Mã quét pcs đã xác nhận (barcode cố định hoặc Lot ID của WO). Lúc quét so với mã của lần xác nhận gần nhất
    public string? ScanCode { get; set; }
}

public class SessionStartRequest
{
    public string? Wo { get; set; }
    public string? EmployeeCode { get; set; }
    public bool Confirmed { get; set; }
    // Cấu hình máy in của trạm (modal Cấu hình máy in). PrinterId riêng = máy trong danh sách, Server TCP
    public Toast.Web.Features.Printer.PrintTarget? Printer { get; set; }
    public string? PrinterId { get; set; }
    public Toast.Web.Features.Printer.PrintTarget Target => Printer ?? new() { PrinterId = PrinterId };
    // Mã máy của trình duyệt trạm: máy xác nhận vào làm thì giữ phiên (Session.DeviceId)
    public string? DeviceId { get; set; }
}
