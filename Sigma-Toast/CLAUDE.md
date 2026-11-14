# CLAUDE.md

Hướng dẫn cho Claude Code khi làm việc trong repo Toast. Trả lời người dùng bằng tiếng Việt.

## Dự án

Toast thay thế Twist (trong SVN-Portal): quét barcode pcs → đủ số lượng in tem box → đủ box in tem carton
(hoặc plate, tuỳ dự án), nhập kết quả sản xuất với Viindoo, chọn máy in, in tự động / in tay, màn quản trị.
Làm cho đơn hàng Toast nhưng hướng tới dùng chung nhiều dự án.

Hiện tại repo là **boilerplate** (xem README.md). Người dùng thiết kế và làm lại **từng bước thủ công**.
Bản thử nghiệm cũ ở `~/work/Toast-backup-20261030`: chỉ để tham khảo, không khôi phục.

## Quy tắc làm việc (bắt buộc)

- **Bàn trước, làm sau.** Đề xuất bước nhỏ, chờ người dùng đồng ý rồi mới code. Không tự thêm tính năng,
  bảng, project hay lớp trừu tượng ngoài yêu cầu. Bản cũ bị bỏ vì over-engineer (16 bảng, 5 project).
- Câu hỏi "làm thế nào" → hướng dẫn bằng công cụ đang có, không tự tạo file cấu hình mới.
- **Không kết nối DB, không chạy SQL** dưới bất kỳ hình thức nào. Chỉ viết file `.sql` vào `db/scripts/`
  để người dùng tự chạy trong DBeaver.
- **Không ghi gì lên Viindoo** (write, create, button_mark_done…) trong lúc phát triển/test.
  Chỉ được đọc (search_read, read, fields_get) khi người dùng cho phép, và mọi query phải có `limit`.
- Không chạy app SVN-Portal (nó query DB production lúc khởi động).
- Không commit mật khẩu / chuỗi kết nối thật. Trên server chúng nằm trong `appsettings.Production.json`
  (đã gitignore). Mật khẩu admin dạng chữ thường trong config, giống TwistAdmin (người dùng đã chọn).
- Commit chỉ khi người dùng yêu cầu.

## Kỹ thuật

- ASP.NET Core 8 MVC, 1 project `src/Toast.Web` + 1 project test `tests/Toast.Tests` (xUnit).
- **Chia thư mục theo chức năng**: mỗi chức năng là `src/Toast.Web/Features/<Tên>/` chứa controller, view
  `.cshtml` và service của nó (namespace `Toast.Web.Features.<Tên>`). Không tạo lại `Controllers/`, `Views/`,
  `Areas/`. Layout/partial dùng chung ở `Shared/`. Chỗ tìm view cấu hình trong `Program.cs`.
- **Đường dẫn**: `/` chuyển sang `/toast` = màn công nhân (quét, in tự động, không cần đăng nhập admin).
  `/toast/admin/...` = quản trị (đăng nhập). Controller dùng attribute route `[Route("toast/admin/<tên>")]`.
- Giao diện **Vuexy** (`wwwroot/themes/vuexy`, giống SVN-Portal TwistAdmin). Menu ở biến `menuItems`
  trong `Shared/_Layout.cshtml`.
- EF Core + SQL Server. Bảng nằm trong `svn_pentaho.dbo`, tên tự có tiền tố `Toast_`
  (cấu hình trong `Data/ToastDbContext.cs`). Giữ số bảng tối thiểu.
- Máy dev chạy SQLite (`toast-dev.db`, khai báo `ConnectionStrings:ToastSqlite` trong appsettings.Development.json,
  bảng tự tạo bằng EnsureCreated; `Data/DevSqlite.cs` khi cấu trúc bảng đổi: sao lưu file cũ thành `toast-dev.db.bak-<thời gian>`, tạo lại và chép
  dữ liệu cũ sang (bảng / cột còn tồn tại), giữ 5 bản sao lưu). Test dùng `ToastFactory` với file SQLite tạm. Migration luôn sinh cho SQL Server
  nhờ `Data/DesignTimeDbContextFactory.cs`.
- Xem trước tem (`ZplPreviewService`, BinaryKits.Zpl.Viewer / SkiaSharp) dùng **font nhúng** `Features/LabelTemplate/Fonts` (EmbeddedResource):
  `^A0` → TeX Gyre Heros Cn Bold (thay CG Triumvirate Bold Condensed của Zebra, bản quyền), `^AA`..`^AH` → DejaVu Sans Mono. Không phụ thuộc font
  hệ điều hành (máy không có font từng báo `Value cannot be null. (Parameter 'asset')`).
- **Căn barcode `^FXFIT`** (13/11/2026, `Features/LabelTemplate/ZplBarcodeFit.cs`, chạy cuối `ZplVariables.Apply` nên xem trước = in): barcode Code 128
  hướng N có comment `^FXFIT` (trong field hoặc field ngay trước, máy in bỏ qua `^FX`) → sau khi điền biến: mã hoá ngắn nhất (`>:`/`>;`/`>5`/`>6`),
  căn giữa ô khung (đường `^GB` dọc bao quanh, không có thì 0 / `^PW`), không vừa (kể cả lề 10 module) thì giảm `^BY`. Không đánh dấu = ZPL giữ nguyên
  (người dùng chốt: Toast chỉ sửa ZPL khi mẫu tự đánh dấu). Bỏ qua khi còn biến chưa điền, mode ≠ N, có ký tự thoát `^FH`, đã có `>` giữa dữ liệu.
  Chữ thì căn bằng `^FB w,1,0,C` có sẵn của ZPL (mẫu tem tự làm, Toast không sửa). Mẫu carton Sling: `D:\Tem\TOAST\…\3 (Toast).prn`.
  **Nút Chuẩn hoá** (màn Mẫu tem, modal Sửa; `ZplNormalizer.cs`, `api/normalize/analyze|apply`, `js/label-template/zpl-normalize.js`, không DB):
  ZPL xuất từ ZebraDesigner (giá trị mẫu) → bảng chữ / barcode (theo y, x) → chọn biến (gợi ý từ label-variables.json + biến trong ZPL)
  + tick căn giữa (chữ: thêm `^FB<rộng ô>,1,0,C`, x = mép trái ô, đuôi `\5C&`/`\&`; barcode: thêm `^FXFIT`, bỏ `>:`/`>5`…) → xem trước Sau
  (giá trị thử = ô Nội dung) / Trước → "Dùng ZPL này" điền ô ZPL, người dùng tự Lưu. Đọc ZPL dùng chung `ZplLayout.cs` với `ZplBarcodeFit`.
  Bước 2 (chưa làm): tự gợi ý biến theo giá trị mẫu.
- `dotnet-ef` nằm ở `~/.dotnet/tools/dotnet-ef` (chưa có trong PATH).
- Mọi file `.sql` trong `db/scripts/` phải bắt đầu bằng `USE [svn_pentaho];` + `GO` **và** ghi tên bảng đủ 3 phần
  `[svn_pentaho].[dbo].[...]` (người dùng yêu cầu, chạy ở DB nào cũng vào đúng svn_pentaho). Script sinh bằng
  `migrations script <migration trước> --idempotent`, chèn dòng USE lên đầu, rồi thay `[dbo].[` → `[svn_pentaho].[dbo].[`,
  trừ: `REFERENCES [dbo].[...]` giữ 2 phần (khoá ngoại cùng DB); `EXEC sp_rename N'[dbo]...` → `EXEC [svn_pentaho].[sys].[sp_rename] N'[dbo]...`;
  `[sys].[...]` → `[svn_pentaho].[sys].[...]`. Người dùng chạy trong DBeaver bằng **Alt+X** (Execute SQL Script) vì có `GO`.
