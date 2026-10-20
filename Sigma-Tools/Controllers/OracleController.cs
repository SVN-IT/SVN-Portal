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
                PONumber = "PO10285",
                Date = "4/17/2026",
                Customer = "WHOOP Inc.",
                Requisitioner = "Nate Froio",
                CalculatedUnits = 19500,
                CalculatedAmount = 298790.00m,
                ExpectedUnits = 20700,
                ExpectedAmount = 315530.00m,
                LineItems = new List<POLineItem>
                {
                    new POLineItem { Line = 1, ItemCode = "810114362747", Description = "SuperKnit Band Dune...", Qty = 9000, UnitPrice = 17.21m },
                    new POLineItem { Line = 2, ItemCode = "810114362730", Description = "SuperKnit Luxe Band...", Qty = 2500, UnitPrice = 10.06m },
                    new POLineItem { Line = 3, ItemCode = "810114362723", Description = "SuperKnit Luxe Band...", Qty = 1000, UnitPrice = 13.41m },
                    new POLineItem { Line = 4, ItemCode = "810114362716", Description = "SuperKnit Luxe Band...", Qty = 1500, UnitPrice = 9.55m },
                    new POLineItem { Line = 5, ItemCode = "810114362563", Description = "SuperKnit Luxe Band...", Qty = 4500, UnitPrice = 17.83m },
                    new POLineItem { Line = 6, ItemCode = "810114362556", Description = "SuperKnit Luxe Band...", Qty = 1000, UnitPrice = 10.78m }
                }
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
    }
}
