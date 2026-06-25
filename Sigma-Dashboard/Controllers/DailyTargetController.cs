using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Sigma_Dashboard.Models;
using Sigma_Dashboard.Services.Configurations;
using Sigma_Dashboard.Services.Helpers;
using System.Threading.Tasks;

namespace Sigma_Dashboard.Controllers
{
    public class DailyTargetController : Controller
    {
        AppConfig appConfig;
        DailyTargetControllerHelper controllerHelper;
        public DailyTargetController(AppConfig appConfig, DailyTargetControllerHelper controllerHelper)
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
            ViewBag.ControllerName = "DailyTarget";
            ViewBag.IndexPage = "Index";
            ViewBag.TitleName = $"Daily Target - {date.ToString("dd/MM/yyyy")}";
            ViewBag.CompanyCode = companyCode;
            ViewBag.AccessMode = "PMC";
            List<DailyTargetViewModel> viewModels = new List<DailyTargetViewModel>();
            try
            {
                viewModels = await controllerHelper.GetDailyTargetData(date, shift, companyCode);
            }
            catch
            {

            }
            return View(viewModels);
        }

        // 1. ACTION THÊM MỚI (Nhận dữ liệu JSON)
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] DailyTargetViewModel model)
        {
            try
            {
                if (model == null || string.IsNullOrEmpty(model.Operation))
                {
                    return Json(new { success = false, message = "Data invalid" });
                }

                var result = await controllerHelper.InsertData(model);

                return Json(new { success = result.OK, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // 2. ACTION CẬP NHẬT (Nhận dữ liệu JSON)
        [HttpPost]
        public async Task<IActionResult> Edit([FromBody] DailyTargetViewModel model)
        {
            try
            {
                if (model == null || string.IsNullOrEmpty(model.Operation))
                {
                    return Json(new { success = false, message = "Data invalid" });
                }

                var result = await controllerHelper.UpdateData(model);

                return Json(new { success = result.OK, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // 3. ACTION XÓA (Nhận Key đơn lẻ)
        [HttpPost]
        public async Task<IActionResult> Delete(string operation, string datetime)
        {
            try
            {
                if (string.IsNullOrEmpty(operation))
                {
                    return Json(new { success = false, message = "Data invalid" });
                }

                var result = await controllerHelper.DeleteData(datetime, operation); 

                return Json(new { success = true, message = "Đã xóa bản ghi thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> ImportExcel(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = "Vui lòng chọn file Excel hợp lệ." });
                }

                var listTargets = new List<DailyTargetViewModel>();
                bool hasError = false;

                using (var stream = new MemoryStream())
                {
                    await file.CopyToAsync(stream);

                    // Khởi tạo Workbook của ClosedXML
                    using (var workbook = new XLWorkbook(stream))
                    {
                        // Lấy Worksheet đầu tiên (ClosedXML có thể lấy theo tên hoặc theo vị trí thứ tự)
                        var worksheet = workbook.Worksheets.First();

                        // Lấy dòng cuối cùng có chứa dữ liệu
                        int rowCount = worksheet.LastRowUsed()?.RowNumber() ?? 0;

                        if (rowCount < 3)
                        {
                            return Json(new { success = false, message = "File Excel không có dữ liệu." });
                        }

                        // Vòng lặp quét dữ liệu từ dòng 3
                        for (int row = 3; row <= rowCount; row++)
                        {
                            var rowErrors = new List<string>();

                            // Đọc dữ liệu thô (ClosedXML dùng .Cell(row, col).GetValue<string>())
                            string operation = worksheet.Cell(row, 1).GetValue<string>()?.Trim();
                            string dailyPlanRaw = worksheet.Cell(row, 2).GetValue<string>()?.Trim();
                            string uphRaw = worksheet.Cell(row, 3).GetValue<string>()?.Trim();
                            string upphRaw = worksheet.Cell(row, 4).GetValue<string>()?.Trim();
                            string laborRaw = worksheet.Cell(row, 5).GetValue<string>()?.Trim();
                            string dateTime = worksheet.Cell(row, 6).GetValue<string>()?.Trim();
                            string defectRaw = worksheet.Cell(row, 7).GetValue<string>()?.Trim();
                            string workingTimeRaw = worksheet.Cell(row, 8).GetValue<string>()?.Trim();
                            string shift = worksheet.Cell(row, 9).GetValue<string>()?.Trim();

                            // --- VALIDATION LOGIC ---
                            if (string.IsNullOrEmpty(operation)) rowErrors.Add("Operation không được để trống.");
                            if (string.IsNullOrEmpty(dateTime)) rowErrors.Add("Date_time không được để trống.");

                            double dailyPlan = 0, uph = 0, upph = 0, labor = 0, defect = 0, workingTime = 0;

                            if (!string.IsNullOrEmpty(dailyPlanRaw) && !double.TryParse(dailyPlanRaw, out dailyPlan)) rowErrors.Add("Daily_plan phải là số.");
                            if (!string.IsNullOrEmpty(uphRaw) && !double.TryParse(uphRaw, out uph)) rowErrors.Add("UPH phải là số.");
                            if (!string.IsNullOrEmpty(upphRaw) && !double.TryParse(upphRaw, out upph)) rowErrors.Add("UPPH phải là số.");
                            if (!string.IsNullOrEmpty(laborRaw) && !double.TryParse(laborRaw, out labor)) rowErrors.Add("Labor phải là số.");
                            if (!string.IsNullOrEmpty(defectRaw) && !double.TryParse(defectRaw, out defect)) rowErrors.Add("Defect phải là số.");
                            if (!string.IsNullOrEmpty(workingTimeRaw) && !double.TryParse(workingTimeRaw, out workingTime)) rowErrors.Add("Workingtime phải là số.");

                            // Nếu dòng này có lỗi -> Ghi chú trực tiếp vào cột 10 (Cột J)
                            if (rowErrors.Any())
                            {
                                hasError = true;
                                var errorCell = worksheet.Cell(row, 10);
                                errorCell.Value = string.Join(" | ", rowErrors);
                                errorCell.Style.Font.SetFontColor(XLColor.Red); // Chữ màu đỏ báo lỗi
                            }
                            else
                            {
                                // Nếu không có lỗi, nạp tạm vào list chờ lưu DB
                                listTargets.Add(new DailyTargetViewModel
                                {
                                    Operation = operation,
                                    Daily_plan = dailyPlan,
                                    UPH = uph,
                                    UPPH = upph,
                                    Labor = labor,
                                    Date_time = dateTime,
                                    Defect = defect,
                                    Workingtime = workingTime,
                                    Shift = shift
                                });
                            }
                        }

                        // THÀNH PHẦN XUẤT LỖI: Nếu phát hiện bất kỳ dòng nào lỗi, dừng tiến trình và trả file
                        if (hasError)
                        {
                            // Tạo Header cho cột lỗi J
                            var headerCell = worksheet.Cell(1, 10);
                            headerCell.Value = "Options (Lỗi hệ thống)";
                            headerCell.Style.Font.SetBold(true);
                            headerCell.Style.Font.SetFontColor(XLColor.DarkRed);
                            worksheet.Column(10).AdjustToContents(); // Tự động dãn độ rộng cột J theo nội dung lỗi

                            using (var errorStream = new MemoryStream())
                            {
                                workbook.SaveAs(errorStream);
                                var errorFileBytes = errorStream.ToArray();
                                return File(errorFileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Import_DailyTarget_Errors.xlsx");
                            }
                        }
                    }
                }

                // --- LƯU VÀO CƠ SỞ DỮ LIỆU (Chỉ chạy khi toàn bộ file không có lỗi nào) ---
                try
                {
                    var result = await controllerHelper.InsertAndUpdateData(listTargets);

                    return Json(new { success = result.OK, message = result.Message });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "Lỗi trong quá trình lưu CSDL: " + ex.Message });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi trong quá trình xử lý file Excel: " + ex.Message });
            }
        }
    }
}
