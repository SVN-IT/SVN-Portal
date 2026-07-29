using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Sigma_Dashboard.Models;
using Sigma_Dashboard.Services.Configurations;
using Sigma_Dashboard.Services.Helpers;
using SVNShareLib;
using System.Threading.Tasks;

namespace Sigma_Dashboard.Controllers
{
    public class HourlyTargetController : Controller
    {
        AppConfig appConfig;
        HourlyTargetControllerHelper controllerHelper;
        DailyTargetControllerHelper dailyTargetControllerHelper;
        public HourlyTargetController(AppConfig appConfig, HourlyTargetControllerHelper controllerHelper, 
            DailyTargetControllerHelper dailyTargetControllerHelper)
        {
            this.appConfig = appConfig;
            this.controllerHelper = controllerHelper;
            this.dailyTargetControllerHelper = dailyTargetControllerHelper;
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
                if(viewModels != null && viewModels.Count != 0)
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

        /// <summary>
        /// 1. XỬ LÝ THÊM MỚI ĐƠN LẺ (CREATE)
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Create(HourlyTargetViewModel model)
        {
            try
            {
                var validationResult = await ValidUpdateData(model);
                if (!validationResult.OK)
                {
                    return Json(new { success = false, message = validationResult.Message });
                }
                // Đồng bộ ép các trường không có trong bảng về giá trị an toàn/mặc định dưới DB
                model.Forecast = 0;
                model.WORunning = "";
                model.Customer = "";
                var result = await controllerHelper.InsertData(model);
                return Json(new { success = result.OK, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
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
                var validationResult = await ValidUpdateData(model);
                if (!validationResult.OK)
                {
                    return Json(new { success = false, message = validationResult.Message });
                }
                var result = await controllerHelper.UpdateData(model);
                return Json(new { success = result.OK, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
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
                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// 4. XỬ LÝ UPLOAD VÀ PARSE FILE EXCEL (IMPORT EXCEL)
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ImportExcel([FromBody] ExcelUploadRequest request)
        {
            if (request == null || string.IsNullOrEmpty(request.FileBase64))
            {
                return Json(new { success = false, message = "Can't get data from Excel file." });
            }

            // 1. Chuyển chuỗi Base64 ngược về mảng byte thô xử lý trực tiếp trên RAM
            byte[] fileBytes = Convert.FromBase64String(request.FileBase64);

            // 2. Ép kiểu và kiểm tra ngày tháng an toàn tuyệt đối bằng TryParseExact
            bool isToday = true;
            string stringDate = request.StringDate?.Trim();
            if (DateTime.TryParseExact(stringDate, "yyyyMMdd",
                                       System.Globalization.CultureInfo.InvariantCulture,
                                       System.Globalization.DateTimeStyles.None,
                                       out DateTime parsedDate))
            {
                if (parsedDate < DateTime.Today) isToday = false;
            }
            else
            {
                isToday = false;
            }

            if (!isToday)
            {
                return Json(new { success = false, message = $"Can't input data for last date (Date: {stringDate})." });
            }

            var listHourlyTargets = new List<HourlyTargetViewModel>(); // Khởi tạo list model nhận dữ liệu sạch mang đi Bulk
            bool hasError = false;

            try
            {
                using (var stream = new MemoryStream(fileBytes))
                {
                    using (var workbook = new XLWorkbook(stream))
                    {
                        var worksheet = workbook.Worksheets.First();
                        int rowCount = worksheet.LastRowUsed()?.RowNumber() ?? 0;

                        if (rowCount < 3)
                        {
                            return Json(new { success = false, message = "Excel file is empty or have no data" });
                        }

                        // Đọc vòng lặp từ dòng 3 (bỏ dòng tiêu đề header)
                        for (int row = 3; row <= rowCount; row++)
                        {
                            var rowErrors = new List<string>();

                            // Bóc chuỗi thô từ các cột chính
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
                            if (string.IsNullOrEmpty(operation)) rowErrors.Add("Operation col is Empty");

                            if (string.IsNullOrEmpty(shift)) 
                            { 
                                rowErrors.Add("Shift (day/night) col is empty"); 
                            }
                            else if (shift.ToLower() != "day" && shift.ToLower() != "night")
                            {
                                // THÊM MỚI: Bắt buộc giá trị cột Shift phải thuộc 1 trong 2 từ khóa cố định
                                rowErrors.Add($"Shift '{shift}' not valid (Must input 'day' or 'night').");
                            }


                            // KIỂM TRA NGÀY THÁNG HỢP LỆ VÀ ĐỒNG BỘ VỚI VIEW
                            if (string.IsNullOrWhiteSpace(dateTime))
                            {
                                rowErrors.Add("Date_time col is Empty");
                            }
                            else if (!string.IsNullOrWhiteSpace(stringDate))
                            {
                                DateTime rowDateTime = DateTime.ParseExact(dateTime, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
                                DateTime curDateTime = DateTime.ParseExact(stringDate, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
                                if (rowDateTime < curDateTime)
                                {
                                    rowErrors.Add($"Datetime '{dateTime}' is invalid (must be >= '{stringDate}').");
                                }
                            }

                            // Kiểm tra giá trị select box Type Value hợp lệ
                            if (string.IsNullOrWhiteSpace(typeValue))
                            {
                                rowErrors.Add("Type_value col is empty.");
                            }
                            else if (typeValue != "Target" && typeValue != "Man Q'ty")
                            {
                                rowErrors.Add("Type_value pls input 'Target' or 'Man Q'ty'.");
                            }

                            // Ép kiểu dữ liệu số an toàn
                            int t1 = 0, t2 = 0, t3 = 0, t4 = 0, t5 = 0, t6 = 0; 
                            double achieve = 0;

                            if (!string.IsNullOrWhiteSpace(t1Raw) && !int.TryParse(t1Raw, out t1)) rowErrors.Add("Time1 pls input integer.");
                            if (!string.IsNullOrWhiteSpace(t2Raw) && !int.TryParse(t2Raw, out t2)) rowErrors.Add("Time2 pls input integer.");
                            if (!string.IsNullOrWhiteSpace(t3Raw) && !int.TryParse(t3Raw, out t3)) rowErrors.Add("Time3 pls input integer.");
                            if (!string.IsNullOrWhiteSpace(t4Raw) && !int.TryParse(t4Raw, out t4)) rowErrors.Add("Time4 pls input integer.");
                            if (!string.IsNullOrWhiteSpace(t5Raw) && !int.TryParse(t5Raw, out t5)) rowErrors.Add("Time5 pls input integer.");
                            if (!string.IsNullOrWhiteSpace(t6Raw) && !int.TryParse(t6Raw, out t6)) rowErrors.Add("Time6 pls input integer.");
                            if (!string.IsNullOrWhiteSpace(achieveRaw) && !double.TryParse(achieveRaw, out achieve)) rowErrors.Add("Achieve pls input number.");

                            if(typeValue == "Target")
                            {
                                // Nếu Type_value = Target thì các giá trị Time1..Time6 phải >= 0
                                if (t1 < 0 || t2 < 0 || t3 < 0 || t4 < 0 || t5 < 0 || t6 < 0)
                                {
                                    rowErrors.Add("Values must be non-negative.");
                                }
                                var dallyTotal = t1 + t2 + t3 + t4 + t5 + t6;
                                var dailyTarget = await dailyTargetControllerHelper.GetDailyTargetByDateAndOperation(dateTime, operation, shift);
                                if (dailyTarget != null && !string.IsNullOrWhiteSpace(dailyTarget.Operation) && dallyTotal != dailyTarget.Daily_plan)
                                {
                                    rowErrors.Add($"Total hourly target ({dallyTotal}) exceeds daily target ({dailyTarget.Daily_plan}).");
                                }
                                if (dailyTarget == null || (dailyTarget != null && string.IsNullOrWhiteSpace(dailyTarget.Operation)))
                                {
                                    rowErrors.Add($"Daily target not found for operation {operation} on date {dateTime}. Pls insert Daily Target firt.");
                                }
                            }

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

                        // Nếu có bất kì dòng nào dính lỗi (bao gồm cả lỗi sai Date_time), ngắt tiến trình lưu DB và trả file Excel về ngay
                        // ─── NẾU CÓ DÒNG LỖI: Mã hóa file Excel thành chuỗi Base64 ───
                        if (hasError)
                        {
                            var headerCell = worksheet.Cell(2, 14);
                            headerCell.Value = "Options (Details Import Error)";
                            headerCell.Style.Font.SetBold(true).Font.SetFontColor(XLColor.DarkRed);
                            worksheet.Column(14).AdjustToContents();

                            using (var errorStream = new MemoryStream())
                            {
                                workbook.SaveAs(errorStream);

                                // Biến đổi cấu trúc Workbook lỗi thành chuỗi Base64 an toàn trả về dạng văn bản JSON
                                string base64File = Convert.ToBase64String(errorStream.ToArray());

                                return Json(new
                                {
                                    success = false,
                                    hasExcelError = true,
                                    fileBase64 = base64File,
                                    fileName = "Import_HourlyTarget_Errors.xlsx",
                                    message = "Import failed! data invalid. System will export details error file."
                                });
                            }
                        }
                    }
                }

                // --- GỌI HÀM BULK ĐÃ TỐI ƯU CỦA DAPPER XUỐNG DB ---
                // Đến đây toàn bộ listDữLiệu đều trùng khớp Date_time với UI 100%
                var result = await controllerHelper.InsertAndUpdateData(listHourlyTargets);

                return Json(new { success = result.OK, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "System error: " + ex.Message });
            }
        }

        private async Task<BODataProcessResult> ValidUpdateData(HourlyTargetViewModel model)
        {
            var result = new BODataProcessResult();
            result.OK = true;
            if (string.IsNullOrWhiteSpace(model.Operation))
            {
                result.OK = false;
                result.Message = "Operation is empty.";
                return result;
            }
            if (string.IsNullOrWhiteSpace(model.Type_value))
            {
                result.OK = false;
                result.Message = "Type_value is empty.";
                return result;
            }
            if (string.IsNullOrWhiteSpace(model.Date_time))
            {
                result.OK = false;
                result.Message = "Date_time is empty.";
                return result;
            }
            if (string.IsNullOrWhiteSpace(model.Shift))
            {
                result.OK = false;
                result.Message = "Shift is empty.";
                return result;
            }
            if (model.Type_value == "Target")
            {
                if (model.Time1 < 0 || model.Time2 < 0 || model.Time3 < 0 || model.Time4 < 0 || model.Time5 < 0 || model.Time6 < 0)
                {
                    result.OK = false;
                    result.Message = "Values must be non-negative.";
                    return result;
                }
                var dallyTotal = model.Time1 + model.Time2 + model.Time3 + model.Time4 + model.Time5 + model.Time6;
                var dailyTarget = await dailyTargetControllerHelper.GetDailyTargetByDateAndOperation(model.Date_time, model.Operation, model.Shift);
                if (dailyTarget != null && !string.IsNullOrWhiteSpace(dailyTarget.Operation) && dallyTotal != dailyTarget.Daily_plan)
                {
                    result.OK = false;
                    result.Message = $"Total hourly target ({dallyTotal}) exceeds daily target ({dailyTarget.Daily_plan}).";
                    return result;
                }
                if(dailyTarget == null || (dailyTarget != null && string.IsNullOrWhiteSpace(dailyTarget.Operation)))
                {
                    result.OK = false;
                    result.Message = $"Daily target not found for operation {model.Operation} on date {model.Date_time}.  Pls insert Daily Target firt.";
                    return result;
                }
            }
            return result;
        }
    }
}