- **Data Protection** (`Program.cs`, 11/11/2026): `SetApplicationName("SigmaToast")` + lưu khoá ở `Toast:DataProtectionKeysDir` (mặc định Windows
  `C:\ProgramData\SigmaToast\DataProtection-Keys`, DPAPI máy; máy khác `<app>/keys`, gitignore; test dùng thư mục tạm). Trước đó khoá trong RAM / tách theo
  thư mục app → stop / start / deploy bản mới (thư mục mới) thì trang đang mở gửi lên bị **400** (antiforgery) và admin bị đăng xuất. Trạm còn tự hồi phục:
  POST bị 400 thì `refreshToken()` tải ngầm `/toast` lấy mã mới rồi gửi lại 1 lần (api, xem trước tem, gửi log).
- Shell của người dùng là **fish** (dùng `set -x`, không dùng `VAR=... cmd`).

Lệnh thường dùng:

```fish
dotnet build
dotnet test
cd src/Toast.Web; dotnet watch --launch-profile http        # http://localhost:5128/toast, /toast/admin (admin/demo123)
~/.dotnet/tools/dotnet-ef migrations add <Ten> -p src/Toast.Web -o Data/Migrations
~/.dotnet/tools/dotnet-ef migrations script --idempotent -p src/Toast.Web -o db/scripts/00X_<Ten>.sql
```

## Dữ kiện đã kiểm chứng (Viindoo / Twist)

- Viindoo = Odoo 16, gọi JSON-RPC (`/web/session/authenticate`, `/web/dataset/call_kw`).
- Toast **tự đọc Viindoo** bằng `Features/Viindoo/ViindooRpc.cs` (singleton, chỉ cho search_read / read / fields_get,
  search_read bắt buộc limit). Cấu hình mục `Viindoo` (Url, Db, Login trong appsettings.json; Password: user-secrets ở máy dev,
  `appsettings.Production.json` trên server). Màn Sản phẩm nhập default code → `product.product` (id, name, x_quantity_per_code
  → Pcs / box, phải > 0). WO (màn công nhân, màn In tem): `ViindooRpc.GetWorkOrderAsync` = `mrp.production` theo **đúng tên**
  (không ilike, thiếu tiền tố thì thêm `NM/MO/`), state done / cancel báo lỗi; trả name, state, product_id, product_code,
  product_name (tách từ "[mã] tên" của product_id), product_qty, origin. Test dùng Viindoo giả `tests/Toast.Tests/ViindooStub.cs`.
  Toast không còn gọi ViidooDBServiceAPI (SVN-Portal) — trước đây dùng `GetWorkOrder` (ilike + mới nhất, "đã hoàn thành" theo
  qty_producing) và `GetProductItemByCode` (đọc `product.template`, chỉ trả x_quantity_per_code).
- Máy in ở bảng riêng `Toast_Printer` (màn Dữ liệu → Máy in). Không dùng `SVN_Printer_Info` của Twist
  (không khoá chính, cột toàn nvarchar(MAX), trộn cả mẫu tem); `003_Printer.sql` có đoạn tuỳ chọn chép máy in từ bảng đó.
  Máy in không gắn dự án; máy đã in box chỉ được Ngừng dùng, không xoá.
  In USB / TCP qua Node dùng agent cũ trên PC trạm `http://localhost:8021` (/health, /usb-printers, /print, /print-usb).
- Tiêu chuẩn đóng gói pcs/box = `product.product.x_quantity_per_code` (có thể trống/0, phải xử lý).
- `barcode` sản phẩm trên Viindoo trống; số box/carton không có trên Viindoo → Toast tự giữ.
- Barcode pcs là mã chung theo SKU (không serial) → chỉ chống quét sai mã, không chống trùng.
  1 barcode có thể dùng cho 2 sản phẩm (bản EN / EU, thấy trong twist_sku_alias.json) → không đặt unique Barcode.
- `Toast_Product` / `Toast_Package` nối với WO bằng cột `ProductId` (= product_id[0] của WO = product.product id trên Viindoo, không đổi; cùng giá trị với `product_id` của `SVN_ProductionInputLogs` / `ProductMapping`). Default code của WO
  tách từ chuỗi "[mã] tên" của product_id. Mở phiên: `x_quantity_per_code` Viindoo phải > 0 **và** bằng Pcs / box Toast, không thì
  chặn (người dùng chốt 09/11/2026, mọi sản phẩm; WO thử không đối chiếu).
- **Mã quét pcs** (`Toast_Product.ScanSource`, 012_ScanSource): `Barcode` = mã cố định cột Barcode; `LotId` = Lot ID của WO (đơn bán,
  `lot_id` của `GetLabelDataAsync`), dùng cho Toast Pouch / Sling (barcode trên pcs = đúng chuỗi Lot, Toast tự in). Không xác định được
  Lot → chặn mở phiên. Mã đã xác nhận lưu ở `Toast_SessionConfirm.ScanCode`; quét so với mã của lần xác nhận gần nhất, không phân
  biệt hoa / thường. WO thử: Lot giả ở `Toast:TestWo:LotId`.
- Twist: trạng thái quét lưu localStorage; master data trong `twist_sku_alias.json` (đã lệch Viindoo);
  in lỗi vẫn báo thành công; tìm lệnh SX bằng `ilike` + `limit 1`; mark_done mỗi box →
  NM/MO/04963 có 280 backorder (trước 27/08/2026 `TwistController.SubmitProduction` gọi thẳng ViidooDBServiceAPI `InputProductionByWorkOrderv2` sau mỗi box: ilike tìm lệnh, write qty_producing, button_mark_done, tạo backorder; từ f0c5115 chỉ ghi log, serial trống nên job đồng bộ bỏ qua). Đã đọc Viindoo 05/11/2026: cả 279 lệnh done đều tạo + xong trong ngày
  27/08/2026 10:50–11:58 giờ VN (Viindoo lưu UTC, +7), mỗi lệnh 2 pcs, cách nhau ~10 giây, người tạo Administrator (lệnh -001 do ViindooBot);
  commit f0c5115 lúc 11:37 cùng ngày → đợt chạy thử của Twist rồi tắt. -280 còn mở 39441 pcs, tổng đã làm 559.
  Code hiện tại (HEAD d790a5e): màn Twist chỉ đọc Viindoo (`GetWorkOrder`); đủ box → in tem → `SubmitProduction` chỉ INSERT log
  (serial '', `Not synchronized`); cả 4 job đồng bộ (Synch… / ReSynch…) lọc `serial_code` khác rỗng nên không đẩy. Đã đọc Viindoo 05/11/2026:
  sau 27/08 nhóm "Twist - F/G" chỉ có 2 lệnh done (06228: 6912, 05235: 3456), đều do **Staff PD** nhập tay 1 lần đủ lệnh → Twist thực tế
  không đẩy KQSX (chatter 06/11/2026: 06228 và 05235 do Staff PD **tạo lệnh mới không gắn đơn bán** (origin trống), sửa SL
  (06228: 12960 → 6912) rồi done trong ~1 phút → nhập tay trên Viindoo; lệnh theo đơn bán vẫn mở; tài khoản user "Staff PD" gắn nhân viên
  PD Engineer). Nhóm "Injection Twist - F/G" (Small Backplate WIP07-…) có ~1600 lệnh done do Administrator, ~mỗi giờ, đến nay: luồng
  khác (không phải màn Twist), không tìm thấy trong repo SVN-Portal.
