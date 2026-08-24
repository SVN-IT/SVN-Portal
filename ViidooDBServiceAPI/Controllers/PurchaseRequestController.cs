using Microsoft.AspNetCore.Mvc;
using ClosedXML.Excel;
using System.IO;
using SVNShareLib.Request;
using SVNShareLib;
using SVNShareLib.Utils;
using Newtonsoft.Json;

namespace ViidooDBServiceAPI.Controllers
{
    [ApiController]
    [Route("api/purchaserequest")]
    public class PurchaseRequestController : Controller
    {
        private readonly IWebHostEnvironment _env;
        SVNDBConfig svnDBConfig;
        public PurchaseRequestController(IWebHostEnvironment env, SVNDBConfig svnDBConfig)
        {
            _env = env;
            this.svnDBConfig = svnDBConfig;
        }
        [HttpPost("generate-excel")]
        public IActionResult GenerateExcel([FromBody] PurchaseRequestPayload payload)
        {
            LogService logger = new LogService(svnDBConfig.ConnectionString);
            try
            {
                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.PurchasePrequest, LogService.LogType.Info, JsonConvert.SerializeObject(payload).ToString());
                string templatePath = Path.Combine(_env.ContentRootPath, "templates", "Purchase_Request_Form.xlsx");

                if (!System.IO.File.Exists(templatePath))
                {
                    return BadRequest(new ExcelResponse { Success = false, Error = "Không tìm thấy file mẫu Excel" });
                }

                using (var workbook = new XLWorkbook(templatePath))
                {
                    var worksheet = workbook.Worksheet(1);

                    // 1. Ghi thông tin phòng ban
                    worksheet.Cell("A5").Value = $"Phòng yêu cầu/Requested department: {payload.Custbody_pr_department ?? ""}";

                    int startRow = 8;
                    int itemCount = payload.Items?.Count ?? 0;

                    if (itemCount > 0)
                    {
                        // File mẫu có sẵn 3 dòng trống (dòng 8, 9, 10)
                        int defaultTemplateRows = 3;

                        // 2. Nếu số lượng Item > 3, chèn thêm dòng ngay phía trên dòng Total Amount (dòng 11)
                        if (itemCount > defaultTemplateRows)
                        {
                            int rowsToInsert = itemCount - defaultTemplateRows;
                            // Chèn số dòng thiếu ngay tại vị trí dòng 11 (đẩy dòng Total xuống)
                            worksheet.Row(11).InsertRowsBelow(rowsToInsert - 1);
                        }
                        // Nếu số lượng Item < 3, xóa bớt các dòng trống dư thừa
                        else if (itemCount < defaultTemplateRows)
                        {
                            int rowsToDelete = defaultTemplateRows - itemCount;
                            worksheet.Rows(startRow + itemCount, startRow + defaultTemplateRows - 1).Delete();
                        }

                        // 3. Lấy dòng chuẩn (dòng 8) làm mẫu để copy format
                        var templateRow = worksheet.Row(startRow);

                        // 4. Đổ dữ liệu và copy định dạng
                        for (int i = 0; i < itemCount; i++)
                        {
                            int currentRowIndex = startRow + i;
                            var row = worksheet.Row(currentRowIndex);

                            // Sao chép định dạng từ dòng mẫu
                            row.Style = templateRow.Style;

                            var item = payload.Items![i];
                            row.Cell(1).Value = i + 1;                             // STT
                            row.Cell(2).Value = item.Custcol_pr_item ?? "";         // Tên mặt hàng
                            row.Cell(3).Value = item.Custcol_pr_item_purpose ?? ""; // Mục đích
                            row.Cell(4).Value = item.Unit ?? "";                    // Đơn vị
                            row.Cell(5).Value = item.Quantity;                     // Số lượng
                            row.Cell(6).Value = item.Estimate_rate;                // Đơn giá

                            // Công thức tính Thành tiền = Đơn giá * Số lượng
                            row.Cell(7).FormulaA1 = $"F{currentRowIndex}*E{currentRowIndex}";
                            row.Cell(8).Value = item.Delivery_date ?? "";          // Ngày giao

                            // Kẻ viền từng ô chuẩn khung mẫu
                            var rowRange = worksheet.Range(currentRowIndex, 1, currentRowIndex, 8);
                            rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            rowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                        }

                        // 5. Cập nhật dòng Total Amount (hiện tại nằm ngay dưới dòng item cuối cùng)
                        int totalRowIndex = startRow + itemCount;
                        var totalRow = worksheet.Row(totalRowIndex);

                        // Cập nhật công thức =SUM(G8:G{lastItemRow})
                        int lastItemRow = totalRowIndex - 1;
                        totalRow.Cell(7).FormulaA1 = $"SUM(G{startRow}:G{lastItemRow})";
                    }

                    // 6. Xuất file dạng Base64
                    // 6. Xuất ra Stream -> Base64 (Đã fix lỗi Stream rỗng)
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);

                        // Đảm bảo flush toàn bộ bộ nhớ
                        stream.Flush();

                        // Đưa con trỏ stream về đầu file trước khi Convert
                        stream.Position = 0;

                        byte[] fileBytes = stream.ToArray();

                        // Kiểm tra nếu byte array bị rỗng (dưới 100 bytes chứng tỏ lưu hỏng)
                        if (fileBytes.Length < 100)
                        {
                            return StatusCode(500, new ExcelResponse { Success = false, Error = "Lỗi khi tạo file Excel: Stream rỗng" });
                        }

                        string base64String = Convert.ToBase64String(fileBytes);
                        logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.PurchasePrequest, LogService.LogType.Info, base64String);
                        return Ok(new ExcelResponse
                        {
                            Success = true,
                            FileBase64 = base64String
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ExcelResponse
                {
                    Success = false,
                    Error = ex.Message
                });
            }
        }

        [HttpGet("test-download")]
        public IActionResult TestDownloadDirect()
        {
            try
            {
                string templatePath = Path.Combine(_env.ContentRootPath, "templates", "Purchase_Request_Form.xlsx");

                if (!System.IO.File.Exists(templatePath))
                {
                    return NotFound($"Không tìm thấy template tại: {templatePath}");
                }

                using (var workbook = new XLWorkbook(templatePath))
                {
                    var worksheet = workbook.Worksheet(1);

                    // Ghi dữ liệu test cứng vào
                    worksheet.Cell("A5").Value = "Phòng yêu cầu/Requested department: IT TEST DIRECT";

                    worksheet.Cell(8, 1).Value = 1;
                    worksheet.Cell(8, 2).Value = "Màn hình Dell 27 inch";
                    worksheet.Cell(8, 3).Value = "Test trực tiếp";
                    worksheet.Cell(8, 4).Value = "Cái";
                    worksheet.Cell(8, 5).Value = 2;
                    worksheet.Cell(8, 6).Value = 5000000;
                    worksheet.Cell(8, 7).FormulaA1 = "F8*E8";

                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        byte[] content = stream.ToArray();

                        // Trả về thẳng file nhị phân đính kèm
                        return File(
                            content,
                            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                            "Test_Direct_Download.xlsx"
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }
    }
}
