using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Sigma_Dashboard.Models;
using Sigma_Dashboard.Services.Configurations;
using Sigma_Dashboard.Services.Helpers;

namespace Sigma_Dashboard.Controllers
{
    public class HourlyTargetController : Controller
    {
        AppConfig appConfig;
        HourlyTargetControllerHelper controllerHelper;
        public HourlyTargetController(AppConfig appConfig, HourlyTargetControllerHelper controllerHelper)
        {
            this.appConfig = appConfig;
            this.controllerHelper = controllerHelper;
        }
        public async Task<IActionResult> Index(DateTime date, string shift = "Day", string companyCode = "SVN", bool isManualLoad = false)
        {
            if (string.IsNullOrWhiteSpace(companyCode))
            {
                companyCode = appConfig.DefaultCompany;
            }
            if (date == DateTime.MinValue)
            {
                date = DateTime.Now;
            }

            if (!isManualLoad)
            {
                if (date.Hour >= 20)
                {
                    shift = "Night";
                }
                else
                {
                    shift = "Day";
                }
            }
            ViewBag.date = date;
            ViewBag.stringDate = date.ToString("yyyyMMdd");
            ViewBag.shift = shift;
            ViewBag.ControllerName = "HourlyTarget";
            ViewBag.IndexPage = "Index";
            ViewBag.TitleName = $"Hourly Target - {date.ToString("dd/MM/yyyy")}";
            ViewBag.CompanyCode = companyCode;
            ViewBag.AccessMode = "PMC";
            List<HourlyTargetViewModel> viewModels = new List<HourlyTargetViewModel>();
            try
            {
                viewModels = await controllerHelper.GetHourlyTargetData(date, shift, companyCode);
            }
            catch
            {

            }
            return View(viewModels);
        }

