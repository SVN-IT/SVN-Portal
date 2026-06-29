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
                if (viewModels != null && viewModels.Count != 0)
                {
                    if (!string.IsNullOrWhiteSpace(companyCode))
                    {
                        switch (companyCode)
                        {
                            case "SM":
                                // Lọc các item có đuôi (SM)
                                viewModels = viewModels.Where(x => x.Operation.Contains("(SM)")).ToList();
                                break;

                            case "ITA":
                                // Lọc các item có đuôi (ITA)
                                viewModels = viewModels.Where(x => x.Operation.Contains("(ITA)")).ToList();
                                break;

                            case "SVN":
                                // Lọc các item KHÔNG chứa (SM) và KHÔNG chứa (ITA)
                                viewModels = viewModels.Where(x => !x.Operation.Contains("(SM)") && !x.Operation.Contains("(ITA)")).ToList();
                                break;
                        }
                    }
                }
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
        public async Task<IActionResult> Delete(string operation, string datetime, string shift)
        {
            try
            {
                if (string.IsNullOrEmpty(operation))
                {
                    return Json(new { success = false, message = "Data invalid" });
                }

                var result = await controllerHelper.DeleteData(datetime, operation, shift); 

                return Json(new { success = true, message = "Đã xóa bản ghi thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ImportExcel(IFormFile file)
        {
            try
            {
                string stringDate = "20260629";
                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = "Pls choose valid excel file" });
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
                            return Json(new { success = false, message = "Excel file is empty or have no data" });
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
                            if (string.IsNullOrWhiteSpace(operation)) rowErrors.Add("Operation col is Empty");

                            // KIỂM TRA NGÀY THÁNG HỢP LỆ VÀ ĐỒNG BỘ VỚI VIEW
                            if (string.IsNullOrWhiteSpace(dateTime))
                            {
                                rowErrors.Add("Date_time col is Empty");
                            }
                            else if (!string.IsNullOrWhiteSpace(stringDate) && dateTime != stringDate)
                            {
                                // CHẮT LỌC LỖI: Phát hiện ngày trong file lệch với ngày đang chọn ngoài giao diện
                                rowErrors.Add($"Datetime '{dateTime}' is incorrect , pls input '{stringDate}'.");
                            }

                            if (string.IsNullOrWhiteSpace(shift))
                            {
                                rowErrors.Add("Shift (day/night) col is empty");
                            }
                            else if (shift.ToLower() != "day" && shift.ToLower() != "night")
                            {
                                // THÊM MỚI: Bắt buộc giá trị cột Shift phải thuộc 1 trong 2 từ khóa cố định
                                rowErrors.Add($"Shift '{shift}' not valid (Must input 'day' or 'night').");
                            }

                            double dailyPlan = 0, uph = 0, upph = 0, labor = 0, defect = 0, workingTime = 0;

                            if (!string.IsNullOrWhiteSpace(dailyPlanRaw) && !double.TryParse(dailyPlanRaw, out dailyPlan)) rowErrors.Add("Daily_plan pls input number.");
                            if (!string.IsNullOrWhiteSpace(uphRaw) && !double.TryParse(uphRaw, out uph)) rowErrors.Add("UPH pls input number.");
                            if (!string.IsNullOrWhiteSpace(upphRaw) && !double.TryParse(upphRaw, out upph)) rowErrors.Add("UPPH pls input number.");
                            if (!string.IsNullOrWhiteSpace(laborRaw) && !double.TryParse(laborRaw, out labor)) rowErrors.Add("Labor pls input number.");
                            if (!string.IsNullOrWhiteSpace(defectRaw) && !double.TryParse(defectRaw, out defect)) rowErrors.Add("Defect pls input number.");
                            if (!string.IsNullOrWhiteSpace(workingTimeRaw) && !double.TryParse(workingTimeRaw, out workingTime)) rowErrors.Add("Workingtime pls input number.");

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
                            var headerCell = worksheet.Cell(2, 10);
                            headerCell.Value = "Options (Chi tiết lỗi Import)";
                            headerCell.Style.Font.SetBold(true).Font.SetFontColor(XLColor.DarkRed);
                            worksheet.Column(14).AdjustToContents();

                            using (var errorStream = new MemoryStream())
                            {
                                workbook.SaveAs(errorStream);
                                byte[] fileBytes = errorStream.ToArray();

                                // Mã hóa mảng byte trên RAM thành chuỗi Base64 an toàn tuyệt đối
                                string base64File = Convert.ToBase64String(fileBytes);

                                return Json(new
                                {
                                    success = false,
                                    hasExcelError = true,
                                    fileBase64 = base64File,
                                    fileName = "Import_DailyTarget_Errors.xlsx",
                                    message = "Import dữ liệu thất bại! Phát hiện dòng dữ liệu sai định dạng. Hệ thống đã tự động kết xuất tệp Excel ghi chú lỗi."
                                });
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
                    return Json(new { success = false, message = "Lỗi xử lý Server: " + ex.Message });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi xử lý Server: " + ex.Message });
            }
        }
    }
}
