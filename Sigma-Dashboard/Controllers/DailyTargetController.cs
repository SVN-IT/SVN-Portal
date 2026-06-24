using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
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
            if (file == null || file.Length == 0)
            {
                return Json(new { success = false, message = "Vui lòng chọn file Excel hợp lệ." });
            }

            // Thiết lập License cho EPPlus (Bắt buộc từ bản v5 trở đi)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var listTargets = new List<DailyTargetViewModel>();
            bool hasError = false;

            using (var stream = new MemoryStream())
            {
                await file.CopyToAsync(stream);
                using (var package = new ExcelPackage(stream))
                {
                    // Lấy Worksheet đầu tiên
                    ExcelWorksheet worksheet = package.Workbook.Worksheets[0];
                    int rowCount = worksheet.Dimension?.Rows ?? 0;

                    // Giả định dòng 1 là tiêu đề (Header), dữ liệu bắt đầu từ dòng 2
                    for (int row = 2; row <= rowCount; row++)
                    {
                        var rowErrors = new List<string>();

                        // Đọc dữ liệu thô từ các cột A -> I
                        string operation = worksheet.Cells[row, 1].Value?.ToString()?.Trim();
                        string dailyPlanRaw = worksheet.Cells[row, 2].Value?.ToString()?.Trim();
                        string uphRaw = worksheet.Cells[row, 3].Value?.ToString()?.Trim();
                        string upphRaw = worksheet.Cells[row, 4].Value?.ToString()?.Trim();
                        string laborRaw = worksheet.Cells[row, 5].Value?.ToString()?.Trim();
                        string dateTime = worksheet.Cells[row, 6].Value?.ToString()?.Trim();
                        string defectRaw = worksheet.Cells[row, 7].Value?.ToString()?.Trim();
                        string workingTimeRaw = worksheet.Cells[row, 8].Value?.ToString()?.Trim();
                        string shift = worksheet.Cells[row, 9].Value?.ToString()?.Trim();

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

                        // Nếu dòng này có lỗi, ghi vào cột Options (Cột 10 - cột J)
                        if (rowErrors.Any())
                        {
                            hasError = true;
                            worksheet.Cells[row, 10].Value = string.Join(" | ", rowErrors);
                            worksheet.Cells[row, 10].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                        }
                        else
                        {
                            // Nếu không có lỗi, nạp vào danh sách chờ xử lý CSDL
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

                    // THÀNH PHẦN TRẢ LỖI: Nếu có bất kỳ dòng nào lỗi -> Dừng lưu, xuất file Excel lỗi ngay lập tức
                    if (hasError)
                    {
                        // Tạo tiêu đề cho cột J nếu chưa có
                        worksheet.Cells[1, 10].Value = "Options (Lỗi hệ thống)";
                        worksheet.Cells[1, 10].Style.Font.Bold = true;
                        worksheet.Cells[1, 10].Style.Font.Color.SetColor(System.Drawing.Color.DarkRed);
                        worksheet.Column(10).Width = 40;

                        var errorFileBytes = package.GetAsByteArray();
                        return File(errorFileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Import_Errors.xlsx");
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
    }
}
