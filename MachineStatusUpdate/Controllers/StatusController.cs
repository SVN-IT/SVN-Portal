using System.Drawing;
using ClosedXML.Excel;
using MachineStatusUpdate.Models;
using MachineStatusUpdate.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZXing;


namespace MachineStatusUpdate.Controllers
{
    public class StatusController : Controller
    {

        private readonly ApplicationDbContext _context;

        private readonly IWebHostEnvironment _webHostEnvironment;

        private readonly IStatusUpdateService _statusUpdateService;


        public StatusController(ApplicationDbContext context, IWebHostEnvironment webHostEnvironment, IStatusUpdateService statusUpdateService)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
            _statusUpdateService = statusUpdateService;

        }

        
        [HttpGet]
        public async Task<IActionResult> GetLatestDowntimeForOperation(string operation)
        {
            if (string.IsNullOrWhiteSpace(operation))
                return Json(new { exists = false });

            var op = operation.Trim();
            var today = DateTime.Now.Date;

            var latest = await _context.SVN_Downtime_Infos
                .Where(x => x.Operation != null
                            && x.Operation.Trim() == op
                            && x.Datetime.HasValue
                            && x.Datetime.Value.Date == today)
                .OrderByDescending(x => x.Datetime)
                .Select(x => new
                {
                    state = (x.State ?? "").Trim(),
                    ISS_Code = (x.ISS_Code ?? "").Trim()
                })
                .FirstOrDefaultAsync();

            if (latest == null)
                return Json(new { exists = false });

            return Json(new { exists = true, state = latest.state, ISSCode = latest.ISS_Code });
        }


        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }


        /*==============================================Machine Status Update=====================================================*/

        // Hàm check mã máy có khớp không
        [HttpPost]
        public async Task<IActionResult> ValidateCode([FromBody] ValidateCodeRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Code))
                {
                    return Json(new { exists = false });
                }

                var exists = await _context.sVN_Equipment_Machine_Info
                    .AnyAsync(x => x.SVNCode == request.Code);

                return Json(new { exists = exists });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating code: {ex.Message}");
                return Json(new { exists = false });
            }
        }

        // Hàm xác định Operation dựa trên Code
        private async Task<string> GetOperationFromCodeAsync(string code)
        {
            if (string.IsNullOrEmpty(code))
                return "";

            try
            {
                var machineInfo = await _context.sVN_Equipment_Machine_Info
                    .FirstOrDefaultAsync(x => x.SVNCode == code);

                return machineInfo?.Project ?? "";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting operation from code: {ex.Message}");
                return "";
            }
        }

        // Hàm decode mã QR từ upload
        [HttpPost]
        public async Task<IActionResult> DecodeQR(IFormFile qrImage)
        {
            if (qrImage == null || qrImage.Length == 0)
                return Json(new { success = false, message = "Chưa chọn ảnh!" });

            try
            {
                using var stream = qrImage.OpenReadStream();
                using var skBitmap = SkiaSharp.SKBitmap.Decode(stream);

                var reader = new ZXing.SkiaSharp.BarcodeReader();
                var result = reader.Decode(skBitmap);

                if (result != null)
                {
                    return Json(new { success = true, code = result.Text });
                }
                else
                {
                    return Json(new { success = false, message = "Không đọc được mã từ ảnh!" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi xử lý: " + ex.Message });
            }
        }

        // Method để xử lý lại toàn bộ dữ liệu (nếu cần)
        public async Task<IActionResult> ProcessAllHistoryToDetail()
        {
            try
            {
                // Xóa toàn bộ dữ liệu cũ trong bảng Detail
                var existingDetails = _context.SVN_Equipment_Status_Update_Detail.ToList();
                _context.SVN_Equipment_Status_Update_Detail.RemoveRange(existingDetails);
                await _context.SaveChangesAsync();

                // Lấy tất cả records từ History, group by Code và xử lý
                var allHistoryRecords = await _context.SVN_Equipment_Info_History_Test
                    .OrderBy(x => x.Code)
                    .ThenBy(x => x.Datetime)
                    .ToListAsync();

                var groupedByCode = allHistoryRecords.GroupBy(x => x.Code).ToList();

                foreach (var codeGroup in groupedByCode)
                {
                    var records = codeGroup.OrderBy(x => x.Datetime).ToList();

                    for (int i = 0; i < records.Count; i++)
                    {
                        var currentRecord = records[i];
                        if (!currentRecord.Datetime.HasValue) continue;

                        // Tính EstimateTime (số phút từ EstimateTime - DateTime)
                        double? estimateTimeMinutes = null;
                        if (!string.IsNullOrEmpty(currentRecord.EstimateTime))
                        {
                            if (TimeSpan.TryParse(currentRecord.EstimateTime, out TimeSpan estimateTimeSpan))
                            {
                                var estimateDateTime = currentRecord.Datetime.Value.Date.Add(estimateTimeSpan);
                                var timeDifference = estimateDateTime - currentRecord.Datetime.Value;
                                estimateTimeMinutes = timeDifference.TotalMinutes;
                            }
                        }

                        // Xử lý ToTime và DurationMinutes
                        string toTime = "";
                        float durationMinutes = 0;

                        if (i < records.Count - 1)
                        {
                            var nextRecord = records[i + 1];
                            if (nextRecord.Datetime.HasValue)
                            {
                                durationMinutes = (float)(nextRecord.Datetime.Value - currentRecord.Datetime.Value).TotalMinutes;
                                toTime = Math.Round(durationMinutes, 2).ToString(); // ToTime là số phút
                            }
                        }

                        // Tạo record mới cho bảng Detail
                        var detailRecord = new SVN_Equipment_Status_Update_Detail
                        {
                            Name = currentRecord.Name ?? "",
                            Operation = currentRecord.Operation ?? "",
                            State = currentRecord.State ?? "",
                            EstimateTime = estimateTimeMinutes?.ToString("F2") ?? "",
                            FromTime = currentRecord.Datetime.Value.ToString("yyyy-MM-dd HH:mm:ss"),
                            ToTime = toTime, // Lưu số phút, để rỗng nếu chưa có
                            DurationMinutes = durationMinutes
                        };

                        _context.SVN_Equipment_Status_Update_Detail.Add(detailRecord);
                    }
                }

                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Đã xử lý thành công toàn bộ dữ liệu History vào Detail!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi trong ProcessAllHistoryToDetail: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Create(SVN_Equipment_Info_History_Test model, IFormFile imageFile)
        {
            try
            {
                if (string.IsNullOrEmpty(model.Code) || string.IsNullOrEmpty(model.State))
                {
                    return Json(new { success = false, message = "Vui lòng điền đầy đủ thông tin bắt buộc!" });
                }

                var machineExists = await _context.sVN_Equipment_Machine_Info
                    .AnyAsync(x => x.SVNCode == model.Code);

                if (!machineExists)
                {
                    return Json(new { success = false, message = "Không tồn tại mã máy này trong hệ thống!" });
                }

                string imagePath = null;
                if (imageFile != null && imageFile.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                    var fileExtension = Path.GetExtension(imageFile.FileName).ToLower();

                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        return Json(new { success = false, message = "Chỉ cho phép upload ảnh với định dạng: jpg, jpeg, png, gif, bmp" });
                    }
                    if (imageFile.Length > 5 * 1024 * 1024)
                    {
                        return Json(new { success = false, message = "Kích thước ảnh không được vượt quá 5MB" });
                    }

                    var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "status-images");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }
                    var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{fileExtension}";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }
                    imagePath = $"/uploads/status-images/{fileName}";
                }

                string generateName = model.Code;
                if (!string.IsNullOrEmpty(model.Code) && model.Code.Contains("-"))
                {
                    var parts = model.Code.Split('-');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int number))
                    {
                        generateName = $"#{number}";
                    }
                }

                model.Name = generateName;
                model.Operation = await GetOperationFromCodeAsync(model.Code);
                model.Datetime = DateTime.Now;

                int insertedId = 0;
                using (var command = _context.Database.GetDbConnection().CreateCommand())
                {
                    command.CommandText = "EXEC [dbo].[SVN_InsertMachineStatus_Test] @Code, @Name, @State, @Operation, @EstimateTime, @Description, @Image, @Datetime";
                    command.CommandType = System.Data.CommandType.Text;

                    command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@Code", model.Code ?? ""));
                    command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@Name", model.Name ?? ""));
                    command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@State", model.State ?? ""));
                    command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@Operation", model.Operation ?? ""));
                    command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@EstimateTime", model.EstimateTime ?? ""));
                    command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@Description", model.Description ?? ""));
                    command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@Image", imagePath ?? ""));
                    command.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@Datetime", model.Datetime));

                    if (command.Connection.State != System.Data.ConnectionState.Open)
                        await command.Connection.OpenAsync();

                    var result = await command.ExecuteScalarAsync();
                    insertedId = Convert.ToInt32(result);
                }


                var insertedRecord = await _context.SVN_Equipment_Info_History_Test
                    .FirstOrDefaultAsync(x => x.Id == insertedId);

                await _statusUpdateService.ProcessSingleRecordToUpdateDetail(insertedRecord);

                return Json(new { success = true, message = "Lưu trạng thái thành công!", data = insertedRecord });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Create: {ex.Message}");
                return Json(new { success = false, message = $"Có lỗi xảy ra: {ex.Message}" });
            }
        }

        // Method để xử lý dữ liệu từ Detail sang Status Update
        [HttpPost]
        public async Task<IActionResult> ProcessToStatusUpdate(DateTime? filterDate = null)
        {
            try
            {
                await _statusUpdateService.ProcessDataToStatusUpdate(filterDate);
                return Json(new { success = true, message = "Đã xử lý thành công dữ liệu vào bảng Status Update!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi trong ProcessToStatusUpdate: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // Method hiển thị Status Update Report
        public async Task<IActionResult> StatusUpdateReport(DateTime? filterDate = null, string operation = "", int page = 1, int pageSize = 25)
        {
            try
            {
                var query = _context.SVN_Equipment_Status_Update.AsQueryable();

                // Apply date filter
                if (filterDate.HasValue)
                {
                    query = query.Where(x => x.Datetime.Date == filterDate.Value.Date);
                }

                if (!string.IsNullOrEmpty(operation))
                {
                    query = query.Where(x => x.Operation.Contains(operation));
                }

                var totalRecords = await query.CountAsync();

                var results = await query
                    .OrderByDescending(x => x.Datetime)
                    .ThenBy(x => x.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .AsNoTracking()
                    .ToListAsync();

                var totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

                // Pagination ViewBag
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalRecords = totalRecords;
                ViewBag.HasPreviousPage = page > 1;
                ViewBag.HasNextPage = page < totalPages;

                // Filter ViewBag
                ViewBag.FilterDate = filterDate?.ToString("yyyy-MM-dd") ?? "";
                ViewBag.Operation = operation ?? "";

                return View(results);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi: {ex.Message}";
                ViewBag.FilterDate = filterDate?.ToString("yyyy-MM-dd") ?? "";
                ViewBag.Operation = operation ?? "";

                // Set default pagination values for error case
                ViewBag.CurrentPage = 1;
                ViewBag.TotalPages = 0;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalRecords = 0;
                ViewBag.HasPreviousPage = false;
                ViewBag.HasNextPage = false;

                return View(new List<SVN_Equipment_Status_Update>());
            }
        }

        // Method xuất Excel cho Status Update
        public async Task<IActionResult> ExportStatusUpdateToExcel(DateTime? filterDate = null, string operation = "")
        {
            try
            {
                var query = _context.SVN_Equipment_Status_Update.AsQueryable();

                if (filterDate.HasValue)
                {
                    query = query.Where(x => x.Datetime.Date == filterDate.Value.Date);
                }

                if (!string.IsNullOrEmpty(operation))
                {
                    query = query.Where(x => x.Operation.Contains(operation));
                }

                var data = await query
                    .OrderByDescending(x => x.Datetime)
                    .ThenBy(x => x.Name)
                    .ToListAsync();

                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("StatusUpdateReport");
                    var currentRow = 1;

                    // Font mặc định
                    ws.Style.Font.FontName = "Times New Roman";
                    ws.Style.Font.FontSize = 11;

                    // Header
                    string[] headers = { "Id", "Name", "Operation", "Start Time", "Duration (min)", "Total Downtime (min)", "Date" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = ws.Cell(currentRow, i + 1);
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Accent1, 0.5);
                        cell.Style.Font.FontColor = XLColor.White;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    }

                    // Data rows
                    foreach (var item in data)
                    {
                        currentRow++;
                        ws.Cell(currentRow, 1).Value = item.Id;
                        ws.Cell(currentRow, 2).Value = item.Name;
                        ws.Cell(currentRow, 3).Value = item.Operation;
                        ws.Cell(currentRow, 4).Value = item.StartTime;
                        ws.Cell(currentRow, 5).Value = Math.Round(item.Duration, 2);
                        ws.Cell(currentRow, 6).Value = Math.Round(item.TotalDuration, 2);
                        ws.Cell(currentRow, 7).Value = item.Datetime.ToString("yyyyMMdd");
                    }

                    // Styling
                    ws.Columns(1, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Columns(1, 7).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    // Column widths
                    ws.Column(1).Width = 8;   // Id
                    ws.Column(2).Width = 15;  // Name
                    ws.Column(3).Width = 20;  // Operation
                    ws.Column(4).Width = 20;  // Start Time
                    ws.Column(5).Width = 15;  // Duration
                    ws.Column(6).Width = 18;  // Total Downtime
                    ws.Column(7).Width = 12;  // Date

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        return File(stream.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"StatusUpdateReport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi ExportStatusUpdateToExcel: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi xuất Excel: {ex.Message}" });
            }
        }

        // API endpoint để xử lý dữ liệu cho ngày cụ thể
        [HttpPost]
        public async Task<IActionResult> ProcessDataForDate([FromBody] ProcessDateRequest request)
        {
            try
            {
                DateTime? filterDate = null;
                if (!string.IsNullOrEmpty(request.Date))
                {
                    if (DateTime.TryParse(request.Date, out DateTime parsedDate))
                    {
                        filterDate = parsedDate;
                    }
                }

                await _statusUpdateService.ProcessDataToStatusUpdate(filterDate);
                return Json(new { success = true, message = "Đã xử lý thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // Method hiển thị kết quả nhập trạng thái
        public async Task<IActionResult> Result(string code = "", string state = "", string operation = "", string fromInsDateTime = "", string toInsDateTime = "", int page = 1, int pageSize = 25)
        {
            try
            {
                var query = _context.SVN_Equipment_Info_History_Test.AsQueryable();

                // Apply filter

                if (!string.IsNullOrEmpty(code))
                    query = query.Where(x => x.Code.Contains(code));

                if (!string.IsNullOrEmpty(state))
                    query = query.Where(x => x.State.Contains(state));

                if (!string.IsNullOrEmpty(operation))
                    query = query.Where(x => x.Operation.Contains(operation));

                if (!string.IsNullOrEmpty(fromInsDateTime) && DateTime.TryParse(fromInsDateTime, out var fromDate))
                {
                    query = query.Where(x => x.Datetime.HasValue && x.Datetime.Value.Date >= fromDate.Date);
                }

                if (!string.IsNullOrEmpty(toInsDateTime) && DateTime.TryParse(toInsDateTime, out var toDate))
                {
                    query = query.Where(x => x.Datetime.HasValue && x.Datetime.Value.Date <= toDate.Date);
                }

                var totalRecords = await query.CountAsync();

                var results = await query
                    .OrderByDescending(x => x.Datetime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .AsNoTracking()
                    .ToListAsync();

                var totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalRecords = totalRecords;
                ViewBag.HasPreviousPage = page > 1;
                ViewBag.HasNextPage = page < totalPages;

                // Truyền giá trị filter ra View

                ViewBag.Code = code ?? "";
                ViewBag.State = state ?? "";
                ViewBag.Operation = operation ?? "";
                ViewBag.fromInsDateTime = fromInsDateTime ?? "";
                ViewBag.toInsDateTime = toInsDateTime ?? "";

                return View(results);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi: {ex.Message}";
                ViewBag.Code = code ?? "";
                ViewBag.State = state ?? "";
                ViewBag.Operation = operation ?? "";
                ViewBag.fromInsDateTime = fromInsDateTime ?? "";
                ViewBag.toInsDateTime = toInsDateTime ?? "";

                // Set default pagination values for error case
                ViewBag.CurrentPage = 1;
                ViewBag.TotalPages = 0;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalRecords = 0;
                ViewBag.HasPreviousPage = false;
                ViewBag.HasNextPage = false;

                return View(new List<SVN_Equipment_Info_History_Test>());
            }
        }

        // Xuất File Excel kết quả
        public async Task<IActionResult> ExportToExcel(string code = "", string state = "", string operation = "", string fromInsDateTime = "", string toInsDateTime = "")
        {
            var query = _context.SVN_Equipment_Info_History_Test.AsQueryable();

            if (!string.IsNullOrEmpty(code))
                query = query.Where(x => x.Code.Contains(code));

            if (!string.IsNullOrEmpty(state))
                query = query.Where(x => x.State.Contains(state));

            if (!string.IsNullOrEmpty(operation))
                query = query.Where(x => x.Operation.Contains(operation));

            if (!string.IsNullOrEmpty(fromInsDateTime) && DateTime.TryParse(fromInsDateTime, out var fromDate))
            {
                query = query.Where(x => x.Datetime.HasValue && x.Datetime.Value.Date >= fromDate.Date);
            }

            if (!string.IsNullOrEmpty(toInsDateTime) && DateTime.TryParse(toInsDateTime, out var toDate))
            {
                query = query.Where(x => x.Datetime.HasValue && x.Datetime.Value.Date <= toDate.Date);
            }

            // Sắp xếp bản ghi theo thời gian ASC
            var data = await query.OrderBy(x => x.Datetime).ToListAsync();

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("StatusHistory");
                var currentRow = 1;

                // Font mặc định
                ws.Style.Font.FontName = "Times New Roman";
                ws.Style.Font.FontSize = 11;

                // Header
                string[] headers = { "Id", "Code", "Name", "State", "Operation", "Description", "Image", "Datetime" };
                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(currentRow, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Accent1, 0.5);
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                }

                // Thiết lập chiều cao hàng cho data (để ảnh hiển thị đẹp)
                const double rowHeight = 70;

                foreach (var item in data)
                {
                    currentRow++;
                    ws.Row(currentRow).Height = rowHeight;
                    ws.Cell(currentRow, 1).Value = item.Id;
                    ws.Cell(currentRow, 2).Value = item.Code;
                    ws.Cell(currentRow, 3).Value = item.Name;
                    ws.Cell(currentRow, 4).Value = item.State;
                    ws.Cell(currentRow, 5).Value = item.Operation;
                    ws.Cell(currentRow, 6).Value = item.Description;

                    if (!string.IsNullOrEmpty(item.Image))
                    {
                        try
                        {
                            string imagePath = "";
                            if (item.Image.StartsWith("/uploads/"))
                            {
                                imagePath = Path.Combine(_webHostEnvironment.WebRootPath, item.Image.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                            }
                            else
                            {
                                imagePath = item.Image;
                            }

                            if (System.IO.File.Exists(imagePath))
                            {

                                var picture = ws.AddPicture(imagePath);
                                picture.MoveTo(ws.Cell(currentRow, 7), 8, 5);
                                picture.WithSize(100, 70);


                                var imageCell = ws.Cell(currentRow, 7);
                                imageCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                imageCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                            }
                            else
                            {

                                ws.Cell(currentRow, 7).Value = "No image";
                                ws.Cell(currentRow, 7).Style.Font.FontColor = XLColor.Gray;
                            }
                        }
                        catch (Exception ex)
                        {

                            ws.Cell(currentRow, 7).Value = $"Error: {ex.Message}";
                            ws.Cell(currentRow, 7).Style.Font.FontColor = XLColor.Red;
                        }
                    }
                    else
                    {
                        ws.Cell(currentRow, 7).Value = "No image";
                        ws.Cell(currentRow, 7).Style.Font.FontColor = XLColor.Gray;
                    }
                    ws.Cell(currentRow, 8).Value = item.Datetime?.ToString("yyyy-MM-dd HH:mm:ss");
                }

                // Canh giữa các cột số và ngày
                ws.Columns(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Columns(2, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Columns(3, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Columns(4, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Columns(5, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Columns(7, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                ws.Column(1).Width = 8;
                ws.Column(2).Width = 15;
                ws.Column(3).Width = 15;
                ws.Column(4).Width = 15;
                ws.Column(5).Width = 15;
                ws.Column(6).Width = 15;
                ws.Column(7).Width = 15;
                ws.Column(8).Width = 18;

                using (var stream = new MemoryStream())
                {

                    workbook.SaveAs(stream);
                    return File(stream.ToArray(),
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "StatusHistory.xlsx");
                }

            }

        }


        public async Task<IActionResult> DowntimeDetailReport(string code = "",string state = "",string operation = "",string fromInsDateTime = "",string toInsDateTime = "",int page = 1,int pageSize = 25)
        {
            try
            {
                IQueryable<SVN_Equipment_Status_Update_Detail> query = _context.SVN_Equipment_Status_Update_Detail;

                // Lọc theo Code
                if (!string.IsNullOrEmpty(code))
                {
                    query = query.Where(x => x.Name.Contains(code));
                }

                // Lọc theo State
                if (!string.IsNullOrEmpty(state))
                {
                    query = query.Where(x => x.State.Contains(state));
                }

                // Lọc theo Operation
                if (!string.IsNullOrEmpty(operation))
                {
                    query = query.Where(x => x.Operation.Contains(operation));
                }

                // Lọc theo khoảng thời gian
                if (!string.IsNullOrEmpty(fromInsDateTime) && DateTime.TryParse(fromInsDateTime, out DateTime fromDate))
                {
                    query = query.Where(x => x.FromTime.CompareTo(fromDate.ToString("yyyy-MM-dd HH:mm:ss")) >= 0);
                }

                if (!string.IsNullOrEmpty(toInsDateTime) && DateTime.TryParse(toInsDateTime, out DateTime toDate))
                {
                    query = query.Where(x => x.FromTime.CompareTo(toDate.ToString("yyyy-MM-dd HH:mm:ss")) <= 0);
                }

                // Lấy tổng số bản ghi
                var totalRecords = await query.CountAsync();
                var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

                // Áp dụng phân trang
                var pagedResults = await query
                    .OrderByDescending(x => x.FromTime)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                // Gán dữ liệu vào ViewBag để truyền sang View
                ViewBag.Code = code;
                ViewBag.State = state;
                ViewBag.Operation = operation;
                ViewBag.fromInsDateTime = fromInsDateTime;
                ViewBag.toInsDateTime = toInsDateTime;
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalPages = totalPages;
                ViewBag.TotalRecords = totalRecords;
                ViewBag.HasPreviousPage = page > 1;
                ViewBag.HasNextPage = page < totalPages;

                return View(pagedResults);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi DowntimeDetailReport: {ex.Message}";
                return View(new List<SVN_Equipment_Status_Update_Detail>());
            }
        }

        // Xuất Excel cho Downtime Detail
        public async Task<IActionResult> ExportDowntimeDetailToExcel(string code = "",string state = "",string operation = "",string fromInsDateTime = "",string toInsDateTime = "")
        {
            try
            {
                IQueryable<SVN_Equipment_Status_Update_Detail> query = _context.SVN_Equipment_Status_Update_Detail;

                // Lọc theo Code
                if (!string.IsNullOrEmpty(code))
                    query = query.Where(x => x.Name.Contains(code));

                // Lọc theo State
                if (!string.IsNullOrEmpty(state))
                    query = query.Where(x => x.State.Contains(state));

                // Lọc theo Operation
                if (!string.IsNullOrEmpty(operation))
                    query = query.Where(x => x.Operation.Contains(operation));

                // Lọc theo khoảng thời gian
                if (!string.IsNullOrEmpty(fromInsDateTime) && DateTime.TryParse(fromInsDateTime, out DateTime fromDate))
                {
                    query = query.Where(x => x.FromTime.CompareTo(fromDate.ToString("yyyy-MM-dd HH:mm:ss")) >= 0);
                }

                if (!string.IsNullOrEmpty(toInsDateTime) && DateTime.TryParse(toInsDateTime, out DateTime toDate))
                {
                    query = query.Where(x => x.FromTime.CompareTo(toDate.ToString("yyyy-MM-dd HH:mm:ss")) <= 0);
                }

                var data = await query.OrderByDescending(x => x.FromTime).ToListAsync();

                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("DowntimeDetail");
                    var currentRow = 1;

                    // Font mặc định
                    ws.Style.Font.FontName = "Times New Roman";
                    ws.Style.Font.FontSize = 11;

                    // Header
                    string[] headers = { "Id", "Name", "Operation", "State", "Estimate Time (min)", "From Time", "To Time (min)", "Duration (min)" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = ws.Cell(currentRow, i + 1);
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Accent1, 0.5);
                        cell.Style.Font.FontColor = XLColor.White;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    }

                    // Data rows
                    foreach (var item in data)
                    {
                        currentRow++;
                        ws.Cell(currentRow, 1).Value = item.Id;
                        ws.Cell(currentRow, 2).Value = item.Name;
                        ws.Cell(currentRow, 3).Value = item.Operation;
                        ws.Cell(currentRow, 4).Value = item.State;

                        // Estimate Time
                        if (!string.IsNullOrEmpty(item.EstimateTime) && double.TryParse(item.EstimateTime, out double estimateMinutes))
                        {
                            ws.Cell(currentRow, 5).Value = Math.Round(estimateMinutes, 1);
                            ws.Cell(currentRow, 5).Style.NumberFormat.Format = "0.0";
                        }
                        else
                        {
                            ws.Cell(currentRow, 5).Value = ""; // để trống
                        }

                        // FromTime
                        if (!string.IsNullOrEmpty(item.FromTime))
                        {
                            ws.Cell(currentRow, 6).Value = item.FromTime;
                        }
                        else
                        {
                            ws.Cell(currentRow, 6).Value = "";
                        }

                        // ToTime
                        if (!string.IsNullOrEmpty(item.ToTime) && double.TryParse(item.ToTime, out double toTimeMinutes))
                        {
                            ws.Cell(currentRow, 7).Value = Math.Round(toTimeMinutes, 1);
                            ws.Cell(currentRow, 7).Style.NumberFormat.Format = "0.0";
                        }
                        else
                        {
                            ws.Cell(currentRow, 7).Value = "";
                        }

                        // Duration
                        if (item.DurationMinutes > 0)
                        {
                            ws.Cell(currentRow, 8).Value = Math.Round(item.DurationMinutes, 1);
                            ws.Cell(currentRow, 8).Style.NumberFormat.Format = "0.0";
                        }
                        else
                        {
                            ws.Cell(currentRow, 8).Value = "";
                        }
                    }
                    // Styling
                    ws.Columns().AdjustToContents();

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        return File(stream.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"DowntimeDetail_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi ExportDowntimeDetailToExcel: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi xuất Excel: {ex.Message}" });
            }
        }




        /*============================================================ Downtime ========================================================================**/


        /* API trả về danh sách SVN Code đã nhập (distinct) */
        [HttpGet]
        public async Task<IActionResult> GetSvnCodeOptions()
        {
            var codes = await _context.SVN_Downtime_Infos
                .AsNoTracking()
                .Where(x => x.SVNCode != null && x.SVNCode != "")
                .Select(x => x.SVNCode)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
            return Json(codes);
        }

        /* Hàm GET Nhập Downtime */
        [HttpGet]
        public async Task<IActionResult> CreateDownTime()
        {
            var today = DateTime.Now.ToString("yyyyMMdd");
            var ops = await _context.SVN_targets
                .AsNoTracking()
                .Where(x => x.Date_time == today 
                && x.Operation != null 
                && x.Operation != ""
                && !x.Operation.StartsWith("Sakura")
                && !x.Operation.Contains("(SM)")
                )
                .Select(x => x.Operation)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();

            ViewBag.OperationOptions = ops;

            var rea = await _context.SVN_Downtime_Reasons
                .AsNoTracking()
                .OrderBy(r => r.Reason_Name)
                .Select(r => new { r.Reason_Code, r.Reason_Name })
                .ToListAsync();

            ViewBag.ReasonOptions = rea;
            return View("CreateDownTime"); // <- chỉ rõ, khớp file .cshtml
        }

        /* Hàm GET Lịch sử Downtime */
        [HttpGet]
        public async Task<IActionResult> DowntimeList(string operation = "", string fromDate = "", string toDate = "",int page = 1, int pageSize = 25)
        {
            try
            {
                // JOIN với bảng Reasons để lấy ErrorName
                var query = from d in _context.SVN_Downtime_Infos
                            join r in _context.SVN_Downtime_Reasons
                            on d.ISS_Code equals r.Reason_Code into reasons
                            from r in reasons.DefaultIfEmpty()
                            select new SVN_Downtime_Info
                            {
                                Id = d.Id,
                                Code = d.Code,
                                SVNCode = d.SVNCode,
                                Name = d.Name,
                                Operation = d.Operation,
                                State = d.State,
                                ISS_Code = d.ISS_Code,
                                ErrorName = r != null ? r.Reason_Name : "", // Lấy Reason_Name
                                Description = d.Description,
                                Datetime = d.Datetime,
                                EstimateTime = d.EstimateTime,
                                Image = d.Image
                            };

                // ----- Filters -----
                if (!string.IsNullOrWhiteSpace(operation))
                {
                    var op = operation.Trim();
                    query = query.Where(x => x.Operation != null && x.Operation.Contains(op));
                }

                if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var from))
                {
                    query = query.Where(x => x.Datetime.HasValue && x.Datetime.Value.Date >= from.Date);
                }

                if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var to))
                {
                    query = query.Where(x => x.Datetime.HasValue && x.Datetime.Value.Date <= to.Date);
                }

                // ----- Pagination -----
                var totalRecords = await query.CountAsync();
                var results = await query
                    .OrderByDescending(x => x.Datetime)
                    .ThenBy(x => x.Operation)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var totalPages = (int)Math.Ceiling((double)totalRecords / pageSize);

                // Distinct operations cho dropdown filter, lọc theo date nếu có
                var opsQuery = _context.SVN_Downtime_Infos
                    .Where(x => x.Operation != null && x.Operation != "");
                if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var fromOp))
                    opsQuery = opsQuery.Where(x => x.Datetime.HasValue && x.Datetime.Value.Date >= fromOp.Date);
                if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var toOp))
                    opsQuery = opsQuery.Where(x => x.Datetime.HasValue && x.Datetime.Value.Date <= toOp.Date);
                ViewBag.OperationOptions = await opsQuery
                    .Select(x => x.Operation!)
                    .Distinct()
                    .OrderBy(x => x)
                    .ToListAsync();

                // Pass filter & pagination to View
                ViewBag.Operation = operation ?? "";
                ViewBag.FromDate = fromDate ?? "";
                ViewBag.ToDate = toDate ?? "";
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalRecords = totalRecords;
                ViewBag.HasPreviousPage = page > 1;
                ViewBag.HasNextPage = page < totalPages;

                return View(results);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi: {ex.Message}";
                // Defaults khi lỗi
                ViewBag.Operation = operation ?? "";
                ViewBag.FromDate = fromDate ?? "";
                ViewBag.ToDate = toDate ?? "";
                ViewBag.CurrentPage = 1;
                ViewBag.TotalPages = 0;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalRecords = 0;
                ViewBag.HasPreviousPage = false;
                ViewBag.HasNextPage = false;

                return View(new List<SVN_Downtime_Info>());
            }
        }

        // AJAX: lấy danh sách Operation theo khoảng ngày
        [HttpGet]
        public async Task<IActionResult> GetOperationsByDate(string fromDate = "", string toDate = "")
        {
            var query = _context.SVN_Downtime_Infos
                .Where(x => x.Operation != null && x.Operation != "");
            if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var from))
                query = query.Where(x => x.Datetime.HasValue && x.Datetime.Value.Date >= from.Date);
            if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var to))
                query = query.Where(x => x.Datetime.HasValue && x.Datetime.Value.Date <= to.Date);
            var ops = await query.Select(x => x.Operation!).Distinct().OrderBy(x => x).ToListAsync();
            return Json(ops);
        }

        /* Dashboard: bản ghi Stop trong 1 ngày mà vẫn chưa có Run tương ứng (đang thực sự dừng máy) */
        [HttpGet]
        public async Task<IActionResult> PendingStopsDashboard(string date = "")
        {
            var selectedDate = DateTime.TryParse(date, out var d) ? d.Date : DateTime.Now.Date;
            var now = DateTime.Now;

            var stopsRaw = await _context.SVN_Downtime_Infos
                .Where(x => x.State == "Stop" && x.Datetime.HasValue && x.Datetime.Value.Date == selectedDate)
                .OrderBy(x => x.Datetime)
                .ToListAsync();

            // Chỉ giữ lần Stop MỚI NHẤT của mỗi Operation trong ngày —
            // tránh hiện trùng khi 1 operation Stop nhiều lần cùng ngày mà chưa Run lần nào.
            var stops = stopsRaw
                .GroupBy(s => s.Operation)
                .Select(g => g.OrderByDescending(x => x.Datetime).First())
                .ToList();

            // Ghép cặp Stop→Run theo Operation (không theo SVNCode, vì SVNCode hay bị gõ nhầm khi nhập liệu).
            var laterRuns = new List<SVN_Downtime_Info>();
            if (stops.Count > 0)
            {
                var operations = stops.Select(s => s.Operation).Distinct().ToList();
                var minStopTime = stops.Min(s => s.Datetime!.Value);

                laterRuns = await _context.SVN_Downtime_Infos
                    .Where(x => x.State == "Run"
                             && x.Datetime.HasValue
                             && x.Datetime.Value > minStopTime
                             && operations.Contains(x.Operation))
                    .ToListAsync();
            }

            var reasonDict = (await _context.SVN_Downtime_Reasons.AsNoTracking().ToListAsync())
                .GroupBy(r => r.Reason_Code)
                .ToDictionary(g => g.Key, g => g.First().Reason_Name);

            // Cho các Stop KHÔNG có EstimateTime thủ công: lấy sẵn dòng Target (day + night) của
            // các Operation liên quan, theo "ngày sản xuất" (không phải ngày dương lịch của Stop —
            // Stop xảy ra trước 8h sáng vẫn thuộc ngày sản xuất hôm trước, xem ProductionScheduleHelper),
            // để tính mốc "hết giờ SX theo kế hoạch" làm dự phòng.
            var missingEstimateStops = stops
                .Where(s => string.IsNullOrWhiteSpace(s.EstimateTime) && s.Operation != null && s.Datetime.HasValue)
                .ToList();

            var targetRowsByDateAndOperation = new Dictionary<(string DateStr, string Operation), List<SVN_Production_result_Viindoo>>();
            if (missingEstimateStops.Count > 0)
            {
                var opsByProdDay = missingEstimateStops
                    .GroupBy(s => ProductionScheduleHelper.GetProductionDay(s.Datetime!.Value))
                    .ToDictionary(g => g.Key, g => g.Select(s => s.Operation!).Distinct().ToList());

                var allTargetRows = new List<SVN_Production_result_Viindoo>();
                foreach (var (prodDay, ops) in opsByProdDay)
                {
                    var dateStr = prodDay.ToString("yyyyMMdd");
                    var rows = await _context.SVN_Production_result_Viindoo
                        .Where(r => r.Type_value == "Target"
                                 && r.Date_time == dateStr
                                 && r.Operation != null
                                 && ops.Contains(r.Operation))
                        .ToListAsync();
                    allTargetRows.AddRange(rows);
                }

                targetRowsByDateAndOperation = allTargetRows
                    .Where(r => r.Operation != null)
                    .GroupBy(r => (r.Date_time ?? "", r.Operation!))
                    .ToDictionary(g => g.Key, g => g.ToList());
            }

            var pending = new List<PendingStopItem>();
            foreach (var s in stops)
            {
                bool resolved = laterRuns.Any(r => r.Operation == s.Operation
                                                 && r.Datetime > s.Datetime);
                if (resolved) continue;

                DateTime? estimateAt = null;
                bool overdue = false;
                bool isAutoEstimated = false;

                if (!string.IsNullOrWhiteSpace(s.EstimateTime) && TimeSpan.TryParse(s.EstimateTime, out var span))
                {
                    var at = s.Datetime!.Value.Date.AddHours(span.Hours).AddMinutes(span.Minutes);
                    if (at <= s.Datetime!.Value) at = at.AddDays(1);
                    estimateAt = at;
                    overdue = now > at;
                }
                else if (s.Operation != null && s.Datetime.HasValue)
                {
                    var prodDay = ProductionScheduleHelper.GetProductionDay(s.Datetime.Value);
                    var dateStr = prodDay.ToString("yyyyMMdd");
                    if (targetRowsByDateAndOperation.TryGetValue((dateStr, s.Operation), out var rows))
                    {
                        var fallback = ProductionScheduleHelper.CalculateEndOfScheduledProduction(prodDay, rows);
                        if (fallback.HasValue)
                        {
                            estimateAt = fallback;
                            overdue = now > fallback.Value;
                            isAutoEstimated = true;
                        }
                    }
                }

                pending.Add(new PendingStopItem
                {
                    Id = s.Id,
                    SVNCode = s.SVNCode,
                    Operation = s.Operation,
                    ISS_Code = s.ISS_Code,
                    ErrorName = !string.IsNullOrEmpty(s.ISS_Code) && reasonDict.TryGetValue(s.ISS_Code, out var rn) ? rn : "",
                    Description = s.Description,
                    StopDatetime = s.Datetime,
                    EstimateTime = s.EstimateTime,
                    EstimateAt = estimateAt,
                    AutoRunEnabled = s.AutoRunEnabled,
                    IsOverdue = overdue,
                    IsAutoEstimated = isAutoEstimated,
                    Duration = now - s.Datetime!.Value
                });
            }

            pending = pending.OrderBy(p => p.StopDatetime).ToList();

            ViewBag.SelectedDate = selectedDate.ToString("yyyy-MM-dd");
            ViewBag.TotalPending = pending.Count;
            ViewBag.OverdueCount = pending.Count(p => p.IsOverdue);
            ViewBag.GeneratedAt = now;

            return View(pending);
        }

        // Xuất Excel DowntimeList với ErrorName
        public async Task<IActionResult> ExportDowntimeListToExcel(string operation = "", string fromDate = "", string toDate = "")
        {
            try
            {
                // JOIN với bảng Reasons để lấy ErrorName
                var query = from d in _context.SVN_Downtime_Infos
                            join r in _context.SVN_Downtime_Reasons
                            on d.ISS_Code equals r.Reason_Code into reasons
                            from r in reasons.DefaultIfEmpty()
                            select new SVN_Downtime_Info
                            {
                                Id = d.Id,
                                SVNCode = d.SVNCode,
                                Code = d.Code,
                                Name = d.Name,
                                Operation = d.Operation,
                                State = d.State,
                                ISS_Code = d.ISS_Code,
                                ErrorName = r != null ? r.Reason_Name : "",
                                Description = d.Description,
                                Datetime = d.Datetime,
                                EstimateTime = d.EstimateTime,
                                Image = d.Image
                            };

                // Apply filters
                if (!string.IsNullOrWhiteSpace(operation))
                {
                    var op = operation.Trim();
                    query = query.Where(x => x.Operation != null && x.Operation.Contains(op));
                }

                if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var from))
                {
                    query = query.Where(x => x.Datetime.HasValue && x.Datetime.Value.Date >= from.Date);
                }

                if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var to))
                {
                    query = query.Where(x => x.Datetime.HasValue && x.Datetime.Value.Date <= to.Date);
                }

                var data = await query
                    .OrderByDescending(x => x.Datetime)
                    .ThenBy(x => x.Operation)
                    .ToListAsync();

                using (var workbook = new XLWorkbook())
                {
                    var ws = workbook.Worksheets.Add("DowntimeList");
                    var currentRow = 1;

                    // Font mặc định
                    ws.Style.Font.FontName = "Times New Roman";
                    ws.Style.Font.FontSize = 11;

                    // Header
                    string[] headers = { "#", "SVN Code", "Operation", "ISS Code", "Tên lỗi", "State", "Mô tả", "Thời gian", "Ước tính", "Ảnh" };
                    for (int i = 0; i < headers.Length; i++)
                    {
                        var cell = ws.Cell(currentRow, i + 1);
                        cell.Value = headers[i];
                        cell.Style.Font.Bold = true;
                        cell.Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Accent1, 0.5);
                        cell.Style.Font.FontColor = XLColor.White;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    }

                    const double rowHeight = 70;

                    int rowIndex = 0;
                    foreach (var item in data)
                    {
                        currentRow++;
                        rowIndex++;
                        ws.Row(currentRow).Height = rowHeight;

                        ws.Cell(currentRow, 1).Value = rowIndex;
                        ws.Cell(currentRow, 2).Value = item.SVNCode;
                        ws.Cell(currentRow, 3).Value = item.Operation;
                        ws.Cell(currentRow, 4).Value = item.ISS_Code;
                        ws.Cell(currentRow, 5).Value = item.ErrorName;
                        ws.Cell(currentRow, 6).Value = item.State;
                        ws.Cell(currentRow, 7).Value = item.Description;
                        ws.Cell(currentRow, 8).Value = item.Datetime?.ToString("dd/MM/yyyy HH:mm") ?? "-";
                        ws.Cell(currentRow, 9).Value = string.IsNullOrEmpty(item.EstimateTime) ? "-" : item.EstimateTime;

                        // Xử lý ảnh
                        if (!string.IsNullOrEmpty(item.Image))
                        {
                            try
                            {
                                string imagePath = "";
                                if (item.Image.StartsWith("/uploads/"))
                                {
                                    imagePath = Path.Combine(_webHostEnvironment.WebRootPath, item.Image.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                                }
                                else
                                {
                                    imagePath = item.Image;
                                }

                                if (System.IO.File.Exists(imagePath))
                                {
                                    var picture = ws.AddPicture(imagePath);
                                    picture.MoveTo(ws.Cell(currentRow, 10), 8, 5);
                                    picture.WithSize(100, 70);

                                    var imageCell = ws.Cell(currentRow, 10);
                                    imageCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                                    imageCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                                }
                                else
                                {
                                    ws.Cell(currentRow, 10).Value = "No image";
                                    ws.Cell(currentRow, 10).Style.Font.FontColor = XLColor.Gray;
                                }
                            }
                            catch (Exception ex)
                            {
                                ws.Cell(currentRow, 10).Value = $"Error: {ex.Message}";
                                ws.Cell(currentRow, 10).Style.Font.FontColor = XLColor.Red;
                            }
                        }
                        else
                        {
                            ws.Cell(currentRow, 10).Value = "-";
                            ws.Cell(currentRow, 10).Style.Font.FontColor = XLColor.Gray;
                        }
                    }

                    // Styling
                    ws.Columns(1, 9).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    ws.Columns(1, 9).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    ws.Column(7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left; // Mô tả căn trái

                    // Column widths
                    ws.Column(1).Width = 8;
                    ws.Column(2).Width = 15;
                    ws.Column(3).Width = 20;
                    ws.Column(4).Width = 15;
                    ws.Column(5).Width = 25;
                    ws.Column(6).Width = 12;
                    ws.Column(7).Width = 30;
                    ws.Column(8).Width = 18;
                    ws.Column(9).Width = 15;
                    ws.Column(10).Width = 15;

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        return File(stream.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"DowntimeList_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi ExportDowntimeListToExcel: {ex.Message}");
                return Json(new { success = false, message = $"Lỗi xuất Excel: {ex.Message}" });
            }
        }

        /* Hàm POST Nhập Downtime */
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDownTime(SVN_Downtime_Info model, IFormFile? imageFile)
        {
            // ===== 1) Chuẩn hoá/điền mặc định =====
            if (!string.IsNullOrWhiteSpace(model.SVNCode))
                model.SVNCode = model.SVNCode.ToUpper().Replace(" ", "");

            if (string.IsNullOrWhiteSpace(model.Code))
                model.Code = model.Operation ?? string.Empty;

            if (string.IsNullOrWhiteSpace(model.Name))
                model.Name = model.Operation ?? string.Empty;

            if (!model.Datetime.HasValue || model.Datetime.Value == default)
                model.Datetime = DateTime.Now;

            if (string.IsNullOrWhiteSpace(model.EstimateTime))
                model.EstimateTime = string.Empty;

            if (string.IsNullOrWhiteSpace(model.Description))
                model.Description = string.Equals(model.State, "Run", StringComparison.OrdinalIgnoreCase)
                    ? null
                    : string.Empty;

            // ✅ Đọc AutoRunHint từ form
            var autoRunHint = Request.Form["AutoRunHint"].FirstOrDefault();
            model.AutoRunEnabled = autoRunHint == "1";

            var autoRunDesc = Request.Form["AutoRunDescription"].FirstOrDefault();
            model.AutoRunDescription = string.IsNullOrWhiteSpace(autoRunDesc) ? null : autoRunDesc.Trim();

            // ===== 2) Xử lý upload ảnh (tuỳ chọn) =====
            string imagePath = string.Empty;
            if (imageFile != null && imageFile.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(ext))
                    return Json(new { success = false, message = "Chỉ cho phép upload ảnh: jpg, jpeg, png, gif, bmp" });

                if (imageFile.Length > 5 * 1024 * 1024)
                    return Json(new { success = false, message = "Kích thước ảnh không được vượt quá 5MB" });

                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "status-images");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                imagePath = $"/uploads/status-images/{fileName}";
            }

            model.Image = imagePath;

            // ===== 3) Validate ModelState & Lưu DB =====
            if (!ModelState.IsValid)
            {
                await RefillOpsForToday();
                await RefillReasonsAsync();
                TempData["Error"] = "Dữ liệu không hợp lệ!";
                return View("CreateDownTime", model);
            }

            // ✅ Nếu người dùng Run thủ công, huỷ auto-run đang chờ (nếu có) của Stop gần nhất
            // cùng SVNCode+Operation, tránh bị hệ thống tự chèn thêm 1 Run trùng lúc tới giờ ước tính.
            if (string.Equals(model.State, "Run", StringComparison.OrdinalIgnoreCase))
            {
                var pendingStop = await _context.SVN_Downtime_Infos
                    .Where(x => x.State == "Stop"
                             && x.AutoRunEnabled == true
                             && x.IsAutoRunExecuted == false
                             && x.SVNCode == model.SVNCode
                             && x.Operation == model.Operation
                             && x.Datetime <= model.Datetime)
                    .OrderByDescending(x => x.Datetime)
                    .FirstOrDefaultAsync();

                if (pendingStop != null)
                    pendingStop.IsAutoRunExecuted = true;
            }

            _context.SVN_Downtime_Infos.Add(model);
            await _context.SaveChangesAsync();

            // ===== 4) Trả JSON cho AJAX =====
            return Json(new { success = true, message = "Đã lưu downtime!" });
        }


        /* Hàm fill danh sách dropdown mã lỗi ISS Code - Error Reason */
        private async Task RefillReasonsAsync()
        {
            ViewBag.ReasonOptions = await _context.SVN_Downtime_Reasons
                .AsNoTracking()
                .OrderBy(r => r.Reason_Name)
                .Select(r => new { r.Reason_Code, r.Reason_Name })
                .ToListAsync();
        }

        /* Hàm fill danh sách Operation đang chạy trong ngày hôm nay */
        private async Task RefillOpsForToday()
        {
            var today = DateTime.Now.ToString("yyyyMMdd");
            ViewBag.OperationOptions = await _context.SVN_targets
                .AsNoTracking()
                .Where(x => x.Date_time == today && x.Operation != null && x.Operation != "" && !x.Operation.Contains("(SM)"))
                .Select(x => x.Operation)
                .Distinct()
                .OrderBy(x => x)
                .ToListAsync();
        }


        /* Hàm báo cáo downtime */
        [HttpGet]
        public async Task<IActionResult> ReportDowntime(string fromDate = "", string toDate = "", int page = 1, int pageSize = 10)
        {
            try
            {
                var allData = await GetDowntimeReportData(fromDate, toDate);

                // Phân trang CHỈ cho danh sách operation
                var totalRecords = allData.Count;
                var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

                // Đảm bảo page không vượt quá totalPages
                page = Math.Max(1, Math.Min(page, totalPages));

                var pagedData = allData
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;

                // Chart data vẫn dùng TOÀN BỘ dữ liệu

                ViewBag.ChartData = PrepareChartData(allData);
                ViewBag.IssCodeChartData = PrepareIssCodeChartData(allData);
                ViewBag.DailyChartData = PrepareDailyDowntimeChartData(allData, fromDate, toDate);
                ViewBag.RunningTimeByProjectChartData = await PrepareRunningTimeByProjectChartData(fromDate, toDate);
                ViewBag.HeatmapData = PrepareHeatmapDowntimeData(fromDate, toDate);

                // Truyền cả toàn bộ dữ liệu cho thống kê
                ViewBag.AllData = allData;

                // Thêm thông tin phân trang
                ViewBag.CurrentPage = page;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalRecords = totalRecords;
                ViewBag.TotalPages = totalPages;
                ViewBag.HasPreviousPage = page > 1;
                ViewBag.HasNextPage = page < totalPages;

                return View(pagedData);
            }
            catch (Exception ex)
            {
                ViewBag.ErrorMessage = $"Lỗi: {ex.Message}";
                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.CurrentPage = 1;
                ViewBag.PageSize = 10;
                ViewBag.TotalRecords = 0;
                ViewBag.TotalPages = 1;
                ViewBag.HasPreviousPage = false;
                ViewBag.HasNextPage = false;
                return View(new List<DowntimeReportByOperation>());
            }
        }

        /* API JSON: Thời gian chạy / Downtime theo từng dự án của ngày liền trước (N-1) ngày truyền vào */
        [HttpGet]
        [Route("Status/ReportDowntime/runningTimeByProject")]
        public async Task<IActionResult> RunningTimeByProject(string date = "")
        {
            try
            {
                var chartData = await PrepareRunningTimeByProjectChartData(date, date);
                return Json(chartData);
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        /* Hàm chuẩn bị dữ liệu downtime ngày => Chart biểu đồ xu hướng downtime */
        private DailyDowntimeChartData PrepareDailyDowntimeChartData(List<DowntimeReportByOperation> reportData, string fromDate, string toDate)
        {
            var dailyData = new DailyDowntimeChartData
            {
                Dates = new List<string>(),
                DowntimeMinutes = new List<double>(),
                DowntimeCounts = new List<int>()
            };

            try
            {
                // Lấy tất cả records downtime trong khoảng thời gian
                var query = from d in _context.SVN_Downtime_Infos
                            join r in _context.SVN_Downtime_Reasons
                            on d.ISS_Code equals r.Reason_Code into reasons
                            from r in reasons.DefaultIfEmpty()
                            select new
                            {
                                d.Operation,
                                d.State,
                                d.ISS_Code,
                                ErrorName = r != null ? r.Reason_Name : "Chưa xác định",
                                d.Datetime,
                                d.SVNCode
                            };

                // Lọc theo ngày
                if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var from))
                {
                    query = query.Where(x => x.Datetime.HasValue && x.Datetime.Value.Date >= from.Date);
                }

                if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var to))
                {
                    query = query.Where(x => x.Datetime.HasValue && x.Datetime.Value.Date <= to.Date);
                }

                var allRecords = query.ToList();

                // Tính toán downtime theo ngày
                var downtimeByDate = new Dictionary<DateTime, (double Minutes, int Count)>();

                // Nhóm records theo SVNCode và Operation
                var groupedBySVNAndOp = allRecords
                    .Where(x => !string.IsNullOrEmpty(x.Operation) && !string.IsNullOrEmpty(x.SVNCode))
                    .GroupBy(x => new { x.SVNCode, x.Operation });

                foreach (var group in groupedBySVNAndOp)
                {
                    var records = group.OrderBy(x => x.Datetime).ToList();

                    for (int i = 0; i < records.Count - 1; i++)
                    {
                        var current = records[i];
                        var next = records[i + 1];

                        // Tính downtime từ Stop đến Run
                        if (current.State?.Trim().ToUpper() == "STOP" &&
                            next.State?.Trim().ToUpper() == "RUN" &&
                            current.Datetime.HasValue &&
                            next.Datetime.HasValue)
                        {
                            var downtimeMinutes = (next.Datetime.Value - current.Datetime.Value).TotalMinutes;
                            var dateKey = current.Datetime.Value.Date;

                            if (downtimeByDate.ContainsKey(dateKey))
                            {
                                downtimeByDate[dateKey] = (downtimeByDate[dateKey].Minutes + downtimeMinutes,
                                                         downtimeByDate[dateKey].Count + 1);
                            }
                            else
                            {
                                downtimeByDate[dateKey] = (downtimeMinutes, 1);
                            }
                        }
                    }
                }

                // Sắp xếp theo ngày
                var sortedDates = downtimeByDate.Keys.OrderBy(date => date).ToList();

                foreach (var date in sortedDates)
                {
                    dailyData.Dates.Add(date.ToString("dd/MM"));
                    dailyData.DowntimeMinutes.Add(Math.Round(downtimeByDate[date].Minutes, 2));
                    dailyData.DowntimeCounts.Add(downtimeByDate[date].Count);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi PrepareDailyDowntimeChartData: {ex.Message}");
            }

            return dailyData;
        }

        /* Hàm chuẩn bị dữ liệu Heatmap Downtime theo Thứ (Mon..Sun) và khung giờ (2 tiếng/ô) */
        private HeatmapDowntimeData PrepareHeatmapDowntimeData(string fromDate, string toDate)
        {
            // Thứ trong tuần theo thứ tự Mon..Sun, khớp với DayOfWeek của .NET (Sunday = 0)
            var dayNames = new[] { "Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun" };
            var dayOfWeekOrder = new[]
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
            };
            const int hourBucketCount = 24; // 1 giờ mỗi ô: 00h, 01h, 02h, ... 23h

            var heatmap = new HeatmapDowntimeData
            {
                Days = dayNames.ToList(),
                HourLabels = Enumerable.Range(0, hourBucketCount).Select(i => $"{i:D2}h").ToList(),
                Counts = Enumerable.Range(0, 7).Select(_ => Enumerable.Repeat(0, hourBucketCount).ToList()).ToList()
            };

            try
            {
                var query = _context.SVN_Downtime_Infos
                    .Where(d => d.State != null && d.State.Trim().ToUpper() == "STOP" && d.Datetime.HasValue);

                if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var from))
                {
                    query = query.Where(d => d.Datetime.Value.Date >= from.Date);
                }

                if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var to))
                {
                    query = query.Where(d => d.Datetime.Value.Date <= to.Date);
                }

                var stopTimes = query.Select(d => d.Datetime.Value).ToList();

                foreach (var dt in stopTimes)
                {
                    var dayIdx = Array.IndexOf(dayOfWeekOrder, dt.DayOfWeek);
                    heatmap.Counts[dayIdx][dt.Hour]++;
                }

                heatmap.MaxCount = heatmap.Counts.SelectMany(row => row).DefaultIfEmpty(0).Max();
                heatmap.LowThreshold = (int)Math.Ceiling(heatmap.MaxCount / 3.0);
                heatmap.MediumThreshold = (int)Math.Ceiling(heatmap.MaxCount * 2.0 / 3.0);

                // Nếu khoảng ngày lọc ngắn hơn 1 tuần, chỉ hiển thị đúng các Thứ nằm trong khoảng đó
                // (VD: lọc từ Thứ 2 đến Thứ 6 thì cột chỉ hiện Mon..Fri, không hiện Sat/Sun)
                if (!string.IsNullOrEmpty(fromDate) && !string.IsNullOrEmpty(toDate) &&
                    DateTime.TryParse(fromDate, out var rangeFrom) && DateTime.TryParse(toDate, out var rangeTo))
                {
                    var spanDays = (rangeTo.Date - rangeFrom.Date).Days + 1;
                    if (spanDays > 0 && spanDays < 7)
                    {
                        // Trong khoảng < 1 tuần, mỗi Thứ chỉ ứng với đúng 1 ngày cụ thể => gắn kèm ngày/tháng vào label
                        var dateOfDay = new Dictionary<DayOfWeek, DateTime>();
                        for (var d = rangeFrom.Date; d <= rangeTo.Date; d = d.AddDays(1))
                        {
                            if (!dateOfDay.ContainsKey(d.DayOfWeek))
                            {
                                dateOfDay[d.DayOfWeek] = d;
                            }
                        }

                        var activeIdx = Enumerable.Range(0, 7).Where(i => dateOfDay.ContainsKey(dayOfWeekOrder[i])).ToList();
                        heatmap.Days = activeIdx.Select(i => $"{dayNames[i]} {dateOfDay[dayOfWeekOrder[i]]:dd/MM}").ToList();
                        heatmap.Counts = activeIdx.Select(i => heatmap.Counts[i]).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi PrepareHeatmapDowntimeData: {ex.Message}");
            }

            return heatmap;
        }

        /* Hàm lấy dữ liệu downtime trong khoảng STOP -> RUN, sau đó tổng hợp theo Operation và ISS Code */
        private async Task<List<DowntimeReportByOperation>> GetDowntimeReportData(string fromDate, string toDate)
        {
            // Lấy tất cả dữ liệu downtime có JOIN với Reasons
            var query = from d in _context.SVN_Downtime_Infos
                        join r in _context.SVN_Downtime_Reasons
                        on d.ISS_Code equals r.Reason_Code into reasons
                        from r in reasons.DefaultIfEmpty()
                        select new
                        {
                            d.Operation,
                            d.State,
                            d.ISS_Code,
                            ErrorName = r != null ? r.Reason_Name : "Chưa xác định",
                            d.Datetime,
                            d.SVNCode
                        };

            // Lọc theo ngày
            if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var from))
            {
                query = query.Where(x => x.Datetime.HasValue && x.Datetime.Value.Date >= from.Date);
            }

            if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var to))
            {
                query = query.Where(x => x.Datetime.HasValue && x.Datetime.Value.Date <= to.Date);
            }

            var allRecords = await query
                .OrderBy(x => x.SVNCode)
                .ThenBy(x => x.Operation)
                .ThenBy(x => x.Datetime)
                .ToListAsync();

            // Tính toán downtime: từ Stop đến Run, nhóm theo ISS_Code của record Stop
            var downtimeRecords = new List<DowntimeRecord>();

            var groupedBySVNAndOp = allRecords
                .Where(x => !string.IsNullOrEmpty(x.Operation) && !string.IsNullOrEmpty(x.SVNCode))
                .GroupBy(x => new { x.SVNCode, x.Operation });

            foreach (var group in groupedBySVNAndOp)
            {
                var records = group.OrderBy(x => x.Datetime).ToList();

                for (int i = 0; i < records.Count - 1; i++)
                {
                    var current = records[i];
                    var next = records[i + 1];

                    // Tính downtime từ Stop đến Run
                    if (current.State?.Trim().ToUpper() == "STOP" &&
                        next.State?.Trim().ToUpper() == "RUN" &&
                        current.Datetime.HasValue &&
                        next.Datetime.HasValue)
                    {
                        var downtimeMinutes = (next.Datetime.Value - current.Datetime.Value).TotalMinutes;

                        // Lấy ISS_Code và ErrorName từ record STOP (current)
                        var issCode = string.IsNullOrWhiteSpace(current.ISS_Code) ? "N/A" : current.ISS_Code.Trim();
                        var errorName = string.IsNullOrWhiteSpace(current.ErrorName) ? "Không xác định" : current.ErrorName.Trim();

                        downtimeRecords.Add(new DowntimeRecord
                        {
                            Operation = current.Operation.Trim(),
                            ISS_Code = issCode,
                            ErrorName = errorName,
                            DowntimeMinutes = downtimeMinutes
                        });
                    }
                }
            }

            // Nhóm theo Operation và ISS_Code
            var groupedReport = downtimeRecords
                .GroupBy(x => x.Operation)
                .Select(opGroup => new DowntimeReportByOperation
                {
                    Operation = opGroup.Key,
                    TotalDowntimeCount = opGroup.Count(),
                    TotalDowntimeMinutes = opGroup.Sum(x => x.DowntimeMinutes),
                    TotalDowntimeFormatted = FormatMinutesToTime(opGroup.Sum(x => x.DowntimeMinutes)),
                    ErrorDetails = opGroup
                        .GroupBy(x => new { x.ISS_Code, x.ErrorName })
                        .Select(errGroup => new DowntimeReportByOperationError
                        {
                            Operation = opGroup.Key,
                            ISS_Code = errGroup.Key.ISS_Code,
                            ErrorName = errGroup.Key.ErrorName,
                            DowntimeCount = errGroup.Count(),
                            TotalDowntimeMinutes = errGroup.Sum(x => x.DowntimeMinutes),
                            TotalDowntimeFormatted = FormatMinutesToTime(errGroup.Sum(x => x.DowntimeMinutes))
                        })
                        .OrderByDescending(x => x.TotalDowntimeMinutes)
                        .ToList()
                })
                .OrderByDescending(x => x.TotalDowntimeMinutes)
                .ToList();

            return groupedReport;
        }

        /* Hàm chuyển đổi tổng số phút sang chuỗi thời gian */
        private string FormatMinutesToTime(double minutes)
        {
            if (minutes < 0) return "0h 0m";

            int hours = (int)(minutes / 60);
            int mins = (int)(minutes % 60);

            return $"{hours}h {mins}m";
        }


        /* Hàm chuẩn bị dữ liệu downtime theo từng Operation */
        private DowntimeChartData PrepareChartData(List<DowntimeReportByOperation> reportData)
        {
            var chartData = new DowntimeChartData
            {
                Operations = reportData.Select(x => x.Operation).ToList(),
                DowntimeMinutes = reportData.Select(x => Math.Round(x.TotalDowntimeMinutes, 2)).ToList(),
                ErrorBreakdowns = new List<ChartErrorBreakdown>()
            };

            foreach (var op in reportData)
            {
                chartData.ErrorBreakdowns.Add(new ChartErrorBreakdown
                {
                    Operation = op.Operation,
                    ErrorNames = op.ErrorDetails.Select(e => e.ErrorName).ToList(),
                    Minutes = op.ErrorDetails.Select(e => Math.Round(e.TotalDowntimeMinutes, 2)).ToList()
                });
            }

            return chartData;
        }

        /* Hàm tổng hợp và chuẩn bị dữ liệu Downtime theo ISS Code */
        private IssCodeChartData PrepareIssCodeChartData(List<DowntimeReportByOperation> reportData)
        {
            // Tổng hợp tất cả errors từ các operation
            var allErrors = reportData
                .SelectMany(op => op.ErrorDetails)
                .GroupBy(e => new { e.ISS_Code, e.ErrorName })
                .Select(g => new
                {
                    IssCode = g.Key.ISS_Code,
                    ErrorName = g.Key.ErrorName,
                    TotalMinutes = g.Sum(e => e.TotalDowntimeMinutes),
                    Count = g.Sum(e => e.DowntimeCount)
                })
                .OrderByDescending(x => x.TotalMinutes)
                .ToList();

            return new IssCodeChartData
            {
                IssCodeLabels = allErrors.Select(e => $"{e.IssCode} - {e.ErrorName}").ToList(),
                DowntimeMinutes = allErrors.Select(e => Math.Round(e.TotalMinutes, 2)).ToList(),
                DowntimeCounts = allErrors.Select(e => e.Count).ToList()
            };
        }

        /* Hàm gộp Operation thành nhóm Project - dùng chung với logic getNewLabel() phía view (Views/Status/ReportDowntime.cshtml)
           cho biểu đồ "Downtime theo Công đoạn": lấy phần trước dấu '-' đầu tiên, trừ trường hợp đặc biệt Injection_POP => POP */
        private static string GetProjectGroup(string operation)
        {
            if (string.IsNullOrWhiteSpace(operation)) return operation;
            var op = operation.Trim();
            if (op.Contains("Injection_POP")) return "POP";
            var idx = op.IndexOf('-');
            return idx != -1 ? op.Substring(0, idx) : op;
        }

        /* Hàm chuẩn bị dữ liệu Chart "Thời gian chạy của từng dự án".
           Quy tắc N-1: dữ liệu SVN_targets của "hôm nay" luôn coi là CHƯA đầy đủ (được ghi nhận vào cuối buổi/tối hôm đó),
           nên KHÔNG BAO GIỜ lấy vượt quá "hôm qua". Cụ thể:
           - Không chọn filter gì => lấy TỪ TRƯỚC TỚI GIỜ (không giới hạn ngày bắt đầu, giống các chart khác trên trang),
             chặn trên ở "hôm qua".
           - Có chọn fromDate/toDate => dùng ĐÚNG mốc người dùng chọn (không dịch lùi), chỉ hạ mốc "đến ngày" xuống
             "hôm qua" nếu mốc đó rơi vào hôm nay/tương lai (dữ liệu chưa có). Ví dụ hôm nay 13/07, chọn 05/07-12/07
             thì lấy đúng 05/07-12/07 (12/07 đã có dữ liệu đầy đủ từ tối 12/07, không bị hạ xuống 11/07 nữa).
           TotalWorkingTime lấy từ SVN_targets, Downtime lấy từ nguồn downtime hiện có (GetDowntimeReportData), gộp theo Project. */
        private async Task<RunningTimeByProjectChartData> PrepareRunningTimeByProjectChartData(string fromDate, string toDate)
        {
            var result = new RunningTimeByProjectChartData();

            try
            {
                // Chấp nhận cả "yyyy-MM-dd" (input type=date của trang) lẫn "yyyyMMdd" (định dạng Date_time thô)
                // vì endpoint JSON độc lập (RunningTimeByProject) có thể được gọi với ?date=yyyyMMdd.
                var acceptedFormats = new[] { "yyyy-MM-dd", "yyyyMMdd" };
                DateTime? ParseDateOrNull(string input)
                {
                    if (!string.IsNullOrEmpty(input) &&
                        (DateTime.TryParseExact(input, acceptedFormats, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed)
                         || DateTime.TryParse(input, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out parsed)))
                    {
                        return parsed.Date;
                    }
                    return null;
                }

                var yesterday = DateTime.Now.Date.AddDays(-1);
                var selectedFrom = ParseDateOrNull(fromDate);
                var selectedTo = ParseDateOrNull(toDate);
                var noFilterSelected = !selectedFrom.HasValue && !selectedTo.HasValue;

                // Chặn trên ở "hôm qua": chỉ hạ xuống khi mốc đã chọn >= hôm qua (tức rơi vào hôm nay/tương lai)
                var targetTo = (selectedTo.HasValue && selectedTo.Value < yesterday) ? selectedTo.Value : yesterday;

                DateTime? targetFrom;
                if (noFilterSelected)
                {
                    // Mới vào trang, chưa chọn filter gì: mặc định 30 ngày gần nhất (tính đến hôm qua) thay vì toàn
                    // bộ lịch sử, để không kéo theo các dự án đã ngừng chạy từ lâu (vd. Solar, eKIT, Astro, ITA*...)
                    targetFrom = targetTo.AddDays(-29);
                }
                else
                {
                    targetFrom = selectedFrom; // Có chọn filter: giữ đúng mốc người dùng chọn (không dịch lùi)
                }
                if (targetFrom.HasValue && targetFrom.Value > targetTo) targetFrom = targetTo; // phòng filter nhập ngược khoảng

                var targetFromStr = targetFrom?.ToString("yyyyMMdd"); // null = không giới hạn dưới
                var targetToStr = targetTo.ToString("yyyyMMdd");
                result.ReportDateFrom = targetFromStr; // null => hiển thị "Đến ngày ..." ở view (không có mốc bắt đầu)
                result.ReportDateTo = targetToStr;

                // 1. Tổng WorkingTime (giờ) theo Project từ SVN_targets, CỘNG DỒN qua từng ngày trong khoảng [targetFrom, targetTo].
                //    Bảng SVN_target hiện chỉ ~vài nghìn dòng nên lấy hết rồi lọc theo khoảng ngày trong bộ nhớ (so sánh
                //    chuỗi Date_time dạng "yyyyMMdd" cùng độ dài => so sánh ordinal tương đương so sánh thời gian).
                //    Loại các Operation không phải dữ liệu thật (dùng chung quy ước loại trừ "Sakura"/"(SM)" đã có sẵn
                //    ở RefillOpsForToday/GetSvnCodeOptions trong controller này, cộng thêm các dòng "Test..." rõ ràng
                //    là dữ liệu thử nghiệm) - nếu không, khoảng ngày rộng (không giới hạn mốc bắt đầu) sẽ kéo theo
                //    hàng loạt "dự án" rác/nháp không liên quan tới sản xuất thật.
                var allTargets = await _context.SVN_targets
                    .AsNoTracking()
                    .Where(x => x.Operation != null && x.Operation != "" && x.Date_time != null
                             && !x.Operation.StartsWith("Sakura")
                             && !x.Operation.Contains("(SM)")
                             && !x.Operation.StartsWith("Test"))
                    .Select(x => new { x.Operation, x.WorkingTime, x.Date_time })
                    .ToListAsync();

                var targets = allTargets
                    .Where(x => string.CompareOrdinal(x.Date_time, targetToStr) <= 0
                             && (targetFromStr == null || string.CompareOrdinal(x.Date_time, targetFromStr) >= 0))
                    .ToList();

                var workingTimeByProject = targets
                    .GroupBy(x => GetProjectGroup(x.Operation))
                    .ToDictionary(g => g.Key, g => (double)g.Sum(x => x.WorkingTime ?? 0));

                // 2. Downtime (phút) theo Operation từ nguồn downtime hiện có (SVN_Downtime_Infos qua GetDowntimeReportData),
                //    GetDowntimeReportData đã tự cộng dồn theo khoảng ngày truyền vào và coi "" là không giới hạn mốc đó
                //    (giống hệt quy ước fromDate/toDate rỗng của các chart khác trên trang) nên tái dùng thẳng được.
                var downtimeReport = await GetDowntimeReportData(targetFrom?.ToString("yyyy-MM-dd") ?? "", targetTo.ToString("yyyy-MM-dd"));
                var downtimeHoursByProject = downtimeReport
                    .GroupBy(x => GetProjectGroup(x.Operation))
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.TotalDowntimeMinutes) / 60.0);

                // Dự án có downtime nhưng KHÔNG có dòng SVN_targets nào trong khoảng N-1: bị bỏ qua khỏi biểu đồ này
                // (không có TotalWorkingTime để so sánh), chỉ ghi log để theo dõi chứ không hiển thị lên UI
                var skippedProjects = downtimeHoursByProject.Keys.Except(workingTimeByProject.Keys).ToList();
                if (skippedProjects.Any())
                {
                    Console.WriteLine($"[RunningTimeByProject] Bỏ qua {skippedProjects.Count} dự án có downtime nhưng không có SVN_targets trong khoảng {(targetFromStr ?? "đầu")}-{targetToStr}: {string.Join(", ", skippedProjects)}");
                }

                // 3. Ghép 2 nguồn theo Project (chỉ các Project có dòng SVN_targets trong khoảng N-1)
                foreach (var kv in workingTimeByProject.OrderByDescending(x => x.Value))
                {
                    var project = kv.Key;
                    var totalWorkingTime = Math.Round(kv.Value, 2);
                    var downtime = downtimeHoursByProject.TryGetValue(project, out var dt) ? Math.Round(dt, 2) : 0;

                    var runningTimeRaw = totalWorkingTime - downtime;
                    if (runningTimeRaw < 0)
                    {
                        Console.WriteLine($"[RunningTimeByProject] Cảnh báo: Downtime ({downtime}h) > TotalWorkingTime ({totalWorkingTime}h) cho dự án '{project}' khoảng {targetFromStr}-{targetToStr}. RunningTime bị chặn về 0.");
                    }
                    var runningTime = Math.Round(Math.Max(0, runningTimeRaw), 2);

                    var downtimePercent = totalWorkingTime > 0 ? Math.Round(downtime / totalWorkingTime * 100, 1) : 0;
                    var runningPercent = totalWorkingTime > 0 ? Math.Round(100 - downtimePercent, 1) : 0;

                    result.Items.Add(new ProjectRunningTimeItem
                    {
                        Project = project,
                        TotalWorkingTime = totalWorkingTime,
                        Downtime = downtime,
                        RunningTime = runningTime,
                        DowntimePercent = downtimePercent,
                        RunningPercent = runningPercent
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi PrepareRunningTimeByProjectChartData: {ex.Message}");
            }

            return result;
        }

        /* Style tiêu đề (title) 1 sheet Excel, merge từ cột 1 đến mergeCols */
        private static void WriteExcelTitle(IXLWorksheet ws, int row, string title, int mergeCols)
        {
            ws.Cell(row, 1).Value = title;
            ws.Range(row, 1, row, mergeCols).Merge();
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 14;
            ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        /* Style hàng header cột (nền xanh, chữ trắng) - dùng chung cho các sheet Excel báo cáo downtime */
        private static void WriteExcelHeaderRow(IXLWorksheet ws, int row, string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(row, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromTheme(XLThemeColor.Accent1, 0.5);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
        }

        /* ClosedXML không vẽ được chart thật (bar/pie/line) trong file xlsx, nên dùng Data Bar - thanh màu
           chạy trong ô theo tỉ lệ giá trị - để mô phỏng hình dạng bar chart ngay trên cột số liệu chính của mỗi sheet */
        private static void AddDataBar(IXLWorksheet ws, int firstRow, int lastRow, int col, XLColor color)
        {
            if (lastRow < firstRow) return;
            ws.Range(firstRow, col, lastRow, col).AddConditionalFormat().DataBar(color);
        }

        /* Hàm xuất Excel cho báo cáo downtime - mỗi sheet tương ứng đúng 1 chart trên trang ReportDowntime,
           cộng thêm 1 sheet chi tiết theo Operation (bảng dưới cùng của trang) */
        [HttpGet]
        public async Task<IActionResult> ExportDowntimeReportToExcel(string fromDate = "", string toDate = "")
        {
            try
            {
                var reportData = await GetDowntimeReportData(fromDate, toDate);
                var dateRangeLabel = (!string.IsNullOrEmpty(fromDate) || !string.IsNullOrEmpty(toDate))
                    ? $"Từ ngày: {fromDate} - Đến ngày: {toDate}"
                    : "Toàn bộ dữ liệu (không giới hạn ngày)";

                using (var workbook = new XLWorkbook())
                {
                    // ── Sheet 1: Downtime theo Công đoạn (khớp bar chart "Downtime theo Công đoạn") ──
                    var groupedOps = reportData
                        .GroupBy(x => GetProjectGroup(x.Operation))
                        .Select(g => new { Operation = g.Key, Minutes = Math.Round(g.Sum(x => x.TotalDowntimeMinutes), 2) })
                        .OrderByDescending(x => x.Minutes)
                        .ToList();
                    var totalOpsMinutes = groupedOps.Sum(x => x.Minutes);

                    var wsOps = workbook.Worksheets.Add("Downtime theo Công đoạn");
                    wsOps.Style.Font.FontName = "Times New Roman";
                    wsOps.Style.Font.FontSize = 11;
                    WriteExcelTitle(wsOps, 1, "DOWNTIME THEO CÔNG ĐOẠN", 4);
                    wsOps.Cell(3, 1).Value = dateRangeLabel;
                    wsOps.Range(3, 1, 3, 4).Merge();
                    WriteExcelHeaderRow(wsOps, 5, new[] { "Công đoạn", "Tổng thời gian (phút)", "Thời gian", "% tổng Downtime" });
                    var opsRow = 6;
                    foreach (var g in groupedOps)
                    {
                        var pct = totalOpsMinutes > 0 ? Math.Round(g.Minutes / totalOpsMinutes * 100, 1) : 0;
                        wsOps.Cell(opsRow, 1).Value = g.Operation;
                        wsOps.Cell(opsRow, 2).Value = g.Minutes;
                        wsOps.Cell(opsRow, 3).Value = $"{(int)(g.Minutes / 60)}h {(int)(g.Minutes % 60)}m";
                        wsOps.Cell(opsRow, 4).Value = $"{pct}%";
                        opsRow++;
                    }
                    wsOps.Column(1).Width = 22;
                    wsOps.Column(2).Width = 20;
                    wsOps.Column(3).Width = 14;
                    wsOps.Column(4).Width = 16;
                    AddDataBar(wsOps, 6, opsRow - 1, 2, XLColor.FromHtml("#2a78d6"));

                    // ── Sheet 2: Phân bố Downtime theo Công đoạn (%) (khớp doughnut chart) ──
                    var wsPie = workbook.Worksheets.Add("Phân bố Downtime (%)");
                    wsPie.Style.Font.FontName = "Times New Roman";
                    wsPie.Style.Font.FontSize = 11;
                    WriteExcelTitle(wsPie, 1, "PHÂN BỐ DOWNTIME THEO CÔNG ĐOẠN (%)", 3);
                    wsPie.Cell(3, 1).Value = dateRangeLabel;
                    wsPie.Range(3, 1, 3, 3).Merge();
                    WriteExcelHeaderRow(wsPie, 5, new[] { "Công đoạn", "Thời gian (phút)", "% tổng Downtime" });
                    var pieRow = 6;
                    foreach (var g in groupedOps)
                    {
                        wsPie.Cell(pieRow, 1).Value = g.Operation;
                        wsPie.Cell(pieRow, 2).Value = g.Minutes;
                        wsPie.Cell(pieRow, 3).Value = totalOpsMinutes > 0 ? Math.Round(g.Minutes / totalOpsMinutes, 3) : 0;
                        wsPie.Cell(pieRow, 3).Style.NumberFormat.Format = "0.0%";
                        pieRow++;
                    }
                    wsPie.Column(1).Width = 22;
                    wsPie.Column(2).Width = 18;
                    wsPie.Column(3).Width = 16;
                    AddDataBar(wsPie, 6, pieRow - 1, 3, XLColor.FromHtml("#2a78d6"));

                    // ── Sheet 3: Downtime theo Mã lỗi (khớp bar chart ISS Code) ──
                    var issGroups = reportData
                        .SelectMany(op => op.ErrorDetails)
                        .GroupBy(e => new { e.ISS_Code, e.ErrorName })
                        .Select(g => new
                        {
                            IssCode = g.Key.ISS_Code,
                            ErrorName = g.Key.ErrorName,
                            Minutes = Math.Round(g.Sum(e => e.TotalDowntimeMinutes), 2),
                            Count = g.Sum(e => e.DowntimeCount)
                        })
                        .OrderByDescending(x => x.Minutes)
                        .ToList();

                    var wsIss = workbook.Worksheets.Add("Downtime theo Mã lỗi");
                    wsIss.Style.Font.FontName = "Times New Roman";
                    wsIss.Style.Font.FontSize = 11;
                    WriteExcelTitle(wsIss, 1, "DOWNTIME THEO MÃ LỖI", 5);
                    wsIss.Cell(3, 1).Value = dateRangeLabel;
                    wsIss.Range(3, 1, 3, 5).Merge();
                    WriteExcelHeaderRow(wsIss, 5, new[] { "ISS Code", "Tên lỗi", "Số lần", "Tổng thời gian (phút)", "Thời gian" });
                    var issRow = 6;
                    foreach (var e in issGroups)
                    {
                        wsIss.Cell(issRow, 1).Value = e.IssCode;
                        wsIss.Cell(issRow, 2).Value = e.ErrorName;
                        wsIss.Cell(issRow, 3).Value = e.Count;
                        wsIss.Cell(issRow, 4).Value = e.Minutes;
                        wsIss.Cell(issRow, 5).Value = $"{(int)(e.Minutes / 60)}h {(int)(e.Minutes % 60)}m";
                        issRow++;
                    }
                    wsIss.Column(1).Width = 14;
                    wsIss.Column(2).Width = 30;
                    wsIss.Column(3).Width = 10;
                    wsIss.Column(4).Width = 20;
                    wsIss.Column(5).Width = 14;
                    AddDataBar(wsIss, 6, issRow - 1, 4, XLColor.FromHtml("#2a78d6"));

                    // ── Sheet 4: Thời gian chạy theo dự án (khớp bar chart "Thời gian chạy của từng dự án") ──
                    var rtpData = await PrepareRunningTimeByProjectChartData(fromDate, toDate);
                    var wsRtp = workbook.Worksheets.Add("Thời gian chạy theo dự án");
                    wsRtp.Style.Font.FontName = "Times New Roman";
                    wsRtp.Style.Font.FontSize = 11;
                    WriteExcelTitle(wsRtp, 1, "THỜI GIAN CHẠY CỦA TỪNG DỰ ÁN (TỔNG THỜI GIAN - THỜI GIAN DOWNTIME)", 6);

                    var rtpToLabel = DateTime.ParseExact(rtpData.ReportDateTo, "yyyyMMdd", null).ToString("dd/MM/yyyy");
                    var rtpDateLabel = string.IsNullOrEmpty(rtpData.ReportDateFrom)
                        ? $"Đến ngày: {rtpToLabel}"
                        : $"Từ ngày: {DateTime.ParseExact(rtpData.ReportDateFrom, "yyyyMMdd", null):dd/MM/yyyy} - Đến ngày: {rtpToLabel}";
                    wsRtp.Cell(3, 1).Value = rtpDateLabel;
                    wsRtp.Range(3, 1, 3, 6).Merge();
                    WriteExcelHeaderRow(wsRtp, 5, new[] { "Dự án", "Tổng thời gian (giờ)", "Thời gian chạy (giờ)", "% Thời gian chạy", "Downtime (giờ)", "% Downtime" });
                    var rtpRow = 6;
                    foreach (var item in rtpData.Items)
                    {
                        wsRtp.Cell(rtpRow, 1).Value = item.Project;
                        wsRtp.Cell(rtpRow, 2).Value = item.TotalWorkingTime;
                        wsRtp.Cell(rtpRow, 3).Value = item.RunningTime;
                        wsRtp.Cell(rtpRow, 4).Value = Math.Round(item.RunningPercent / 100, 3);
                        wsRtp.Cell(rtpRow, 4).Style.NumberFormat.Format = "0.0%";
                        wsRtp.Cell(rtpRow, 5).Value = item.Downtime;
                        wsRtp.Cell(rtpRow, 6).Value = Math.Round(item.DowntimePercent / 100, 3);
                        wsRtp.Cell(rtpRow, 6).Style.NumberFormat.Format = "0.0%";
                        rtpRow++;
                    }
                    wsRtp.Column(1).Width = 22;
                    wsRtp.Column(2).Width = 18;
                    wsRtp.Column(3).Width = 18;
                    wsRtp.Column(4).Width = 15;
                    wsRtp.Column(5).Width = 14;
                    wsRtp.Column(6).Width = 12;
                    AddDataBar(wsRtp, 6, rtpRow - 1, 3, XLColor.FromHtml("#2a78d6"));

                    // ── Sheet 5: Lịch sử Downtime theo ngày (khớp line chart xu hướng) ──
                    var dailyData = PrepareDailyDowntimeChartData(reportData, fromDate, toDate);
                    var wsDaily = workbook.Worksheets.Add("Lịch sử Downtime theo ngày");
                    wsDaily.Style.Font.FontName = "Times New Roman";
                    wsDaily.Style.Font.FontSize = 11;
                    WriteExcelTitle(wsDaily, 1, "LỊCH SỬ DOWNTIME THEO NGÀY", 3);
                    wsDaily.Cell(3, 1).Value = dateRangeLabel;
                    wsDaily.Range(3, 1, 3, 3).Merge();
                    WriteExcelHeaderRow(wsDaily, 5, new[] { "Ngày", "Thời gian Downtime (phút)", "Số lần" });
                    var dailyRow = 6;
                    for (int i = 0; i < dailyData.Dates.Count; i++)
                    {
                        wsDaily.Cell(dailyRow, 1).Value = dailyData.Dates[i];
                        wsDaily.Cell(dailyRow, 2).Value = dailyData.DowntimeMinutes[i];
                        wsDaily.Cell(dailyRow, 3).Value = dailyData.DowntimeCounts[i];
                        dailyRow++;
                    }
                    wsDaily.Column(1).Width = 14;
                    wsDaily.Column(2).Width = 22;
                    wsDaily.Column(3).Width = 12;
                    AddDataBar(wsDaily, 6, dailyRow - 1, 2, XLColor.FromHtml("#2a78d6"));

                    // ── Sheet 6: Chi tiết theo Operation (bảng chi tiết dưới cùng của trang, không gộp) ──
                    var wsDetail = workbook.Worksheets.Add("Chi tiết theo Operation");
                    var currentRow = 1;

                    wsDetail.Style.Font.FontName = "Times New Roman";
                    wsDetail.Style.Font.FontSize = 11;

                    WriteExcelTitle(wsDetail, currentRow, "CHI TIẾT DOWNTIME THEO OPERATION VÀ LỖI", 6);
                    currentRow += 2;

                    wsDetail.Cell(currentRow, 1).Value = dateRangeLabel;
                    wsDetail.Range(currentRow, 1, currentRow, 6).Merge();
                    currentRow += 2;

                    foreach (var operation in reportData)
                    {
                        // Operation header
                        wsDetail.Cell(currentRow, 1).Value = $"Operation: {operation.Operation}";
                        wsDetail.Cell(currentRow, 1).Style.Font.Bold = true;
                        wsDetail.Cell(currentRow, 1).Style.Fill.BackgroundColor = XLColor.LightBlue;
                        wsDetail.Range(currentRow, 1, currentRow, 6).Merge();
                        currentRow++;

                        // Tổng hợp Operation
                        wsDetail.Cell(currentRow, 1).Value = "Tổng số lần downtime:";
                        wsDetail.Cell(currentRow, 2).Value = operation.TotalDowntimeCount;
                        wsDetail.Cell(currentRow, 3).Value = "Tổng thời gian:";
                        wsDetail.Cell(currentRow, 4).Value = operation.TotalDowntimeFormatted;
                        wsDetail.Cell(currentRow, 1).Style.Font.Bold = true;
                        wsDetail.Cell(currentRow, 3).Style.Font.Bold = true;
                        currentRow++;

                        // Chi tiết lỗi header
                        WriteExcelHeaderRow(wsDetail, currentRow, new[] { "ISS Code", "Tên lỗi", "Số lần", "Tổng thời gian (phút)", "Thời gian", "%" });
                        currentRow++;

                        // Chi tiết từng lỗi
                        foreach (var error in operation.ErrorDetails)
                        {
                            wsDetail.Cell(currentRow, 1).Value = error.ISS_Code;
                            wsDetail.Cell(currentRow, 2).Value = error.ErrorName;
                            wsDetail.Cell(currentRow, 3).Value = error.DowntimeCount;
                            wsDetail.Cell(currentRow, 4).Value = Math.Round(error.TotalDowntimeMinutes, 2);
                            wsDetail.Cell(currentRow, 5).Value = error.TotalDowntimeFormatted;

                            double percentage = operation.TotalDowntimeMinutes > 0
                                ? (error.TotalDowntimeMinutes / operation.TotalDowntimeMinutes * 100)
                                : 0;
                            wsDetail.Cell(currentRow, 6).Value = $"{Math.Round(percentage, 1)}%";

                            currentRow++;
                        }

                        currentRow += 2; // Khoảng cách giữa các operation
                    }

                    wsDetail.Column(1).Width = 15;
                    wsDetail.Column(2).Width = 30;
                    wsDetail.Column(3).Width = 12;
                    wsDetail.Column(4).Width = 20;
                    wsDetail.Column(5).Width = 15;
                    wsDetail.Column(6).Width = 10;

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        return File(stream.ToArray(),
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            $"DowntimeReport_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi xuất Excel: {ex.Message}" });
            }
        }

        // Helper class
        private class DowntimeRecord
        {
            public string Operation { get; set; }
            public string ISS_Code { get; set; }
            public string ErrorName { get; set; }
            public double DowntimeMinutes { get; set; }
        }


        // Hàm test
        [HttpGet]
        public async Task<IActionResult> TestProcessAll()
        {
            try
            {
                return await ProcessAllHistoryToDetail();
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message, stackTrace = ex.StackTrace });
            }
        }


        // DTO class cho request
        public class ProcessDateRequest
        {
            public string Date { get; set; }
        }

        public class InsertedIdResult
        {
            public int InsertedId { get; set; }
        }


        public class ValidateCodeRequest
        {
            public string Code { get; set; }
        }
    }
}