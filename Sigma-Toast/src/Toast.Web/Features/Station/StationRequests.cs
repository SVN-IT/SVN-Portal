namespace Toast.Web.Features.Station;

/// <summary>PIN leader dùng chung (Toast:LeaderPin trong appsettings), cần cho các thao tác chỉnh số / in thủ công.</summary>
public class StationOptions
{
    public const string Section = "Toast";
    public string LeaderPin { get; set; } = "";
    // Toast:RequireLeaderPin: false = Menu F2 không hỏi PIN, API bỏ kiểm PIN. Mặc định bật. Sửa appsettings: server nhận ngay,
    // màn /toast đang mở phải tải lại trang
    public bool RequireLeaderPin { get; set; } = true;
    // Toast:AutoEndInactiveHours: phiên Open không hoạt động (quét / sửa box, xác nhận) từ số giờ này trở lên thì tự kết thúc + nhập KQSX
    // (SessionAutoEndService, kiểm tra mỗi phút). 0 / không khai báo = tắt. Sửa appsettings là app tự nhận
    public double AutoEndInactiveHours { get; set; }
    // Toast:ScanSyncEvery: trạm còn từ N pcs chưa gửi thì gửi lên server (ghi DB), ngoài lúc đủ box. 0 / không khai báo = chỉ gửi khi
    // đủ box. Sửa appsettings: màn /toast tải lại trang mới nhận
    public int ScanSyncEvery { get; set; }
    // Toast:ClientLogDir: thư mục ghi log lỗi gửi từ trình duyệt trạm (ClientLogService), tương đối = trong thư mục app.
    // Mặc định Windows C:\ProgramData\SigmaToast\logs, máy khác "logs" trong thư mục app
    public string? ClientLogDir { get; set; }
    public TestWoOptions TestWo { get; set; } = new();
    // Toast:PushProduction: kết thúc phiên thì nhập KQSX lên Viindoo (ProductionPushService). Mặc định tắt; sửa appsettings là app tự nhận
    public bool PushProduction { get; set; }

    // WO thử (Toast:TestWo:Name): không tra Viindoo, không ghi log dashboard
    public bool IsTestWo(string? wo) =>
        !string.IsNullOrWhiteSpace(TestWo.Name) && string.Equals((wo ?? "").Trim(), TestWo.Name.Trim(), StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// WO thử để chạy thử trên server thật (Toast:TestWo). Mở WO này thì không gọi Viindoo: sản phẩm lấy từ Toast_Product theo
/// ProductId (phải khai báo ở màn Sản phẩm). Quét / in tem như thật nhưng không ghi SVN_ProductionInputLogs. Name trống = tắt.
/// </summary>
public class TestWoOptions
{
    public string Name { get; set; } = "";
    public int ProductId { get; set; }
    public int ProductQty { get; set; }
    // Lot ID giả của WO thử (sản phẩm quét theo Lot ID cần có), trống = không có Lot
    public string LotId { get; set; } = "";
}

public class ScanRequest
{
    public string? Wo { get; set; }
    public int ProductId { get; set; }
    public string? Barcode { get; set; }
    // Lô quét từ trình duyệt (trạm đếm trước, gửi khi đủ box / trước thao tác box): Count pcs cùng mã Barcode, số thứ tự FromSeq..ToSeq
    // của máy DeviceId. Không gửi Seq / DeviceId (test, gọi tay) = 1 lần quét như cũ, không kiểm tra máy
    public int Count { get; set; } = 1;
    public string? DeviceId { get; set; }
    public long? FromSeq { get; set; }
    public long? ToSeq { get; set; }
    // Box đủ lúc mất mạng mà trạm đã in qua agent (USB / TCP qua Node, máy in đang chọn): server đóng box theo thông tin này, không in lại
    public List<Package.OfflinePrintedBox>? OfflineBoxes { get; set; }
    // Box chưa đồng bộ do trạm tự giữ khi mất kết nối (có thể đã sửa số / xoá / đóng thiếu ở trạm), theo thứ tự: có Boxes thì server
    // không chia theo quy cách nữa mà đóng từng box đúng Qty (Printed thì không in lại), rồi đặt box đang quét = OpenQty pcs.
    // Gửi lại: box có EndSeq <= số đã nhận bị bỏ, OpenQty là số tuyệt đối nên không cộng đôi
    public List<Package.OfflinePrintedBox>? Boxes { get; set; }
    public int? OpenQty { get; set; }
    // Cấu hình máy in của trạm (modal Cấu hình máy in). PrinterId riêng = máy trong danh sách, Server TCP
    public Toast.Web.Features.Printer.PrintTarget? Printer { get; set; }
    public string? PrinterId { get; set; }
    public Toast.Web.Features.Printer.PrintTarget Target => Printer ?? new() { PrinterId = PrinterId };
}

public class PinRequest
{
    public string? Pin { get; set; }
    public Toast.Web.Features.Printer.PrintTarget? Printer { get; set; }
    public string? PrinterId { get; set; }
    public Toast.Web.Features.Printer.PrintTarget Target => Printer ?? new() { PrinterId = PrinterId };
}

// Kết quả trình duyệt gửi tem tới agent (chế độ TCP / USB qua Node)
public class PrintResultRequest
{
    public bool Ok { get; set; }
    public string? Error { get; set; }
}

public class EndSessionRequest : PinRequest
{
    public bool CloseOpenBox { get; set; }
}

public class BoxQtyRequest : PinRequest
{
    public int Qty { get; set; }
}

// Menu → In tem: giá trị biến (KEY -> giá trị) đã xem / sửa
public class StationLabelPrintRequest : PinRequest
{
    public int TemplateId { get; set; }
    public int Copies { get; set; } = 1;
    public Dictionary<string, string?>? Values { get; set; }
}
