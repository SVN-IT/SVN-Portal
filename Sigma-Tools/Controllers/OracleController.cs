using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sigma_Tools.Models;
using Sigma_Tools.Services.Configurations;
using SVNShareLib;
using SVNShareLib.DAL;
using SVNShareLib.Request;
using SVNShareLib.Utils;

namespace Sigma_Tools.Controllers
{
    public class OracleController : Controller
    {
        DBConfiguration dbConfiguration;
        string connectionString;
        LogService logger;
        public OracleController(DBConfiguration dBConfiguration)
        {
            this.dbConfiguration = dBConfiguration;
            connectionString = dbConfiguration.GetConnectionString();
            logger = new LogService(connectionString);
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult POInbound()
        {
            string accessMode = "Tools";
            string controllerName = "Oracle";
            string indexPage = "POInbound";
            string titleName = "Create Netsuite PO";
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
                LineItems = new List<POLine>()
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
                apiUrl = "https://orc.datatech.com.vn/api/v1/agent/extract-po";

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
                        LineItems = new List<POLine>(),
                        AuditReport = auditReport
                    };

                    // Hoặc nếu bạn muốn trả về trực tiếp qua JSON response gọn gàng:
                    string currencyCode = apiData["header"]?["currency"]?.ToString() ?? "USD";

                    var items = apiData["line_items"] as Newtonsoft.Json.Linq.JArray;
                    if (items != null)
                    {
                        int index = 1;
                        foreach (var item in items)
                        {
                            model.LineItems.Add(new POLine
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

                    //xử lý replace itemCode của khách hàng sang item code của Sigma
                    if (model.LineItems != null)
                    {
                        var itemCodeMappingDataPortal = new SVN_ItemCodeMappingDataPortal(connectionString);
                        List<string> customerItemCode = model.LineItems.Select(x => x.ItemCode).ToList();

                        var dataUI = itemCodeMappingDataPortal.GetDataByCustomerCode(customerItemCode);

                        if (dataUI == null)
                        {
                            logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.OCRAIRequest, LogService.LogType.Error, $"Không tìm thấy mã item ERP tương ứng với mã item của khách hàng. Customer: {model.Customer}, PONumber: {model.PONumber}");
                            return Json(new { success = false, message = "Không tìm thấy mã item ERP tương ứng với mã item của khách hàng" });
                        }

                        List<string> itemCodeNotFound = new List<string>();

                        // Lấy mảng line_items từ JObject gốc để update trực tiếp sigma_code vào đó
                        var rawItemsArray = apiData["line_items"] as Newtonsoft.Json.Linq.JArray;

                        for (int i = 0; i < model.LineItems.Count; i++)
                        {
                            var item = model.LineItems[i];
                            var data = dataUI.FirstOrDefault(x => x.customer_code == item.ItemCode);

                            if (data == null)
                            {
                                itemCodeNotFound.Add(item.ItemCode);
                            }
                            else
                            {
                                string sigmaCode = data.sigma_code;

                                // 1. Cập nhật mã cho Model trả về giao diện
                                item.ItemCode = sigmaCode;

                                // 2. CẬP NHẬT LUÔN MÃ TRONG JOBJECT GỐC (apiData) ĐỂ LƯU VÀO RAW OCR RESPONSE
                                if (rawItemsArray != null && i < rawItemsArray.Count)
                                {
                                    rawItemsArray[i]["item"] = sigmaCode;
                                }
                            }
                        }

                        if (itemCodeNotFound.Count > 0)
                        {
                            logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.OCRAIRequest, LogService.LogType.Error, $"Các mã của khách hàng sau chưa có mã ERP tương ứng: {string.Join(",", itemCodeNotFound)}. Hãy liên hệ với quản trị viên.");
                            return Json(new { success = false, message = $"Các mã của khách hàng sau chưa có mã ERP tương ứng: {string.Join(",", itemCodeNotFound)}. Hãy liên hệ với quản trị viên." });
                        }
                    }
                    else
                    {
                        logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.OCRAIRequest, LogService.LogType.Error, $"Không đọc được danh sách item trong PO. Customer: {model.Customer}, PONumber: {model.PONumber}");
                        return Json(new { success = false, message = "Không đọc được danh sách item trong PO" });
                    }

                    // 1. Serialize toàn bộ JObject thành chuỗi JSON string an toàn ngay tại server
                    string rawJsonString = Newtonsoft.Json.JsonConvert.SerializeObject(apiData);

                    logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.OCRAIRequest, LogService.LogType.Info, $"UploadPO PDF thành công. Customer: {model.Customer}, PONumber: {model.PONumber}, Currency: {currencyCode}");
                    // Trả về dữ liệu JSON kèm theo đối tượng `rawJson` (chứa apiData đã được cập nhật sigma_code)
                    return Json(new { success = true, data = model, rawJsonStr = rawJsonString, currency = currencyCode });
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.OCRAIRequest, LogService.LogType.Error, $"Lỗi khi upload PO PDF: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> VerifyErpPrices([FromBody] ErpPriceVerificationRequest request)
        {
            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.VerifyErpPrices, LogService.LogType.Error, $"Không có dữ liệu line items để đối chiếu. Customer: {request?.Customer}, Currency: {request?.Currency}");
                return Json(new { success = false, message = "Không có dữ liệu line items để đối chiếu." });
            }