- Màn công nhân: WO tìm đúng tên (`=`, không ilike); không có lệnh đúng tên (tên gốc đã tách backorder) thì lấy **lệnh đang mở mới
  nhất của nhóm cùng tên** (`procurement_group_id`), không cảnh báo (bỏ 09/11/2026; màn In tem vẫn báo) (Twist lấy backorder gần nhất
  bằng ilike + mới nhất). Cả nhóm xong hết → lỗi "đã hoàn thành hết (n lệnh, mới nhất …)"; màn chỉ xem (allowClosed) lấy lệnh mới
  nhất. Dict WO có `requested_name` = tên đã nhập. Kiểm thật: NM/MO/04963 → -280; NM/MO/03975 → xong hết (261). Sai barcode: báo đỏ "Mã sai." (không nêu chi tiết), không tính,
  vẫn quét tiếp. Mỗi WO 1 box Open.
- Đuôi `-001`, `-002`… là **backorder**: mỗi lần báo xong 1 phần (mark_done), lệnh gốc đổi tên thành `-001` (done),
  phần còn lại sang lệnh mới `-002`… (đã đọc NM/MO/04963: `-001`..`-279` done, `-280` còn mở 39441 pcs; tìm
  `NM/MO/04963` bằng ilike trả về `-280`). Backorder do tài khoản API `sigma123` ("Administrator") tạo mỗi lần nhập kết quả qua
  ViidooDBServiceAPI (`ViindooProductionService`: qty_producing → button_mark_done → mrp.production.backorder → action_backorder);
  lệnh gốc do ViindooBot tạo lúc xác nhận đơn bán. Lệnh cũ đổi tên -00x và giảm product_qty về số đã làm, phần còn lại sang lệnh mới.
  Chạy thử: tự thêm máy in ở màn Máy in (VD 127.0.0.1:9100), PIN leader 1234.
- **3 loại mã, không lẫn**: Product ID (`924`, khoá số Viindoo) · **default code** = mã nội bộ Sigma (`FG-TRO-CE-0001-00`,
  `product.default_code`, = `product_code` của WO; cột `DefaultCode` ở `Toast_Product` / `Toast_LabelTemplate` /
  `Toast_Package`, giao diện ghi "Mã sản phẩm"; 007 đổi tên từ `PartNumber`) · **part number của khách** = mã Toast đặt, in lên tem
  (`HW0032`, `product.x_custcode` "Mã Khách Hàng"; chỉ 924 / 1309 có, không unique: 585 khách khác cũng `HW0151`; chưa API nào trả về).
  Model number (`TDC300`) không có trên Viindoo (tem Toast cũ SVN-Tools ghi cứng theo part number). Part number khách làm ở giai đoạn in
  (người dùng sẽ bảo): biến đã có giá trị trong mẫu thì dùng, chưa có thì tự điền.
- **Mỗi sản phẩm 1 mẫu tem**, tìm theo default code: `Toast_LabelTemplate.DefaultCode`
  = `Toast_Product.DefaultCode` + Type (Box) + Operation mặc định. Người dùng tự thêm mẫu cho mã mới, không cần nhân bản / dùng chung.
  (005_TemplateLink cho sản phẩm chọn mẫu dùng chung + biến riêng sản phẩm, đã chạy rồi bị bỏ; 006_RevertTemplateLink đảo lại.)
  Giá trị biến `{KEY}` lúc in (`LabelVariableResolver`): giá trị lưu trong mẫu >
  nguồn tự động ở `src/Toast.Web/Configs/label-variables.json` (giống SVN-Portal; `wo`, `product`, `package` = box, `system`;
  `Optional: true` = được để trống, in ra rỗng) > thiếu. Mẫu Box còn biến không có nguồn
  thì màn công nhân chặn quét. Thông tin WO lúc đóng box lấy từ `WorkOrderReader` (nhớ RAM 12h khi mở WO, hết thì đọc lại Viindoo).
- **PO / Lot** (đã đọc Viindoo 03/11/2026): không có trên WO (`lot_producing_id`, `x_Svn_customer_SN` trống). Nằm ở đơn bán:
  WO `origin` (lệnh con thì origin = lệnh cha, lần lên) → `sale.order.x_ERP_PO`, `x_LotID`: chuỗi nối dấu phẩy, mỗi giá trị ứng
  1 dòng `order_line` theo thứ tự (cùng sản phẩm có thể nhiều dòng / nhiều PO). ViidooDBServiceAPI có `GetSaleOrderByName`;
  Whoop (SVN-Tools `getPoNumber`) làm theo đường này. Đơn Toast hiện ghi PO `1`/`125`, Lot trống; Toast cũ (SVN-Tools) nhập tay.
  Đã làm (`ViindooRpc.GetLabelDataAsync`, gộp vào WO trong `WorkOrderReader`): chỉ hiển thị / tự điền, không lưu DB. 1 giá trị
  duy nhất = áp cho cả đơn; số giá trị ≠ số dòng → để trống + cảnh báo. Sản phẩm ở nhiều dòng với giá trị khác nhau →
  **đoán dòng theo số lượng** (Viindoo không lưu WO thuộc dòng nào: `stock.move.sale_line_id` của WO trống, move của các WO gộp
  thành 1 move trước khi chia cho dòng giao): mỗi dòng sinh 1 nhóm WO (`procurement_group_id`, lệnh gốc + backorder), tổng SL nhóm =
  SL dòng (đã kiểm S00589: 03973..03977 = 300/1800/3100/3000/600). Trùng SL → ghép theo thứ tự id nhóm (người dùng chốt 06/11/2026: sản phẩm + SL + id tăng dần là đủ, không cần chọn tay). Ghép được vẫn kèm cảnh báo
  "chọn theo số lượng"; không khớp → để trống. S00589 = Toast TG3 Pouch (1341), 5 dòng / 5 PO, Lot chung `0001-0-0626`;
  S00396: Lot `PVT`, PO `101`.
- **Tem Box Toast** (mẫu người dùng gửi 03/11/2026): biến `{PART_NUMBER}` (x_custcode, mẫu có giá trị thì dùng mẫu), `{PO}`, `{LOT_ID}`
  (đơn bán), `{QTY}` (= Pcs / box của Toast_Product, màn Sản phẩm điền từ x_quantity_per_code), `{MODEL}`, `{PART}` (mô tả tiếng Anh,
  không phải mã), `{DESC}` (mô tả tiếng Pháp) người dùng tự điền trong mẫu, trống thì báo thiếu. Mã vạch PO trong mẫu đó đang ghi cứng
  `>:PO>5123456` (đã báo người dùng). Chưa làm serial.
- **/toastv2** (`Features/StationV2`): màn nháp chỉ xem để đánh giá dữ liệu tem: nhập WO → dữ liệu Viindoo (WO, mã khách, đơn bán, PO,
  Lot, x_quantity_per_code), sản phẩm + mẫu tem Toast, bảng biến (giá trị, lấy từ đâu, THIẾU), xem trước tem. Không quét / in / ghi DB.
  Mở được cả WO đã hoàn thành / huỷ để thử (`allowClosed`, không nhớ vào RAM; /toast vẫn chặn).
  Khi đúng ý sẽ đưa cải tiến sang /toast. Mở thẳng được bằng `/toastv2?wo=...`.
- **Màn In tem** `/toast/admin/print`: `api/wo` đọc qua `WorkOrderReader.ReadAsync(allowClosed: true)` (có mã khách, đơn bán, PO, Lot +
  cảnh báo; WO đã xong / huỷ vẫn mở để in bù). Đổi WO / Điền lại: biến tự điền lấy lại theo WO mới, biến nhập tay giữ giá trị đã gõ.