        /// <summary>
        /// 1. XỬ LÝ THÊM MỚI ĐƠN LẺ (CREATE)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create(HourlyTargetViewModel model)
        {
            try
            {
                // Đồng bộ ép các trường không có trong bảng về giá trị an toàn/mặc định dưới DB
                model.Forecast = 0;
                model.WORunning = "";
                model.Customer = "";
                var result = await controllerHelper.InsertData(model);
                return Json(new { success = result.OK, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi Controller: " + ex.Message });
            }
        }

        /// <summary>
        /// 2. XỬ LÝ CẬP NHẬT ĐƠN LẺ (UPDATE)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Update(HourlyTargetViewModel model)
        {
            try
            {
                var result = await controllerHelper.UpdateData(model);
                return Json(new { success = result.OK, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi Controller: " + ex.Message });
            }
        }

        /// <summary>
        /// 3. XỬ LÝ XÓA THEO KHÓA CHÍNH (DELETE)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Delete(string dateTime, string operation, string typeValue, string shift)
        {
            try
            {
                var result = await controllerHelper.DeleteData(dateTime, operation, typeValue, shift);
                return Json(new { success = result.OK, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi Controller: " + ex.Message });
            }
        }

        /// <summary>
        /// 4. XỬ LÝ UPLOAD VÀ PARSE FILE EXCEL (IMPORT EXCEL)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ImportExcel(IFormFile file, string stringDate)
        {
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn tệp tin Excel hợp lệ trước khi tải lên." });
            }

            var listHourlyTargets = new List<HourlyTargetViewModel>(); // Khởi tạo list model nhận dữ liệu sạch mang đi Bulk
            bool hasError = false;

            try
            {
                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);
                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheets.First();
                        int rowCount = worksheet.LastRowUsed()?.RowNumber() ?? 0;

                        if (rowCount < 3)
                        {
                            return Json(new { success = false, message = "Tệp Excel trống hoặc không có dòng dữ liệu." });
                        }

                        // Đọc vòng lặp từ dòng 2 (bỏ dòng tiêu đề header)
                        for (int row = 3; row <= rowCount; row++)
                        {
                            var rowErrors = new List<string>();

                            // Bóc chuỗi thô từ 13 cột chính xuất hiện trong bảng hiển thị giao diện
                            string operation = worksheet.Cell(row, 1).GetValue<string>()?.Trim();
                            string typeValue = worksheet.Cell(row, 2).GetValue<string>()?.Trim();
                            string t1Raw = worksheet.Cell(row, 3).GetValue<string>()?.Trim();
                            string t2Raw = worksheet.Cell(row, 4).GetValue<string>()?.Trim();
                            string t3Raw = worksheet.Cell(row, 5).GetValue<string>()?.Trim();
                            string t4Raw = worksheet.Cell(row, 6).GetValue<string>()?.Trim();
                            string t5Raw = worksheet.Cell(row, 7).GetValue<string>()?.Trim();
                            string t6Raw = worksheet.Cell(row, 8).GetValue<string>()?.Trim();
                            string dateTime = worksheet.Cell(row, 9).GetValue<string>()?.Trim();
                            string wc = worksheet.Cell(row, 10).GetValue<string>()?.Trim();
                            string achieveRaw = worksheet.Cell(row, 11).GetValue<string>()?.Trim();
                            string product = worksheet.Cell(row, 12).GetValue<string>()?.Trim(); // Cột Line map vào thuộc tính Product
                            string shift = worksheet.Cell(row, 13).GetValue<string>()?.Trim();

                            // Bỏ qua dòng trống nếu người dùng kéo thừa ô ở cuối file Excel
                            if (string.IsNullOrEmpty(operation) && string.IsNullOrEmpty(dateTime) && string.IsNullOrEmpty(typeValue))
                            {
                                continue;
                            }

                            // ─── KIỂM TRA ĐIỀU KIỆN BẮT BUỘC (VALIDATION) ───
                            if (string.IsNullOrEmpty(operation)) rowErrors.Add("Thiếu Operation.");
                            if (string.IsNullOrEmpty(dateTime)) rowErrors.Add("Thiếu Date_time.");
                            if (string.IsNullOrEmpty(shift)) rowErrors.Add("Thiếu Shift (day/night).");

                            // Kiểm tra giá trị select box Type Value hợp lệ
                            if (string.IsNullOrEmpty(typeValue))
                            {
                                rowErrors.Add("Thiếu Type_value.");
                            }
                            else if (typeValue != "Target" && typeValue != "Man Q'ty")
                            {
                                rowErrors.Add("Type_value bắt buộc phải là 'Target' hoặc 'Man Q'ty'.");
                            }

                            // Ép kiểu dữ liệu số an toàn
                            double t1 = 0, t2 = 0, t3 = 0, t4 = 0, t5 = 0, t6 = 0, achieve = 0;

                            if (!string.IsNullOrEmpty(t1Raw) && !double.TryParse(t1Raw, out t1)) rowErrors.Add("Time1 không phải là số.");
                            if (!string.IsNullOrEmpty(t2Raw) && !double.TryParse(t2Raw, out t2)) rowErrors.Add("Time2 không phải là số.");
                            if (!string.IsNullOrEmpty(t3Raw) && !double.TryParse(t3Raw, out t3)) rowErrors.Add("Time3 không phải là số.");
                            if (!string.IsNullOrEmpty(t4Raw) && !double.TryParse(t4Raw, out t4)) rowErrors.Add("Time4 không phải là số.");
                            if (!string.IsNullOrEmpty(t5Raw) && !double.TryParse(t5Raw, out t5)) rowErrors.Add("Time5 không phải là số.");
                            if (!string.IsNullOrEmpty(t6Raw) && !double.TryParse(t6Raw, out t6)) rowErrors.Add("Time6 không phải là số.");
                            if (!string.IsNullOrEmpty(achieveRaw) && !double.TryParse(achieveRaw, out achieve)) rowErrors.Add("Achieve không phải là số.");

                            // ─── XỬ LÝ GHI FILE LỖI NẾU CÓ DÒNG THẤT BẠI ───
                            if (rowErrors.Any())
                            {
                                hasError = true;
                                var errorCell = worksheet.Cell(row, 14); // Ghi nội dung lỗi vào cột số 14 (Cột N - Options)
                                errorCell.Value = string.Join(" | ", rowErrors);
                                errorCell.Style.Font.SetFontColor(XLColor.Red);
                            }
                            else
                            {
                                // Nếu dữ liệu dòng này chuẩn, map vào list để chuẩn bị lưu DB
                                listHourlyTargets.Add(new HourlyTargetViewModel
                                {
                                    Operation = operation,
                                    Type_value = typeValue,
                                    Time1 = t1,
                                    Time2 = t2,
                                    Time3 = t3,
                                    Time4 = t4,
                                    Time5 = t5,
                                    Time6 = t6,
                                    Date_time = dateTime,
                                    WC = wc,
                                    Achieve = achieve,
                                    Product = product,
                                    Shift = shift,
                                    Forecast = 0,     // Gán giá trị rỗng/mặc định cho các trường không dùng ngoài bảng
                                    WORunning = "",
                                    Customer = ""
                                });
                            }
                        }

                        // Nếu có bất kì dòng nào dính lỗi, ngắt tiến trình lưu DB và trả file Excel về ngay
                        if (hasError)
                        {
                            var headerCell = worksheet.Cell(1, 14);
                            headerCell.Value = "Options (Chi tiết lỗi Import)";
                            headerCell.Style.Font.SetBold(true).Font.SetFontColor(XLColor.DarkRed);
                            worksheet.Column(14).AdjustToContents();

                            using (var errorStream = new MemoryStream())
                            {
                                workbook.SaveAs(errorStream);
                                return File(errorStream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Import_HourlyTarget_Errors.xlsx");
                            }
                        }
                    }
                }

                // --- GỌI HÀM BULK ĐÃ TỐI ƯU CỦA DAPPER XUỐNG DB ---
                var result = await controllerHelper.InsertAndUpdateData(listHourlyTargets);

                return Json(new { success = result.OK, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi xử lý luồng hệ thống: " + ex.Message });
            }
        }
    }
}