            try
            {
                string apiUrl = "https://api.sigmaworldwide.io/api/Netsuite/GetCustomerItemPricing";
                ErpApiResponse erpPricingData = null;

                // 1. Gọi API lấy chính sách giá từ ERP/NetSuite
                using (var client = new HttpClient())
                {
                    var payload = new
                    {
                        customerId = request.Customer,
                        currency = string.IsNullOrEmpty(request.Currency) ? "USD" : request.Currency
                    };

                    string jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
                    var httpContent = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

                    HttpResponseMessage apiResponse = await client.PostAsync(apiUrl, httpContent);
                    if (apiResponse.IsSuccessStatusCode)
                    {
                        string jsonResult = await apiResponse.Content.ReadAsStringAsync();
                        erpPricingData = Newtonsoft.Json.JsonConvert.DeserializeObject<ErpApiResponse>(jsonResult);
                    }
                }

                var verificationResult = new List<object>();

                foreach (var item in request.Items)
                {
                    decimal erpBasePrice = 0; // Mặc định nếu không tìm thấy trên hệ thống

                    if (erpPricingData != null && erpPricingData.Success && erpPricingData.Data != null && erpPricingData.Data.Items != null)
                    {
                        // Tìm kiếm item tương ứng trong danh sách API trả về theo ItemName (chính là ItemCode đã được map sang sigma_code)
                        var matchedErpItem = erpPricingData.Data.Items
                            .FirstOrDefault(x => x.ItemName.Equals(item.ItemCode, StringComparison.OrdinalIgnoreCase));

                        if (matchedErpItem != null && matchedErpItem.PricingTiers != null && matchedErpItem.PricingTiers.Count > 0)
                        {
                            if (matchedErpItem.PricingTiers.Count == 1)
                            {
                                // Trường hợp chỉ có 1 mức giá duy nhất
                                erpBasePrice = matchedErpItem.PricingTiers[0].Price;
                            }
                            else
                            {
                                // Trường hợp có nhiều bậc thang giá (pricingTiers): 
                                // Lấy mức giá có quantity lớn nhất mà vẫn nhỏ hơn hoặc bằng số lượng (qty) của PO dòng hiện tại
                                var validTier = matchedErpItem.PricingTiers
                                    .Where(t => item.Qty >= t.Quantity)
                                    .OrderByDescending(t => t.Quantity)
                                    .FirstOrDefault();

                                if (validTier != null)
                                {
                                    erpBasePrice = validTier.Price;
                                }
                                else
                                {
                                    // Mặc định lấy phần tử đầu tiên (quantity = 0) nếu không thỏa điều kiện bậc thang lớn hơn
                                    erpBasePrice = matchedErpItem.PricingTiers[0].Price;
                                }
                            }
                        }
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
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.VerifyErpPrices, LogService.LogType.Info, $"Đối chiếu giá ERP thành công. Customer: {request.Customer}, Currency: {request.Currency}, TotalItems: {request.Items.Count}");
                return Json(new { success = true, data = verificationResult });
            }
            catch (Exception ex)
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.VerifyErpPrices, LogService.LogType.Error, $"Lỗi khi gọi API lấy giá ERP: {ex.Message}");
                return Json(new { success = false, message = "Lỗi khi gọi API lấy giá ERP: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult RejectPO([FromBody] RejectPORequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.RejectPO, LogService.LogType.Error, $"Lý do từ chối PO bị trống. PONumber: {request?.PONumber}");
                return Json(new { success = false, message = "Lý do từ chối không được để trống." });
            }

            try
            {
                // TODO: Thực hiện logic cập nhật trạng thái PO thành 'Rejected' trong DB hoặc NetSuite tại đây
                string poNum = string.IsNullOrEmpty(request.PONumber) ? "N/A" : request.PONumber;

                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.RejectPO, LogService.LogType.Info, $"Đã từ chối PO {poNum} với lý do: {request.Reason}");
                return Json(new { success = true, message = $"Đã từ chối PO {poNum} thành công!" });
            }
            catch (Exception ex)
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.RejectPO, LogService.LogType.Error, $"Lỗi khi xử lý từ chối PO: {ex.Message}");
                return Json(new { success = false, message = "Lỗi khi xử lý từ chối PO: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateNetsuiteSO([FromBody] CreateNetsuiteSORequest request)
        {
            if (request == null || request.LineItems == null || request.LineItems.Count == 0)
            {
                return Json(new { success = false, message = "Dữ liệu truyền lên bị trống." });
            }

            try
            {
                string poNumber = request.PONumber;
                string customer = request.Customer;
                string currency = request.Currency;
                int totalLines = request.LineItems.Count;

                // VÍ DỤ: Đọc các trường phụ từ RawOcrResponse nếu cần dùng để đẩy lên NetSuite
                if (!string.IsNullOrEmpty(request.RawOcrResponse))
                {
                    //OCRPurchaseOrderRequest? dataRequest = JsonConvert.DeserializeObject<OCRPurchaseOrderRequest>(request.RawOcrResponse);
                    string apiUrl = "https://api.sigmaworldwide.io/api/Netsuite/CreatePurchaseOrderAsync";

                    using (var client = new HttpClient())
                    {
                        var httpContent = new StringContent(request.RawOcrResponse, System.Text.Encoding.UTF8, "application/json");

                        HttpResponseMessage apiResponse = await client.PostAsync(apiUrl, httpContent);
                        if (apiResponse.IsSuccessStatusCode)
                        {
                            string jsonResult = await apiResponse.Content.ReadAsStringAsync();
                            BODataProcessResult? processResult = JsonConvert.DeserializeObject<BODataProcessResult>(jsonResult);
                            if (processResult != null && processResult.OK)
                            {
                                // Thành công
                                string? jsonString = processResult.Message;
                                int jsonStartIndex = processResult.Message.IndexOf('{');
                                if (jsonStartIndex == -1)
                                {
                                    jsonStartIndex = processResult.Message.IndexOf('[');
                                }

                                if (jsonStartIndex != -1)
                                {
                                    // Cắt bỏ phần text thông báo ở đầu, chỉ lấy từ chuỗi JSON trở đi
                                    jsonString = processResult.Message.Substring(jsonStartIndex);
                                }

                                // Sau khi đã lọc sạch text thừa, tiến hành parse chuỗi JSON an toàn
                                var parsedResponse = JsonConvert.DeserializeObject<CreateNetsuiteSOResponse>(jsonString);

                                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Info, $"Tạo Sales Order trên NetSuite thành công. PONumber: {poNumber}, Customer: {customer}, Currency: {currency}, TotalLines: {totalLines}, Netsuite PO ID: {parsedResponse?.poId}, Netsuite SO Number: {parsedResponse?.poNumber}");
                                // Trả kết quả chuẩn về cho client
                                return Json(parsedResponse);
                            }
                            else
                            {
                                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Error, $"Lỗi khi tạo Sales Order trên NetSuite. PONumber: {poNumber}, Customer: {customer}, Currency: {currency}, TotalLines: {totalLines}, ErrorMessage: {processResult?.Message}");
                                return Json(new { success = false, message = "Lỗi khi tạo Sales Order trên NetSuite: " + (processResult?.Message ?? "Không có thông tin chi tiết") });
                            }
                        }
                        else
                        {
                            logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Error, $"Lỗi khi gọi API tạo Sales Order trên NetSuite. PONumber: {poNumber}, Customer: {customer}, Currency: {currency}, TotalLines: {totalLines}, StatusCode: {apiResponse.StatusCode}, ReasonPhrase: {apiResponse.ReasonPhrase}");
                            return Json(new { success = false, message = apiResponse.RequestMessage });
                        }
                    }
                }
                else
                {
                    logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Error, $"Dữ liệu Raw OCR Response bị trống, không thể tạo Sales Order trên NetSuite. PONumber: {poNumber}, Customer: {customer}, Currency: {currency}, TotalLines: {totalLines}");
                    return Json(new { success = false, message = "Dữ liệu Raw OCR Response bị trống, không thể tạo Sales Order trên NetSuite." });
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Error, $"Lỗi hệ thống khi tạo Sales Order trên NetSuite: {ex.Message}");
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }
    }

    public class RejectPORequest
    {
        public string PONumber { get; set; }
        public string Reason { get; set; }
    }

    public class ErpPriceVerificationRequest
    {
        public string Customer { get; set; }
        public string Currency { get; set; }
        public List<POLine> Items { get; set; }
    }

    public class PricingTier
    {
        public int Quantity { get; set; }
        public decimal Price { get; set; }
    }

    public class ErpItemPricing
    {
        public string ItemId { get; set; }
        public string ItemName { get; set; }
        public string PriceLevel { get; set; }
        public string Currency { get; set; }
        public List<PricingTier> PricingTiers { get; set; }
    }

    public class ErpApiResponseData
    {
        public bool Success { get; set; }
        public string CustomerCode { get; set; }
        public string CurrencyParam { get; set; }
        public string InternalId { get; set; }
        public int TotalItems { get; set; }
        public List<ErpItemPricing> Items { get; set; }
    }

    public class ErpApiResponse
    {
        public bool Success { get; set; }
        public ErpApiResponseData Data { get; set; }
    }

    // Request Model dùng để nhận dữ liệu từ bước Create SO
    public class CreateNetsuiteSORequest
    {
        public string PONumber { get; set; }
        public string Date { get; set; }
        public string Customer { get; set; }
        public string Requisitioner { get; set; }
        public string Currency { get; set; }
        public List<POLineItem> LineItems { get; set; }

        // Bổ sung thêm thuộc tính này để hứng trọn vẹn cấu trúc Raw OCR JSON từ Client gửi lên
        public string RawOcrResponse { get; set; }
    }

    public class CreateNetsuiteSOResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public int poId { get; set; }
        public string poNumber { get; set; }
        public string poUrl { get; set; }
    }
}