- **Tra cứu đơn bán** `/toast/admin/sale-order` (`Features/SaleOrderLookup`, menu Tra cứu → Đơn bán, `ViindooRpc.GetSaleOrderOverviewAsync`):
  nhập `589` / `S00589` → dòng đơn (PO / Lot / deadline tách theo dòng), các nhóm WO có origin = đơn (gom theo procurement_group_id:
  số lệnh, tổng SL, lệnh đang mở / mới nhất) ghép với dòng theo SP + tổng SL (trùng SL theo thứ tự), nhóm không khớp liệt kê riêng.
  Lệnh con (origin = lệnh khác) không hiện. WO link sang /toastv2. Chỉ đọc.
- **Tra cứu lệnh sản xuất** `/toast/admin/work-order` (`Features/WorkOrderLookup`) và **sản phẩm Viindoo** `/toast/admin/viindoo-product`
  (`Features/ProductLookup`): danh sách lọc dự án / loại / trạng thái + chi tiết (WO: PO / Lot, nhóm backorder, NVL; sản phẩm: BoM,
  WO mở, đơn bán gần đây, Toast đã có quy cách / mẫu tem chưa). Phân loại `Features/Viindoo/ViindooCatalog.cs`: dự án + loại tách
  từ tên nhóm sản phẩm (`"Toast - F/G"`, `"Sandford- WIP"`, `"Nguyên vật liệu TOAST"`, `"Twist FG"`); khách + loại con (từ khoá
  trong tên SP, VD Toast Dock / Case / Sling / Pouch) ở mục `ViindooProjects` trong appsettings.json.
- **Khách hàng**: Viindoo không có khách cuối — 641 đơn bán (S00041..S00693) toàn công ty nội bộ (Sigma Worldwide Asia Limited 590,
  -I/C Entity- …). Nhóm sản phẩm có: Toast, Twist, Injection Twist, Walter (+ Inbox, WYW), Astro, Sakura, SPA, POP, Injection POP,
  Saddle, Sandford, Solar, ITA002, Sling × F/G / WIP / Raw MAT. Mã: `FG-<dự án>-<CE/SP>-…` (TRO = Toast, AST = Astro, RAI = Sakura,
  SOL = Solar, SF = Sandford); Twist / Walter dùng SKU khách làm mã (Walter `810114…` = mã Whoop ở SVN-Tools → đoán WHOOP).
  Đoán khác (chưa xác nhận): Sakura / SPA = Kobo, POP = Belkin, Saddle = Zebra.
- **Toast = client code "Trio"**. Bản vẽ tem chính thức (`~/work/SigmaDocument/RE__Trio_-_TG3_Pouch_Sling_-_New_packaging_design_/`,
  SVN-POUCH-030 / SVN-SLING-018, 20/08/2026): Pouch = "Toast Go 3 Holster / Étui" SKU **HW0151**, model **TGP300**; Sling SKU
  **HW0169**, model **TGS300**. **Quy tắc Lot ID**: EVT / DVT / PVT cho các đợt thử (vì vậy S00396 Lot = `PVT`); hàng MP dạng
  `XXXX-X-XXXX` = `<Mfg Lot 0001, tăng khi đổi quy trình>-<Rework 0/1>-<MMYY sản xuất>` (VD `0001-0-0626` = tháng 06/2026).
  Lệch: Viindoo ghi `x_custcode` HW0151 cho 1309 "FG DCT3 Dock" (theo bản vẽ HW0151 là Pouch) — chưa rõ bên nào sai.
- **WO thử** `Toast:TestWo` = `NM/MO/991144` (không có trên Viindoo), sản phẩm thử Viindoo product ID `9999` (phải tạo ở màn
  Sản phẩm + mẫu tem Box). Mở WO này không gọi Viindoo, quét / in tem như thật, không bao giờ ghi log dashboard.
- **Sigma-Dashboard** (`SVN-Portal/Sigma-Dashboard`, /dashboard) chỉ đọc `SVN_Production_result_Viindoo`
  (Operation × ngày × ca × Time1..5, ca ngày 08–20, đêm 20–08). Bảng này do SP trong DB ghi:
  `SVN_Sync_Production_By_Hour` lấy lệnh done trên Viindoo (`SVN_mrp_production_1`, +7h) cho mọi Operation
  **trừ Toast%/Astro%/POP**; `SVN_Sync_Production_By_Hour_1s` lấy **`SVN_ProductionInputLogs`** (không lọc status/serial,
  theo `date_finished`) cho **Toast%/Astro%/Twist%**, chỉ ca hiện tại, được SVN-Portal gọi khi mở màn biểu đồ.
  Cần: Operation có target trong `SVN_Target` + `ProductMapping` (product_id → Operation). Twist ghi 1 dòng log / box
  (serial trống → job đồng bộ Viindoo bỏ qua); từ commit f0c5115 (27/08/2026) Twist không gửi KQSX lên Viindoo nữa.
  Cơ chế hiển thị (đọc code 06/11/2026, `Sigma-Dashboard/Services/Helpers/HomeControllerHelper.SummaryData`, trang tự gọi
  `GetFullDashboardData` mỗi 10 giây, số card đổi thì reload): Operation hiện khi đủ 3 điều kiện: (1) có trong JSON `SVN_AppSetting_v1`
  nhóm `OperInfo` (lọc theo company: hậu tố (SM)/(ITA)/(BT)); (2) SP `SVN_Pro_CalTarget_Viindoo(@date_time, @shift)` trả target cho
  ngày + ca (target ngày: màn /DailyTarget → `SVN_target`); (3) bảng `SVN_Production_result_Viindoo` có dòng `Type_value = 'Target'`
  ngày đó với Time1..5 khác 0 (target giờ: màn /HourlyTarget, tạo kèm dòng `Production Qty` + `NG_Qty` = 0). Line = cột Product của
  dòng NG_Qty. Ca: `Hour >= 20` = Night; khung giờ ca ở `SVN_AppSetting_v1` nhóm `SectionConfig`. Sigma-Dashboard **không** gọi SP đồng bộ:
  `SVN_Sync_Production_By_Hour_1s` (log → Production Qty) chỉ được SVN-Portal `HomeController.ProductionResultV2` /
  `GetDataByOperAndWCMainDashBoardV1` gọi; `SVN_Sync_Production_By_Hour` không có ai gọi trong repo (đoán là job SQL, chưa kiểm).
- Log Toast hiện có (đọc 02/11/2026): `ProductMapping` có 8 sản phẩm Toast (924, 925, 951, 952, 1309, 1310, 1312, 1313),
  mỗi sản phẩm 1 Operation `Toast-<mã>`. NM/MO/06229 (951) có 2–6 dòng log/ngày (20–520 pcs), serial trống,
  `Not synchronized`, product_type `none`, total_qty = SL WO, component_list `[]`, API_function `localhost:8026/.../
  InputProductionByWorkOrderv2`, wo_code `-003`..`-022` tự đánh số (Viindoo vẫn chưa tách lệnh) → khớp màn SVN-Portal
  "Nhập kết quả sản xuất" `/Production` (`ProductionController.InputProductionResult`, isInputToViindoo=false): có người nhập
  số lượng theo đợt, leader chỉ sửa/xoá dòng cho khớp thực tế. Toast ghi log thì phải ngừng nhập Toast ở màn đó (tránh cộng đôi).
- SVN-Tools (`~/work/SigmaDocument/SVN-Tools`) đã có hệ thống Toast cũ: `SVN_Toast_Serial_Info` (serial từng pcs, FCT/FQC),
  `SVN_Astro_Label_Data` (EmployeeID 'toast', package/pallet), SP `SP_Toast_*`. Chưa rõ quan hệ với dự án này.
