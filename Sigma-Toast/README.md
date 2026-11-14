# Toast

Hệ thống quét barcode pcs → in tem box → in tem carton/plate, nhập kết quả sản xuất với Viindoo, chọn máy in, in tay/tự động, màn hình quản trị.
Làm cho đơn hàng Toast, hướng tới dùng chung cho nhiều dự án. Thay thế Twist trong SVN-Portal.

> Đây là **boilerplate**: chưa có chức năng nghiệp vụ. Thiết kế và code sẽ làm lại từng bước.
> Bản thử nghiệm trước đó được lưu ở `~/work/Toast-backup-20261030` (chỉ để tham khảo).

## Có sẵn

- ASP.NET Core 8 MVC, giao diện **Vuexy** (`wwwroot/themes/vuexy`, copy từ SVN-Portal), layout sidebar.
- Đường dẫn: `/` và `/toast` là màn công nhân (quét, in tự động); `/toast/admin` là quản trị.
- Đăng nhập admin bằng cookie (`/toast/admin/account/login`), tài khoản/mật khẩu lưu trong `appsettings` (giống TwistAdmin).
- EF Core + SQL Server, `ToastDbContext` **chưa có bảng**. Bảng mới đặt trong `svn_pentaho.dbo` với tiền tố `Toast_`.
- Test khởi động app (`tests/Toast.Tests`).

## Cấu trúc (chia theo chức năng)

```
src/Toast.Web/
  Program.cs                 cấu hình app, đăng nhập, EF Core, chỗ tìm view
  _ViewImports, _ViewStart   để ở gốc project để áp dụng cho cả Features/ và Shared/
  Features/                  mỗi chức năng 1 thư mục: controller + view + service của nó
    Account/                 đăng nhập admin (AccountController, Login.cshtml, AdminUsers)
    Station/                 /toast: màn công nhân. Chọn máy in, mở WO, quét pcs, đủ pcs/box tự in tem Box;
                             sửa số / đóng / xoá box, in thủ công cần PIN leader (Toast:LeaderPin)
    StationV2/               /toastv2: bản nháp chỉ xem, nhập WO -> dữ liệu Viindoo, biến tem (nguồn, thiếu), xem trước tem
    SaleOrderLookup/         /toast/admin/sale-order: tra cứu đơn bán (dòng đơn, PO / Lot theo dòng, nhóm WO ghép theo SL)
    WorkOrderLookup/         /toast/admin/work-order: tra cứu lệnh sản xuất (lọc dự án / loại / trạng thái, chi tiết + NVL + backorder)
    ProductLookup/           /toast/admin/viindoo-product: tra cứu sản phẩm Viindoo (phân loại, BoM, WO mở, đơn bán, Toast)
    Package/                 Toast_Package: kiện đóng gói (hiện là box) ở server + màn /toast/admin/package (sửa / xoá);
                             đóng kiện ghi 1 dòng SVN_ProductionInputLogs cho dashboard (Toast:WriteDashboardLog)
    Printer/                 máy in (Toast_Printer) + màn quản lý /toast/admin/printer; PrinterLookup = máy đang dùng
                             cho In tem và màn công nhân
    Home/                    /toast/admin: trang chủ quản trị
    Product/                 quy cách sản phẩm (Toast_Product): barcode pcs, pcs/box, cấp 2 Carton/Plate
    Viindoo/                 ViindooRpc: đọc thẳng Viindoo bằng JSON-RPC (chỉ đọc): WO (mrp.production), sản phẩm theo mã
    LabelTemplate/           mẫu tem ZPL theo mã sản phẩm (mỗi sản phẩm 1 mẫu): danh sách, thêm/sửa/xoá, biến, khoá, nhân bản, xem trước
    LabelPrint/              in tem: tra WO, chọn mẫu, điền biến, in qua Server TCP / agent Node (chuyển từ TwistAdmin)
  Shared/                    layout Vuexy + partial dùng chung (menu ở biến menuItems trong _Layout)
  Data/ToastDbContext.cs     DbContext (thêm DbSet tại đây)
tests/Toast.Tests/           xUnit
```

Thêm chức năng mới: tạo `Features/<Tên>/` chứa `<Tên>Controller.cs` và các view `.cshtml` ngay cạnh.
View được tìm ở `Features/<Tên controller>/` rồi đến `Shared/`. Namespace: `Toast.Web.Features.<Tên>`.

## Chạy thử (không cần DB)

Shell fish:

```fish
cd src/Toast.Web
dotnet watch --launch-profile http
```

Mở http://localhost:5128 (màn công nhân) hoặc http://localhost:5128/toast/admin (quản trị), đăng nhập `admin` / `demo123` (khai báo trong `appsettings.Development.json`).
Khi chạy thử, dữ liệu lưu vào file SQLite `src/Toast.Web/toast-dev.db` (tự tạo). Xoá file này để làm lại từ đầu.

## Mật khẩu admin trên server

Tạo file `appsettings.Production.json` cạnh `Toast.Web.dll` trên server (file này không có trong repo,
nên publish không ghi đè):

```json
{
  "ConnectionStrings": { "Toast": "Server=...;Database=svn_pentaho;User Id=...;Password=...;TrustServerCertificate=True" },
  "Toast": { "AdminUsers": [ { "UserName": "admin", "Password": "..." } ] }
}
```

Đổi mật khẩu: sửa file này và lưu, app tự nhận, **không cần restart**. Thêm tài khoản: thêm phần tử vào `AdminUsers`.

## Khi có bảng mới

Sinh script SQL (không cần kết nối DB), rồi chạy tay trên `svn_pentaho`:

```
dotnet ef migrations add <Ten> -p src/Toast.Web -o Data/Migrations
dotnet ef migrations script --idempotent -p src/Toast.Web -o db/scripts/00X_<Ten>.sql
```

Chuỗi kết nối và mật khẩu thật đặt trong `appsettings.Production.json` trên server, không commit.
