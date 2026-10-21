using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Sigma_Tools.Models;

namespace Sigma_Tools.Controllers
{
    public class OracleController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult POInbound()
        {
            string accessMode = "Tools";
            string controllerName = "Oracle";
            string indexPage = "POInbound";
            string titleName = "Create Netsuite SO";
            string selectedPage = "NetsuiteSO";

            ViewBag.AccessMode = accessMode;
            ViewBag.ControllerName = controllerName;
            ViewBag.IndexPage = indexPage;
            ViewBag.TitleName = titleName;
            ViewBag.SelectedPage = selectedPage;

            var model = new InboundPOReviewViewModel
            {
                PONumber = "",
                Date = "",
                Customer = "",
                Requisitioner = "",
                CalculatedUnits = 0,
                CalculatedAmount = 0m,
                ExpectedUnits = 0,
                ExpectedAmount = 0m,
                LineItems = new List<POLineItem>()
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> UploadPOPdf(IFormFile pdfFile)
        {
            if (pdfFile == null || pdfFile.Length == 0)
            {
                return Json(new { success = false, message = "Không tìm thấy file tải lên." });
            }

            try
            {
                string fileName = pdfFile.FileName;
                string apiUrl = "";

                // 1. Kiểm tra tên file để chọn endpoint API tương ứng
                if (fileName.Contains("GTK", StringComparison.OrdinalIgnoreCase) || fileName.Contains("Goertek", StringComparison.OrdinalIgnoreCase))
                {
                    apiUrl = "https://orc.datatech.com.vn/api/v1/goertek/extract-po";
                }
                else if (fileName.Contains("Whoop", StringComparison.OrdinalIgnoreCase))
                {
                    apiUrl = "https://orc.datatech.com.vn/api/v1/whoop/extract-po";
                }
                else if (fileName.Contains("Toast", StringComparison.OrdinalIgnoreCase))
                {
                    apiUrl = "https://orc.datatech.com.vn/api/v1/toast/extract-po";
                }
                else
                {
                    return Json(new { success = false, message = "Tên file không khớp với bất kỳ định dạng hãng nào (GTK, Whoop, Toast)!" });
                }

                // 2. Gửi file PDF xuống API OCR bên ngoài thông qua HttpClient
                using (var client = new HttpClient())
                using (var content = new MultipartFormDataContent())
                {
                    var fileStreamContent = new StreamContent(pdfFile.OpenReadStream());
                    fileStreamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
                    content.Add(fileStreamContent, "file", fileName);

                    HttpResponseMessage response = await client.PostAsync(apiUrl, content);
                    if (!response.IsSuccessStatusCode)
                    {
                        return Json(new { success = false, message = "Lỗi khi gọi API bóc tách dữ liệu." });
                    }

                    string jsonResponse = await response.Content.ReadAsStringAsync();

                    // 3. Parse JSON trả về từ API và Map vào InboundPOReviewViewModel
                    // (Bạn có thể dùng Newtonsoft.Json hoặc System.Text.Json)
                    var apiData = Newtonsoft.Json.Linq.JObject.Parse(jsonResponse);

                    // Trong Action UploadPOPdf sau khi parse apiData:
                    var auditObj = apiData["audit_report"];
                    var auditReport = new AuditReportViewModel();

                    if (auditObj != null)
                    {
                        auditReport.LineIntegrityPassed = auditObj["line_integrity_passed"]?.Value<bool>() ?? true;
                        auditReport.UnitsMatched = auditObj["units_matched"]?.Value<bool>() ?? true;
                        auditReport.AmountMatched = auditObj["amount_matched"]?.Value<bool>() ?? true;
                        auditReport.CalculatedUnits = auditObj["calculated_units"]?.Value<int>() ?? 0;
                        auditReport.ExpectedUnits = auditObj["expected_units"]?.Value<int>() ?? 0;
                        auditReport.CalculatedAmount = auditObj["calculated_amount"]?.Value<decimal>() ?? 0;
                        auditReport.ExpectedAmount = auditObj["expected_amount"]?.Value<decimal>() ?? 0;

                        var errs = auditObj["errors"] as Newtonsoft.Json.Linq.JArray;
                        if (errs != null)
                        {
                            foreach (var err in errs)
                            {
                                auditReport.Errors.Add(err.ToString());
                            }
                        }
                    }

                    var model = new InboundPOReviewViewModel
                    {
                        PONumber = apiData["header"]?["po_number"]?.ToString(),
                        Date = apiData["header"]?["po_date"]?.ToString(),
                        Customer = apiData["header"]?["buyer"]?["name"]?.ToString(),
                        Requisitioner = apiData["header"]?["buyer_contact"]?["name"]?.ToString(),
                        CalculatedUnits = apiData["audit_report"]?["calculated_units"]?.Value<int>() ?? 0,
                        CalculatedAmount = apiData["audit_report"]?["calculated_amount"]?.Value<decimal>() ?? 0,
                        ExpectedUnits = apiData["audit_report"]?["expected_units"]?.Value<int>() ?? 0,
                        ExpectedAmount = apiData["audit_report"]?["expected_amount"]?.Value<decimal>() ?? 0,
                        LineItems = new List<POLineItem>(),
                        AuditReport = auditReport
                    };

                    var items = apiData["line_items"] as Newtonsoft.Json.Linq.JArray;
                    if (items != null)
                    {
                        int index = 1;
                        foreach (var item in items)
                        {
                            model.LineItems.Add(new POLineItem
                            {
                                Line = index++,
                                ItemCode = item["item"]?.ToString(),
                                Description = item["description"]?.ToString(),
                                Qty = item["quantity"]?.Value<int>() ?? 0,
                                UnitPrice = item["price_each"]?.Value<decimal>() ?? 0,
                                Amount = item["amount"]?.Value<decimal>() ?? 0
                            });
                        }
                    }

                    // Trả về dữ liệu JSON để client cập nhật giao diện
                    return Json(new { success = true, data = model });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult VerifyErpPrices([FromBody] List<POLineItem> items)
        {
            if (items == null || items.Count == 0)
            {
                return Json(new { success = false, message = "Không có dữ liệu line items để đối chiếu." });
            }

            var verificationResult = new List<object>();

            foreach (var item in items)
            {
                // Giả lập logic gọi sang NetSuite / ERP lấy giá Base Price theo ItemCode
                decimal erpBasePrice = item.UnitPrice; // Mặc định khớp

                // Ví dụ giả lập dòng thứ 2 có sự chênh lệch giá để test giao diện giống ảnh mẫu của bạn
                if (item.ItemCode == "308001014737")
                {
                    erpBasePrice = 1.559M; // OCR là 10.06, ERP là 10.50 -> Lệch -0.44
                }
                if (item.ItemCode == "308001014736")
                {
                    erpBasePrice = 1.666M; // OCR là 10.06, ERP là 10.50 -> Lệch -0.44
                }

                decimal variance = item.UnitPrice - erpBasePrice;
                bool isMatched = Math.Abs(variance) < 0.0001m;

                verificationResult.Add(new
                {
                    line = item.Line,
                    itemCode = item.ItemCode,
                    description = item.Description,
                    qty = item.Qty,
                    ocrPrice = item.UnitPrice,
                    erpBasePrice = erpBasePrice,
                    isMatched = isMatched,
                    variance = variance
                });
            }

            return Json(new { success = true, data = verificationResult });
        }

        [HttpPost]
        public IActionResult RejectPO([FromBody] RejectPORequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            {
                return Json(new { success = false, message = "Lý do từ chối không được để trống." });
            }

            try
            {
                // TODO: Thực hiện logic cập nhật trạng thái PO thành 'Rejected' trong DB hoặc NetSuite tại đây
                string poNum = string.IsNullOrEmpty(request.PONumber) ? "N/A" : request.PONumber;

                return Json(new { success = true, message = $"Đã từ chối PO {poNum} thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi khi xử lý từ chối PO: " + ex.Message });
            }
        }
    }

    public class RejectPORequest
    {
        public string PONumber { get; set; }
        public string Reason { get; set; }
    }
}