- **Toast ghi log dashboard**: `Toast_Package` (kiện đóng gói, hiện là box; màn công nhân / API vẫn gọi "box") đóng xong thì
  ghi 1 dòng `SVN_ProductionInputLogs` giống Twist (product_qty = pcs, date_finished = giờ đóng, serial `''`, product_type `none`,
  state `Used`, status `Toast`), lưu id vào `InputLogId`. Công tắc `Toast:WriteDashboardLog` (mặc định tắt, để chạy song song).
  Sửa / xoá kiện **đồng bộ sang log** (06/11/2026, `PackageService.SyncDashboardLogAsync`, cùng 1 lần lưu): sửa số pcs → sửa
  product_qty; xoá kiện → **xoá hẳn** dòng log; admin mở lại kiện (Open) → xoá log, đóng lại → ghi dòng mới. wo_code = lệnh thật,
  master_wo_code = nhóm lệnh (WoGroup). Kiện đóng lúc công tắc tắt (InputLogId trống) không có log để sửa.
  Đẩy KQSX Viindoo tách riêng, làm sau (người dùng bấm tay hoặc chạy theo lịch, hướng đối soát Toast − Viindoo, thừa thì unbuild). Không có chức năng thêm kiện bằng tay.
  Bảng log map bằng `ExcludeFromMigrations` (migration không đụng); SQLite chạy thử tạo bảng giả trong `DevSqlite`.
- **Phiên làm việc** (màn /toast, `Features/Station/SessionService.cs`, bảng `Toast_Session` + `Toast_SessionConfirm`, 008_Session):
  nhập WO → modal mở phiên (`/toast/api/session/check`): danh sách kiểm tra ok / warn / error / info (lệnh mở, **BoM** = `bom_id` của WO,
  quy cách, barcode pcs, mẫu tem Box, biến tem không Optional phải có giá trị; biến package / system không kiểm) + giá trị tem → tick (mặc định đã tick, 06/11/2026) +
  máy in + mã NV `SVN` + 4 số (không PIN) → `session/start`. Mỗi **lệnh (backorder)** 1 phiên Open, khoá theo `ViindooMoId` (id lệnh,
  WO thử theo tên); người dùng chốt 09/11/2026: -001 / -002 là 2 phiên (thực tế lệnh chỉ tách khi chính máy đang làm đẩy KQSX). Mở lệnh mới
  của nhóm mà phiên lệnh trước còn mở → cảnh báo trong modal, vào làm thì tự kết thúc phiên đó (EndedBy "Tự động: …"). Box dở vẫn theo
  **nhóm lệnh** (011_SessionGroup: `Toast_Session.WoGroup` / `Toast_Package.WoGroup` = tên nhóm `procurement_group_id`, WO thử bỏ đuôi -00x):
  mở phiên lệnh mới thì xét **box mới nhất của nhóm** (số box lớn nhất): Open và Qty < pcs/box → chuyển sang lệnh hiện tại quét tiếp
  (kể cả box đã đóng rồi bị mở lại, người dùng chấp nhận); đã đóng hoặc đủ / vượt → để nguyên ở lệnh trước, lệnh mới mở box mới. Số box liên
  tục theo nhóm. Màn Đóng gói: mở / đóng box tự do, chỉ chặn 2 box Open cùng 1 lệnh (trạm quét vào box Open của lệnh). Sau này
  đẩy KQSX chỉ tính box đã đóng, Toast đổi tên lệnh trong phiên / box thành -00x cho khớp Viindoo (chưa làm, bàn sau). 2 line chạy 2 WO
  cùng sản phẩm = 2 nhóm → phiên / box dở riêng; refresh / đổi máy
  thì vào lại phiên cũ nhưng phải xác nhận lại (mỗi lần 1 dòng SessionConfirm: mã NV, máy in, giá trị tem JSON, cảnh báo). Quét chỉ
  được khi WO có phiên Open. Box đóng ghi **`SessionId`** (phiên đóng box; 014_PackageSession thay `SessionConfirmId`, người dùng chốt
  09/11/2026: chỉ cần biết box thuộc phiên nào, **không cần in lại giống hệt**); tem (in tự động + in lại) = mẫu tem hiện tại + giá trị
  của lần xác nhận **mới nhất của phiên đó** thắng giá trị đọc lại, ngày giờ = giờ đóng box (không lưu ZPL).
  **Menu F2** (hoặc nút Menu, luôn hiện; thay mọi nút cũ trên màn) khoá PIN leader (`/toast/api/pin`), PIN đúng thì 2 phút sau khi
  đóng menu không hỏi lại. PIN = `Toast:LeaderPin`; `Toast:RequireLeaderPin` = false thì không hỏi PIN, API bỏ kiểm (mặc định true; server nhận ngay, màn /toast phải tải lại). Ô (bấm số theo thứ tự đang hiện): Máy in · **In tem** · (chỉ trong phiên) Box (chọn box → In lại
  `box/{id}/reprint` · Đóng box thiếu · Sửa số pcs · Xoá, bấm 2 lần). **Không còn ô Kết thúc** (11/11/2026; API `session/{id}/end` vẫn còn):
  phiên **tự kết thúc** khi không hoạt động `Toast:AutoEndInactiveHours` giờ (0 = tắt; `SessionAutoEndService` chạy nền mỗi phút,
  `SessionService.EndInactiveAsync`, cách tính = cột Inactive admin; lần lượt từng phiên, box dở giữ lại, nhập KQSX nếu bật; EndedBy
  "Tự động: không hoạt động 4 giờ" / "… 6 phút" khi 0.1; giá trị thập phân được) hoặc admin bấm Kết thúc. IIS phải chạy liên tục (app pool AlwaysRunning + Idle Time-out 0, site Preload
  Enabled, cài Application Initialization), không thì app tắt khi không ai truy cập và tác vụ dừng. Quét khi không có phiên mở → `needSession`,
  lần quét không tính, trạm mở lại modal xác nhận theo tên nhóm lệnh (`wo.group`). Người dùng chọn chỉ dùng inactive, chưa làm giờ chốt cố định
  (để sau, cho ca đêm).
  **Đếm ở trình duyệt, gửi theo lô** (11/11/2026, 015_SessionDevice): trạm tự so mã (mã quét của phiên) và đếm ngay, pcs nằm trong hàng chờ
  localStorage `toast.station.outbox:<nhóm lệnh>` ({group, productId, barcode, fromSeq, toSeq}; số thứ tự `toast.station.seq:<nhóm>`); **gửi khi đủ box** hoặc khi còn ≥ `Toast:ScanSyncEvery` pcs chưa gửi (0 = chỉ khi đủ box; trạm nhận khi tải lại trang) + trước Menu → Box +
  khi vào lại phiên mà hàng chờ đã đủ box. `scan` nhận lô (`count`, `fromSeq`/`toSeq`, `deviceId`): đóng + in từng box đủ (lô lúc mất mạng có
  thể nhiều box, in bù), bỏ phần số thứ tự ≤ `Toast_Session.LastScanSeq` (không cộng đôi). **Máy giữ phiên** `Toast_Session.DeviceId` (mã ngẫu nhiên
  `toast.station.device`): máy xác nhận vào làm thì giữ phiên (cùng máy mở phiên mới của nhóm thì chép LastScanSeq từ phiên trước); lô của máy
  khác → `otherDevice`, trạm bỏ hàng chờ (không cộng thừa). Gửi lỗi → giữ hàng chờ, quét tiếp, thử lại mỗi 5 giây khi đủ box. Modal mở phiên báo
  "phiên đang ở máy khác". 1 WO chỉ làm ở 1 tab trên 1 máy (`navigator.locks` theo nhóm lệnh, khoá lúc bấm Vào làm việc, nhả khi rời màn quét); tab khác làm WO khác được. Không gửi seq / deviceId (test) = 1 lần quét như cũ. Server chỉ thấy hoạt động khi
  đủ box (Inactive tính từ box đóng gần nhất).
  **In khi mất mạng** (11/11/2026): icon mạng trên trạm (chỉ icon, chữ ở title; `/toast/api/ping` mỗi 10 giây = gọi được server **và** server chạy được `SELECT 1` trên DB trong 3 giây (kết nối ADO riêng từ `GetConnectionString()`, không qua EF để mất DB không in lỗi EF mỗi lần ping; đang mất mạng thì ping 30 giây / lần, sự kiện `online` của trình duyệt thì ping ngay), vì máy dev chạy server localhost + DB công ty qua mạng; API trả 5xx / không gọi được = mất mạng; gửi lô chờ 10 giây; lỗi in khi mất mạng (agent chưa chạy / máy in không phản hồi) không báo đỏ: mỗi box đủ hàng chưa in được có 1 cảnh báo cam "Box N đủ hàng, chưa in được tem / Lý do: Mất kết nối server, agent … / Sẽ in khi có kết nối lại" (13/11/2026, người dùng chốt; lỗi gốc ở tooltip dòng trạng thái, log 1 lần / box + lỗi); đã biết mất mạng thì gửi lô không gọi server, chỉ in qua agent, có mạng lại mới gửi; `api.network` / `sync.fail` chỉ ghi log lần lỗi đầu / lỗi khác nội dung). Trạm lưu mẫu tem Box của
  phiên ở localStorage `toast.station.label:<nhóm>` (`label/offline`: ZPL đã điền biến cố định + giá trị đã xác nhận, còn biến package / system now;
  tải lại khi `template.version` (= Id-UpdatedAt) hoặc lần xác nhận đổi). Mất mạng: công nhân tự đổi máy in sang USB / TCP qua Node ở Menu → Máy in
  (người dùng chọn đổi máy in, không có máy dự phòng riêng; mất mạng thì Menu không hỏi PIN, chỉ có ô Máy in + **Box**). **Box chưa đồng bộ** (13/11/2026, người dùng cho toàn quyền):
  mất kết nối thì mỗi box đủ hàng thành bản ghi trạm giữ trong hàng chờ (`outbox.boxes`: boxNo, qty, endSeq, closedAt, printed; `keepBoxes`,
  `runModel`), `outbox.adj` = pcs thêm / bớt khi sửa. Menu → Box: box chưa đồng bộ (id âm) **đủ thao tác như bình thường** (đang quét, luôn hiện khi mất kết nối:
  Đóng box thiếu / Sửa số / Xoá; đã đóng: In lại tem (chưa in thì in ngay và ghi đã in) / Sửa số / Xoá) ngay trên máy (`qtyLocal` / `deleteLocal` / `closeLocal` / `reprintLocal`; xoá thì các box sau lùi 1 số, box đã in đổi số / sửa số thì tự in lại;
  đóng thiếu lấy thêm 1 số thứ tự ảo để có EndSeq mới); box đã đồng bộ khoá "cần kết nối server". Gửi khi có kết nối: có boxes / adj thì
  `scan` kèm **`boxes` + `openQty`** (`StationApiController.ScanBoxes`: đóng từng box đúng Qty theo thứ tự, Printed thì `CloseOfflineAsync` không in
  lại, chưa in thì in bù; box đang quét = OpenQty tuyệt đối; toSeq <= LastScanSeq thì bỏ cả lô, box EndSeq <= đã nhận bị bỏ → gửi lại không cộng đôi).
  Không có boxes / adj thì gửi như cũ (server chia theo quy cách); box đủ in qua agent, ngày giờ theo
  máy trạm (lệch > 5 phút cảnh báo, > 1 giờ không in), ghi `offline` vào hàng chờ (số box, giờ, máy, version, endSeq). Có mạng lại: lô kèm
  `offlineBoxes` → `PackageService.CloseOfflineAsync` đóng box theo giờ đó, không in lại, PrintTarget "In khi mất mạng (offline) · …", lệch số box /
  mẫu cũ thì thêm "⚠" (trạm hiện badge Cần kiểm tra). Máy in **Server TCP** khi mất kết nối server: trạm **tự in qua agent** (`/print`) tới đúng
  IP:port máy in đó (13/11/2026, người dùng chốt; không cần đổi cấu hình, máy trạm phải chạy agent; agent lỗi thì báo + thử lại 5 giây).
  **Log lỗi trạm** (11/11/2026, để debug máy công nhân): `station.js` `log(level, event, message, data)` → console `[Toast] …` + 300 dòng gần nhất
  ở localStorage `toast.station.log` (F12 gõ `toastLog()` / `toastLog.download()` / `toastLog.clear()`) + gửi về `/toast/api/client-log` (lô 50, khi có
  mạng / sau ping thành công) → `ClientLogService` ghi `{Toast:ClientLogDir, mặc định logs}/client-yyyyMMdd.log` (giữ 30 ngày, gitignore; IIS cần
  quyền ghi; tương đối = từ thư mục bản build, chuẩn hoá bằng GetFullPath. Server deploy mỗi lần 1 thư mục `C:\inetpub\wwwroot\SigmaToast\<ddMMyy-HHmm>\publish`
  → **mặc định Windows `C:\ProgramData\SigmaToast\logs`** (11/11/2026, cùng chỗ khoá Data Protection: app pool tự tạo được, không cần icacls,
  các bản deploy dùng chung; dev Linux `src/Toast.Web/logs`; `ClientLogDir` vẫn ghi đè được); không để trong wwwroot của app (bị tải được qua web); lỗi ghi file gần nhất hiện ở màn Log trạm; ghi lỗi thì `client-log` trả 503,
  trạm giữ log gửi lại ở lần ping sau. Server thật 11/11/2026 bị `Access denied` vì app pool không có quyền ghi trong `C:\inetpub\wwwroot` → cấp
  Modify cho `IIS AppPool\<pool>` vào thư mục log, nên dùng `..\\..\\logs` + quyền chỉ cho `SigmaToast\logs`). Ghi: lỗi JS / promise, mọi thông báo đỏ (alertError, notyf.error), API mất mạng / 5xx / HTTP lỗi, đổi trạng thái mạng, gửi lô lỗi /
  máy khác / phiên kết thúc, in qua agent / in khi mất mạng lỗi, quét sai mã (warn kèm mã đã quét).
  Màn **Log trạm** `/toast/admin/client-log` (`Features/ClientLogAdmin`, menu Vận hành, chỉ đọc file, không bảng DB): chọn ngày (mặc định ngày
  mới nhất có file), tối đa 5000 dòng mới nhất, lọc mức + tìm ở trình duyệt (ListTable, Tải lại = xoá lọc), bấm dòng xem chi tiết + JSON, Tải file.
  **Card Thông báo** (13/11/2026, thay khung đỏ đầu trang + toast; design người dùng chốt: claude.ai/artifact/T6dvVskEGj5yayDMJxTtY8): chỉ ở màn làm
  việc, **bên phải Box gần nhất** (col-xl-7 / 5, màn rộng cao bằng card box, tối thiểu 32rem, absolute trong cột). `notice(type, msg)` trong `station.js`: ô **mới nhất
  to** (nền đặc đỏ / xanh / cam theo loại, chữ 1,75rem, `×n` khi trùng liên tiếp) + danh sách **"Trước đó"** cuộn trong card; trống = "Chưa có thông
  báo"; danh sách chỉ giờ:phút, ô mới nhất có giây. Chỉ trong trang (không lưu, không nút xoá), tối đa 50. `alertError` và mọi `notyf.*` trên màn trạm đổ vào đây (đang mở modal thì thêm toast).
  **Bước nhập WO** (chưa vào màn làm việc): lỗi / cảnh báo hiện dòng đỏ dưới ô WO + viền đỏ (`woError`), gõ lại thì mất; rời phiên (máy khác giữ,
  phiên kết thúc) gọi `leaveRun()` trước rồi mới báo để lý do hiện ở dòng này. Hàng Lệnh sản xuất / tiến trình giữ nguyên.
  **Câu thông báo in tem** (13/11/2026, người dùng duyệt): 1 chỗ `printMsg` + `PRINT_HINT` trong `station.js`. Thông báo **nhiều dòng** (`\n`):
  dòng 1 = việc gì (to, đậm), mỗi ý 1 dòng `Nhãn: nội dung` (Lý do / Xử lý / Máy in / Mã; `lines()`, `noticeHtml()`), vì card hẹp; toast và dòng lỗi
  dưới ô WO nối các ý bằng " · " (`oneLine`). VD "Box 12 chưa in được tem / Lý do: … / Xử lý: …". `PRINT_HINT` = dòng Xử lý (người dùng sẽ bổ sung VD
  liên hệ trưởng line / bộ phận liên quan). Áp cho in tự động, in khi mất mạng, In lại (Menu → Box), In tem thủ công. Lý do vẫn là lỗi gốc (chuẩn hoá sau).
  **Mất kết nối server** (13/11/2026, người dùng chốt; server sau này lên cloud nên server sập / mạng hỏng đều phải làm tiếp được, không tải lại):
  dòng trạng thái `st-sync` (dưới thanh tiến trình, vẽ lại mỗi lần quét, không phải thông báo) dùng "Mất kết nối server. …" + "Không đóng tab, không tải
  lại trang." (server sập thì trang không mở lại được); box đủ mà không in được ghi cả khoảng "Box 4–7 (4 box)". Card: vừa mất ("Mất kết nối tới server
  Toast / Vẫn quét và in tem bình thường… / Xử lý: Không đóng tab…"), có lại ("Đã kết nối lại server"), mỗi box ghi nhận lên server sau khi có lại,
  mỗi box đủ mà chưa in được (chỉ còn khi chưa chọn máy in; `net.waitNoticedBox`). Chỉ mất Internet mà server + DB trong LAN: Toast không coi là mất
  kết nối (chỉ Viindoo lỗi khi mở WO mới). **Box gần nhất** hiện cả box đủ hàng ở máy này chưa đồng bộ (`localBoxes`, đầu bảng, cột # trống,
  nền cam, badge "Chưa đồng bộ", In tem "Đã in (mất kết nối)" / "Chưa in", tiêu đề đếm "n box chưa đồng bộ"; vẽ lại sau mỗi lần quét). Trình duyệt
  không gửi TCP thẳng tới máy in được: server tắt thì chỉ in được qua agent trên máy trạm (HTTP `/pstprnt` của Zebra là phương án phụ, chưa làm). Còn pcs / box chưa gửi (`outbox`) thì `beforeunload` bắt trình duyệt hỏi khi đóng tab / tải lại
  (Chrome / Edge chỉ hiện câu chung, không đặt chữ riêng được).
  **Máy in** = modal "Cấu hình máy in" dùng chung với màn In tem (`Shared/_PrinterConfigModal.cshtml` + `js/shared/printer-config.js`,
  localStorage `toast.print.config`, cùng máy dùng chung 1 cấu hình): Server TCP / TCP qua Node / USB qua Node, chọn danh sách hoặc IP tay.
  Mọi API in của /toast nhận `printer` = cấu hình đó (`PrintTarget`, `PrinterLookup.ResolveAsync`). Qua Node: server điền biến, trả
  `printed.agent` (path /print | /print-usb + ZPL), trình duyệt gửi agent rồi `box/{id}/print-result`; box giữ PrintError "chưa nhận
  được kết quả" đến khi báo. `PrintTarget` (009) ghi mô tả máy in vào Toast_Package / Toast_SessionConfirm (PrinterId chỉ khi máy trong danh sách). Chưa làm: nhập KQSX
  Viindoo lúc kết thúc phiên. Giao diện tối giản, làm từng bước theo yêu cầu.
- **Nhân viên** (đọc Viindoo 05/11/2026): `hr.employee` không có field mã; mã SVN nằm ở **Thẻ** (`category_ids` →
  `hr.employee.category.name`, VD `SVN0902`). 160 nhân viên, 129 mã hợp lệ; thẻ sai dạng (`061`, `SVN 00892`, `2106072`, `HR Staff`) bỏ,
  `SVN 0886` bỏ dấu cách; mã trùng nhiều bản ghi (SVN0892 ×3, SVN0428 ×3, SVN0708 ×2) → bản ghi `__last_update` mới nhất, bằng nhau id nhỏ
  nhất. `work_phone` hầu hết là tổng đài `0225.366.8818`. `ViindooRpc.GetEmployeesAsync` + `Features/Station/EmployeeDirectory.cs`
  (RAM 1 giờ, lỗi → rỗng, thử lại sau 1 phút). Modal xác nhận: `SVN` cố định + ô chỉ nhập 4 số (không Select2,
  không gửi danh sách mã xuống trình duyệt); bấm Vào làm việc, server kiểm tra mã, không có thì "Nhân viên không tồn tại." + viền đỏ ô mã
  (`field: "employee"`; không đọc được danh sách Viindoo thì cho qua).
  `Toast_SessionConfirm.EmployeeName` / `EmployeePhone` (010_EmployeeInfo) lưu lúc xác nhận. Tên / điện thoại chỉ hiện ở màn admin (chưa làm).
- **Màn Phiên làm việc** `/toast/admin/session` (`Features/SessionAdmin`, menu Vận hành): nút **Kết thúc phiên** trong modal (phiên Open,
  `api/{id}/end`, EndedBy "Admin <tài khoản>", box dở giữ lại); cột **Inactive** (phiên Open: giờ hiện tại − max(UpdatedAt / ClosedAt /
  CreatedAt box mới nhất của lệnh, lần xác nhận cuối, StartedAt); ≥ 1 giờ tô vàng). Danh sách 500 phiên
  mới nhất + mọi phiên đang mở, tìm (WO / mã SP / mã hoặc tên NV) + lọc trạng thái ở trình duyệt; modal chi tiết: thông tin phiên, các lần
  xác nhận (mã NV, tên, **điện thoại đầy đủ**, máy in; bấm dòng xem giá trị tem + cảnh báo), box (đã đóng theo SessionId + box dở của WO
  khi phiên mở). Màn danh sách có lọc luôn có nút Tải lại = tải lại + xoá tìm / lọc (như Mẫu tem). Phân trang + sắp xếp
  dùng chung `wwwroot/js/shared/list-table.js` (`ListTable`) + `Shared/_ListFooter.cshtml` + class `.list-sort` (site.css): Đóng gói, Máy in,
  Phiên làm việc (Mẫu tem có bản riêng). Đóng gói tải 2000 box mới nhất, ô tìm gõ là tìm ở server.
  **Bấm 1 lần vào dòng** (`wwwroot/js/shared/row-click.js`, `ToastRowClick.button(tbody, nút)`): bấm dòng = bấm nút đó trong dòng, bôi đen /
  double-click thì không mở; nút ở cột thao tác **không tên** (bảng `table-bordered`) để Vimium bấm được. Xem: Tra cứu WO / sản phẩm, Phiên làm việc,
  Mẫu tem (Xem tem); Sửa: Máy in, Sản phẩm, Đóng gói.
- **Hệ thống làm cho Toast Pouch / Sling** (người dùng đính chính 06/11/2026; không phải Case): in tem **box** + **plate**. Đọc Viindoo
  06/11/2026: **1317 `FG-TRO-CE-0006-00` FG TG3 SLING** (x_quantity_per_code **30**) và **1341 `FG-TRO-CE-0007-00` FG TG3 POUCH** (**10**):
  tracking `none`, BoM `flexible` (271 / 274), không công đoạn, NVL đều `none` (Sling 5 dòng: 1,01 / 1,01 / 1,01 / 1,02 / Masterpack 0,033633;
  Pouch 3 dòng × 1,0), x_custcode trống. Lệnh mở: S00694 06384–06386 (Sling 100 / 200 / 300), 06387–06389 (Pouch 100 / 200 / 300).
  **Đã từng đẩy KQSX tự động**: 30/07–18/08/2026 ~300 lệnh done (nhóm 03815, 03976, 03977), mỗi lần đúng **1 box** (10 Pouch / 30 Sling),
  Administrator, cách ~13 phút → màn SVN-Portal **Tools → "Lệnh sản xuất" / "Lệnh sản xuất FG Pouch"** (`ToolsController.InputProductionResult`
  → ViidooDBServiceAPI `InputProductionByWorkOrderv2` ngay khi bấm, không ghi log). Sau 18/08 không còn (vài lệnh cuối Staff PD).
- **Sản phẩm Toast khác trên Viindoo** (đọc 05/11/2026): **Case 951 / 952** (FG-TRO-CE-0002/0003): tracking `none`, BoM `flexible`,
  không công đoạn, 7–9 NVL đều `none` (tỉ lệ / pcs: 1 cho hộp / insert / case / tem, Masterpack 0,2, Pallet 0,001, Corner 0,004, Pallet Insert 0,002),
  move NVL đa số `waiting` (chưa giữ hàng). Lệnh mở: 06229 (951, 10000), 06230 (952, 3000), 05143 (952, 2000), 05140-003 (952, 460).
  **Dock 924 / 1309 / 1312** (FG-TRO-CE-0001/0005/0004): tracking `serial`, NVL serial (WSS03-… assembly + charger REL10-…, 2 REL10 đã lưu trữ
  nhưng còn trong BoM 267 / 269), 924 có công đoạn; 370 lệnh 924 done mỗi lệnh 1 pcs (03252-3207…, Administrator) = đẩy theo serial.
  **WIP 925 / 1310 / 1313**: serial, BoM `warning`. `x_quantity_per_code` của cả 8 sản phẩm = 0. Toast quét barcode chung theo SKU nên
  chỉ Case đẩy được không cần serial. Toast tự ghi Viindoo (không qua ViidooDBServiceAPI), theo id lệnh.
- **Nhập KQSX lên Viindoo** (09/11/2026, `Features/ProductionPush`, `ViindooRpc.Production.cs`, 013_ProductionPush): bật bằng **`Toast:PushProduction`**
  trong appsettings (mặc định tắt, sửa file là app tự nhận; người dùng chọn appsettings thay vì bảng cài đặt 09/11/2026), màn Phiên làm việc chỉ hiện Bật / Tắt. Kết thúc phiên (trạm / admin) + bật → nhập theo **id lệnh**: số =
  tổng pcs box **đã đóng** của lệnh (`Package.WoName`), tối đa SL còn lại (thừa không nhập, ghi "Thừa n pcs"); box dở không tính.
  Gọi: write `qty_producing` + `move_raw_ids` quantity_done = số × `unit_factor` (tỉ lệ BoM, làm tròn lên theo `uom.rounding`, PCS 0,0001)
  → `button_mark_done` 1 lần với context `skip_consumption`, `skip_backorder`, `mo_ids_to_backorder` = [lệnh] khi còn thiếu ([] khi đủ);
  Viindoo vẫn trả wizard thì tạo + xác nhận như SVN-Portal cũ → đọc lại phải `done`. Lệnh huỷ: lỗi. Lệnh **đã done** khi nhập: không ghi, **đối chiếu** với PushDetail lần trước:
  `qty_produced` khớp + `date_finished` (UTC → giờ máy) lệch ≤ 2 phút so với **lần Toast gửi** `button_mark_done` (`PushDetail.lastSent`, kể cả
  lỗi; giữ qua các lần thử không gửi gì) **hoặc giờ kết thúc phiên** (`EndedAt`, không bị ghi đè; số = tổng pcs box đã đóng) → Done "xác nhận lại từ
  Viindoo" + đổi tên (phiên 06387 bị Lệch vì lần thử bằng bản cũ đã ghi đè PushDetail → thêm 2 bằng chứng này). Mismatch vẫn bấm Đẩy KQSX được (chỉ đối chiếu lại); không khớp → **Mismatch "Lệch"** (ghi người sửa cuối `write_uid`, giờ, số), Toast không tự xử lý. Kết quả
  `button_mark_done` khi tách backorder trên Viindoo thật = action mở lệnh mới (`res_model` mrp.production), không phải wizard (06387, 09/11/2026: -001 done 10, -002 90);
  tracking serial / lot, WO thử: không nhập. Thành công: phiên + box đã đóng đổi tên theo Viindoo (-001). Kết quả trên `Toast_Session`
  (PushStatus Done / Error / Off / Skipped, PushQty, PushedAt, PushMessage; `PushDetail` JSON lần nhập gần nhất kể cả lỗi:
  lệnh trước / sau, toastQty / pushQty / excess, backorder, tiêu hao từng NVL, các bước read / write / mark_done / wizard / verify + lỗi;
  không lưu danh sách box, người dùng chốt 09/11/2026, cần thì thêm sau; modal chi tiết phiên có phần "Chi tiết KQSX"); Off / Error có nút **Đẩy KQSX** ở admin (cần `Toast:PushProduction` bật).
  SVN-Portal cũ (`ViindooProductionService`) ghi quantity_done NVL = số thành phẩm (sai với Masterpack 0,033633). Đọc 06384 (09/11/2026):
  BoM 271 `flexible`, NVL `waiting` (chưa giữ hàng, tồn âm), không công đoạn. Lệnh thử người dùng tạo: **S00694** (06384–06386 Sling 100 / 200 /
  300, 06387–06389 Pouch 100 / 200 / 300) — chỉ người dùng tự bật / bấm; Claude không tự ghi Viindoo.
- **Menu → In tem** ở trạm (09/11/2026, `Features/Station/StationLabelService.cs`, API `label/wo` · `label/templates` · `label/context` ·
  `label/preview` · `label/print`, thay In thủ công): **giống phần 1, 2 của màn In tem admin** (người dùng chốt). 1. WO (không bắt buộc, chỉ
  để điền dữ liệu): Tìm → thông tin WO + nhãn trong / ngoài phiên; phần 2 chỉ hiện **mẫu tem của sản phẩm** (tự chọn mẫu đang làm / Box / đầu),
  nút "Tìm mẫu khác" mở ô tìm; X = bỏ WO. Không có WO: chỉ hiện **kết quả tìm theo mã** (tối đa 50, ô trống không trả gì, không liệt kê hết).
  3. Bảng Biến · Giá trị · Lấy từ. Trong phiên: mở modal tự tìm WO đang làm, giá trị theo WO = lần xác nhận mới nhất của phiên; ngoài phiên đọc
  mới Viindoo (cả lệnh đã xong / huỷ). Mẫu Plate / Carton: QTY = box / plate × pcs / box (`spec`). Biến không có nguồn nhập tay. Giá trị **khoá sẵn** (chỉ đọc), bấm ổ khoá
  cuối dòng để mở sửa (11/11/2026; biến bắt buộc đang trống mở sẵn; đổi WO / mẫu thì khoá lại), ô gõ tay giữ khi đổi mẫu / WO, thiếu thì không in. Không chọn box (in lại box ở Menu → Box). Chưa lưu lịch sử in.
- Yêu cầu đã nêu: mở lệnh có modal checklist
  xác nhận giá trị và lưu log; đẩy kết quả lên Viindoo lúc **kết thúc phiên** (04/11/2026 thay cho "gom theo ca"), không theo từng box.
