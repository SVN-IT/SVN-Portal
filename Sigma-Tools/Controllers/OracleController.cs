using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sigma_Tools.Models;
using Sigma_Tools.Services.Authorizations;
using Sigma_Tools.Services.Configurations;
using SVNShareLib;
using SVNShareLib.DAL;
using SVNShareLib.Request;
using SVNShareLib.Utils;
using ClosedXML.Excel; // Thêm namespace này (hoặc EPPlus tùy dự án)
using System.IO;

namespace Sigma_Tools.Controllers
{
    public class OracleController : Controller
    {
        DBConfiguration dbConfiguration;
        string connectionString;
        LogService logger;
        AppConfig appConfig;
        private readonly IPermissionService _permissionService;
        public OracleController(DBConfiguration dBConfiguration, IPermissionService permissionService, AppConfig appConfig)
        {
            this.dbConfiguration = dBConfiguration;
            connectionString = dbConfiguration.GetConnectionString();
            logger = new LogService(connectionString);
            _permissionService = permissionService;
            this.appConfig = appConfig;
        }
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult POInbound()
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!_permissionService.HasRight("createnetsuitepo", "read"))
            {
                return RedirectToAction("AccessDenied", "Account", new { permission = "read", right = "createnetsuitepo" });
            }

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

        /// <summary>
        /// Action hiển thị trang danh sách PO Tracking được bóc tách từ lịch sử log
        /// </summary>
        [HttpGet]
        public IActionResult PoTrackingHistory(DateTime? fromDate, DateTime? toDate, string? status, int page = 1, int pageSize = 20)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return RedirectToAction("Login", "Account");
            }

            if (!_permissionService.HasRight("potrackinghistory", "read"))
            {
                return RedirectToAction("AccessDenied", "Account", new { permission = "read", right = "potrackinghistory" });
            }

            string accessMode = "Tools";
            string controllerName = "Oracle";
            string indexPage = "PoTrackingHistory";
            string titleName = "PO Tracking History";
            string selectedPage = "PoTrackingHistory";

            ViewBag.AccessMode = accessMode;
            ViewBag.ControllerName = controllerName;
            ViewBag.IndexPage = indexPage;
            ViewBag.TitleName = titleName;
            ViewBag.SelectedPage = selectedPage;

            try
            {
                // 1. CHUẨN HÓA KHOẢNG THỜI GIAN TRƯỚC KHI QUERY DB
                DateTime fDate = fromDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime tDate = toDate ?? DateTime.Now.Date;

                if (fDate.Date > DateTime.Now.Date) fDate = DateTime.Now.Date;
                if (tDate.Date > DateTime.Now.Date) tDate = DateTime.Now.Date;

                // Đảm bảo tDate lấy trọn đến cuối ngày (23:59:59)
                tDate = tDate.Date.AddHours(23).AddMinutes(59).AddSeconds(59);

                if (fDate > tDate)
                {
                    fDate = tDate.Date;
                }

                ViewBag.FromDate = fDate.ToString("yyyy-MM-dd");
                ViewBag.ToDate = tDate.ToString("yyyy-MM-dd");
                ViewBag.SelectedStatus = status ?? "";

                // 2. GỌI LOG SERVICE VỚI KHOẢNG THỜI GIAN ĐÃ CHUẨN HÓA
                var poLogList = logger.GetLogs(LogService.LogAction.NetsuitePOLogInfo, fDate, tDate);
                var txStateList = logger.GetLogs(LogService.LogAction.POTransactionState, fDate, tDate);

                // 3. BÓC TÁCH CONTENT THÀNH DANH SÁCH TRACKING ITEMS
                var allTrackingItems = new List<PoOrderTrackingDisplayModel>();
                var seenPoNumbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                // A. Xử lý các Transaction States trước (có trạng thái thời gian thực và cờ Resume)
                if (txStateList != null)
                {
                    foreach (var log in txStateList)
                    {
                        if (string.IsNullOrEmpty(log.Content)) continue;
                        try
                        {
                            var tx = JsonConvert.DeserializeObject<POTransactionStateModel>(log.Content);
                            if (tx == null) continue;

                            string poNum = !string.IsNullOrEmpty(tx.CreatedPoNumber) ? tx.CreatedPoNumber : (!string.IsNullOrEmpty(tx.CustomerPONumber) ? tx.CustomerPONumber : "N/A");
                            if (!string.IsNullOrEmpty(tx.CreatedPoNumber)) seenPoNumbers.Add(tx.CreatedPoNumber);
                            if (!string.IsNullOrEmpty(tx.CustomerPONumber)) seenPoNumbers.Add(tx.CustomerPONumber);

                            bool canResume = (tx.Status == "PROCESSING" || tx.Status == "PO_CREATED");
                            string statusDisplay = tx.Status switch
                            {
                                "PO_CREATED" => "PO Created (Chờ tạo SO)",
                                "PROCESSING" => $"Đang xử lý (Bước {tx.CurrentStep})",
                                "SO_COMPLETED" => "Hoàn thành (PO & SO)",
                                "REJECTED" => "Đã từ chối",
                                "FAILED" => "Gặp lỗi",
                                _ => tx.Status ?? "Processing"
                            };

                            if (tx.LineItems != null && tx.LineItems.Count > 0)
                            {
                                foreach (var line in tx.LineItems)
                                {
                                    allTrackingItems.Add(new PoOrderTrackingDisplayModel
                                    {
                                        CreatedOn = tx.ModifiedOn != DateTime.MinValue ? tx.ModifiedOn : log.CreatedOn,
                                        CreatedBy = log.CreatedBy ?? tx.CreatedBy ?? "System",
                                        PartNumber = line.ItemCode ?? "",
                                        SigmaPO = poNum,
                                        Description = line.Description ?? "",
                                        OrderQty = line.Quantity,
                                        OpenOrderQty = line.Quantity,
                                        PoUrl = tx.CreatedPoUrl,
                                        TransactionId = tx.TransactionId,
                                        Customer = tx.Customer,
                                        Status = tx.Status,
                                        StatusDisplay = statusDisplay,
                                        CurrentStep = tx.CurrentStep,
                                        CanResume = canResume,
                                        SoNumber = tx.CreatedSoNumber,
                                        SoUrl = tx.CreatedSoUrl
                                    });
                                }
                            }
                            else
                            {
                                allTrackingItems.Add(new PoOrderTrackingDisplayModel
                                {
                                    CreatedOn = tx.ModifiedOn != DateTime.MinValue ? tx.ModifiedOn : log.CreatedOn,
                                    CreatedBy = log.CreatedBy ?? tx.CreatedBy ?? "System",
                                    PartNumber = "N/A",
                                    SigmaPO = poNum,
                                    Description = $"Customer: {tx.Customer}",
                                    OrderQty = 0,
                                    OpenOrderQty = 0,
                                    PoUrl = tx.CreatedPoUrl,
                                    TransactionId = tx.TransactionId,
                                    Customer = tx.Customer,
                                    Status = tx.Status,
                                    StatusDisplay = statusDisplay,
                                    CurrentStep = tx.CurrentStep,
                                    CanResume = canResume,
                                    SoNumber = tx.CreatedSoNumber,
                                    SoUrl = tx.CreatedSoUrl
                                });
                            }
                        }
                        catch { }
                    }
                }

                // B. Bổ sung các bản ghi NetsuitePOLogInfo cũ nếu chưa có trong txStateList
                if (poLogList != null)
                {
                    foreach (var log in poLogList)
                    {
                        if (string.IsNullOrEmpty(log.Content)) continue;
                        try
                        {
                            var items = JsonConvert.DeserializeObject<List<PoOrderTrackingDto>>(log.Content);
                            if (items != null && items.Count > 0)
                            {
                                foreach (var item in items)
                                {
                                    if (!string.IsNullOrEmpty(item.SigmaPO) && seenPoNumbers.Contains(item.SigmaPO))
                                    {
                                        continue;
                                    }

                                    allTrackingItems.Add(new PoOrderTrackingDisplayModel
                                    {
                                        CreatedOn = log.CreatedOn,
                                        CreatedBy = log.CreatedBy ?? "System",
                                        PartNumber = item.PartNumber,
                                        SigmaPO = item.SigmaPO,
                                        Description = item.Description,
                                        OrderQty = item.OrderQty,
                                        OpenOrderQty = item.OpenOrderQty,
                                        PoUrl = item.PoUrl,
                                        Status = "SO_COMPLETED",
                                        StatusDisplay = "Hoàn thành (PO)",
                                        CanResume = false
                                    });
                                }
                            }
                        }
                        catch { }
                    }
                }

                // Áp dụng bộ lọc Status nếu có
                if (!string.IsNullOrEmpty(status))
                {
                    if (status == "PROCESSING")
                    {
                        allTrackingItems = allTrackingItems.Where(x => x.Status == "PROCESSING" || x.Status == "PO_CREATED").ToList();
                    }
                    else
                    {
                        allTrackingItems = allTrackingItems.Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();
                    }
                }

                // Sắp xếp mới nhất lên đầu
                allTrackingItems = allTrackingItems.OrderByDescending(x => x.CreatedOn).ToList();

                // 4. PHÂN TRANG (PAGING) CƠ BẢN TRÊN BỘ NHỚ
                int totalItems = allTrackingItems.Count;
                int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
                if (page < 1) page = 1;
                if (page > totalPages && totalPages > 0) page = totalPages;

                var pagedItems = allTrackingItems
                                    .Skip((page - 1) * pageSize)
                                    .Take(pageSize)
                                    .ToList();

                // Đẩy thông tin phân trang sang ViewBag để render giao diện
                ViewBag.CurrentPage = page;
                ViewBag.TotalPages = totalPages;
                ViewBag.PageSize = pageSize;
                ViewBag.TotalItems = totalItems;

                return View(pagedItems);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi tải lịch sử tracking: " + ex.Message;
                return View(new List<PoOrderTrackingDisplayModel>());
            }
        }

        [HttpPost]
        public async Task<IActionResult> UploadPOPdf(IFormFile pdfFile)
        {
            var currentUser = _permissionService.GetCurrentUser();
            if (pdfFile == null || pdfFile.Length == 0)
            {
                return Json(new { success = false, messageKey = "FileNotFound", message = "Không tìm thấy file tải lên." });
            }

            try
            {
                string fileName = pdfFile.FileName;
                string transactionId = Guid.NewGuid().ToString();

                // Lưu file PDF vào wwwroot/uploads/po_pdf để có thể preview lại khi F5 / Resume
                string relativePdfUrl = "";
                try
                {
                    string uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "po_pdf");
                    if (!Directory.Exists(uploadsDir))
                    {
                        Directory.CreateDirectory(uploadsDir);
                    }
                    string safeFileName = $"{transactionId}_{Path.GetFileName(fileName)}";
                    string fullPath = Path.Combine(uploadsDir, safeFileName);
                    using (var fs = new FileStream(fullPath, FileMode.Create))
                    {
                        await pdfFile.CopyToAsync(fs);
                    }
                    relativePdfUrl = $"/uploads/po_pdf/{safeFileName}";
                }
                catch (Exception saveEx)
                {
                    logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.POTransactionState, LogService.LogType.Warning, $"Không thể lưu file PDF vào server: {saveEx.Message}", currentUser?.UserName ?? "System");
                }

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
                        return Json(new { success = false, messageKey = "ApiOcrError", message = "Lỗi khi gọi API bóc tách dữ liệu." });
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

                        List<string> itemCodeNotFound = new List<string>();

                        // Lấy mảng line_items từ JObject gốc để update trực tiếp sigma_code vào đó nếu có
                        var rawItemsArray = apiData["line_items"] as Newtonsoft.Json.Linq.JArray;

                        for (int i = 0; i < model.LineItems.Count; i++)
                        {
                            var item = model.LineItems[i];
                            var data = dataUI?.FirstOrDefault(x => x.customer_code == item.ItemCode);

                            if (data == null || string.IsNullOrEmpty(data.sigma_code))
                            {
                                // Không tìm thấy sigma_code -> Giữ nguyên customer code làm itemCode và ghi nhận cảnh báo
                                itemCodeNotFound.Add(item.ItemCode);
                                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.OCRAIRequest, LogService.LogType.Warning, $"Không tìm thấy mã item ERP tương ứng với mã khách hàng: {item.ItemCode}. Giữ nguyên mã gốc.", currentUser.UserName);
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

                        // Trả về kết quả kèm theo danh sách mã chưa mapping để hiển thị cảnh báo trên giao diện
                        string rawJsonString = Newtonsoft.Json.JsonConvert.SerializeObject(apiData);

                        // Tự động khởi tạo và lưu Transaction State ban đầu để khôi phục khi F5 hoặc gián đoạn
                        try
                        {
                            var initialState = new POTransactionStateModel
                            {
                                TransactionId = transactionId,
                                CustomerPONumber = model.PONumber ?? "",
                                Customer = model.Customer ?? "",
                                Date = model.Date ?? "",
                                Currency = currencyCode,
                                Requisitioner = model.Requisitioner ?? "",
                                Status = "PROCESSING",
                                CurrentStep = 1,
                                PdfFileName = pdfFile.FileName,
                                PdfPath = relativePdfUrl,
                                RawOcrJson = rawJsonString,
                                LineItems = model.LineItems?.Select(l => new POTransactionLineItem
                                {
                                    Line = l.Line,
                                    ItemCode = l.ItemCode,
                                    Description = l.Description,
                                    Quantity = l.Qty,
                                    UnitPrice = l.UnitPrice,
                                    Amount = l.Amount
                                }).ToList(),
                                CreatedBy = currentUser?.UserName ?? "System",
                                CreatedOn = DateTime.Now,
                                ModifiedOn = DateTime.Now
                            };
                            if (Guid.TryParse(transactionId, out Guid txGuid))
                            {
                                logger.SaveTransactionState(txGuid, JsonConvert.SerializeObject(initialState), "PROCESSING", currentUser?.UserName ?? "System");
                            }
                        }
                        catch (Exception initEx)
                        {
                            logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.POTransactionState, LogService.LogType.Warning, $"Không thể lưu initial state: {initEx.Message}", currentUser?.UserName ?? "System");
                        }

                        logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.OCRAIRequest, LogService.LogType.Info, $"UploadPO PDF hoàn tất với {itemCodeNotFound.Count} mã chưa mapping ERP. Customer: {model.Customer}, PONumber: {model.PONumber}", currentUser?.UserName ?? "System");

                        return Json(new
                        {
                            success = true,
                            data = model,
                            rawJsonStr = rawJsonString,
                            currency = currencyCode,
                            unmappedCodes = itemCodeNotFound, // Truyền sang giao diện để cảnh báo dòng
                            transactionId = transactionId,
                            pdfUrl = relativePdfUrl
                        });
                    }
                    else
                    {
                        logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.OCRAIRequest, LogService.LogType.Error, $"Không đọc được danh sách item trong PO. Customer: {model.Customer}, PONumber: {model.PONumber}", currentUser.UserName);
                        return Json(new { success = false, message = "Không đọc được danh sách item trong PO" });
                    }

                    // 1. Serialize toàn bộ JObject thành chuỗi JSON string an toàn ngay tại server
                    //string rawJsonString = Newtonsoft.Json.JsonConvert.SerializeObject(apiData);

                    //logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.OCRAIRequest, LogService.LogType.Info, $"UploadPO PDF thành công. Customer: {model.Customer}, PONumber: {model.PONumber}, Currency: {currencyCode}", currentUser.UserName);
                    // Trả về dữ liệu JSON kèm theo đối tượng `rawJson` (chứa apiData đã được cập nhật sigma_code)
                    //return Json(new { success = true, data = model, rawJsonStr = rawJsonString, currency = currencyCode });
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.OCRAIRequest, LogService.LogType.Error, $"Lỗi khi upload PO PDF: {ex.Message}", currentUser.UserName);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> VerifyErpPrices([FromBody] ErpPriceVerificationRequest request)
        {
            var currentUser = _permissionService.GetCurrentUser();
            if (request == null || request.Items == null || request.Items.Count == 0)
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.VerifyErpPrices, LogService.LogType.Error, $"Không có dữ liệu line items để đối chiếu. Customer: {request?.Customer}, Currency: {request?.Currency}", currentUser.UserName);
                return Json(new { success = false, messageKey = "NoLineItemsError", message = "Không có dữ liệu line items để đối chiếu." });
            }

            try
            {
                string apiUrl = $"{appConfig.BaseAPIURL}api/Netsuite/GetCustomerItemPricing";
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
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.VerifyErpPrices, LogService.LogType.Info, $"Đối chiếu giá ERP thành công. Customer: {request.Customer}, Currency: {request.Currency}, TotalItems: {request.Items.Count}", currentUser.UserName);
                return Json(new { success = true, data = verificationResult });
            }
            catch (Exception ex)
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.VerifyErpPrices, LogService.LogType.Error, $"Lỗi khi gọi API lấy giá ERP: {ex.Message}", currentUser.UserName);
                return Json(new { success = false, message = "Lỗi khi gọi API lấy giá ERP: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetSubsidiaries([FromBody] SubsidiaryRequest request)
        {
            var currentUser = _permissionService.GetCurrentUser();
            if (request == null || string.IsNullOrEmpty(request.customerId))
            {
                return Json(new { success = false, message = "Customer ID không được để trống." });
            }

            try
            {
                string apiUrl = $"{appConfig.BaseAPIURL}api/Netsuite/GetSubsidiaries";
                using (var client = new HttpClient())
                {
                    string jsonPayload = JsonConvert.SerializeObject(request);
                    var httpContent = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(apiUrl, httpContent);
                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResult = await response.Content.ReadAsStringAsync();

                        // 1. Parse chuỗi JSON trả về thành JObject để bóc lớp ngoài
                        JObject rootObject = JsonConvert.DeserializeObject<JObject>(jsonResult);

                        // 2. Lấy phần giá trị nằm trong key "data"
                        var dataToken = rootObject?["data"];

                        if (dataToken != null)
                        {
                            // 3. Convert phần "data" đó trực tiếp ra class SubsidiaryApiResponse
                            SubsidiaryApiResponse subsidiaryApiResponse = dataToken.ToObject<SubsidiaryApiResponse>();

                            // Trả về giao diện theo cấu trúc bọc cũ của bạn nhưng bên trong data chính là SubsidiaryApiResponse đã bóc tách
                            return Json(new { success = true, data = subsidiaryApiResponse });
                        }

                        return Json(new { success = false, message = "Không tìm thấy khối dữ liệu data từ API NetSuite." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Lỗi khi gọi API lấy Subsidiaries từ NetSuite." });
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Error, $"Lỗi khi lấy danh sách Subsidiaries: {ex.Message}", currentUser.UserName);
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetLocationsBySubsidiary([FromBody] LocationRequest request)
        {
            if (request.subsidiaryId == 0)
            {
                return Json(new { success = false, message = "Subsidiary ID không được để trống." });
            }
            
            var currentUser = _permissionService.GetCurrentUser();

            try
            {
                string apiUrl = $"{appConfig.BaseAPIURL}api/Netsuite/GetLocations"; // Thay đổi endpoint API NetSuite tương ứng của bạn
                using (var client = new HttpClient())
                {
                    string jsonPayload = JsonConvert.SerializeObject(request);
                    var httpContent = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(apiUrl, httpContent);
                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResult = await response.Content.ReadAsStringAsync();

                        // Parse lớp ngoài để bóc tách khối "data" chứa LocationApiResponse
                        JObject rootObject = JsonConvert.DeserializeObject<JObject>(jsonResult);
                        var dataToken = rootObject?["data"];

                        if (dataToken != null)
                        {
                            LocationApiResponse locationApiResponse = dataToken.ToObject<LocationApiResponse>();
                            return Json(new { success = true, data = locationApiResponse });
                        }

                        return Json(new { success = false, message = "Không tìm thấy khối dữ liệu data từ API NetSuite." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Lỗi khi gọi API lấy Locations từ NetSuite." });
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Error, $"Lỗi khi lấy danh sách Locations: {ex.Message}", currentUser.UserName);
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetTaxCodesBySubsidiary([FromBody] LocationRequest request) // Hoặc dùng chung một kiểu Request nhận subsidiaryId (int)
        {
            if (request == null || request.subsidiaryId <= 0)
            {
                return Json(new { success = false, message = "Subsidiary ID không hợp lệ." });
            }
            var currentUser = _permissionService.GetCurrentUser();
            try
            {
                string apiUrl = $"{appConfig.BaseAPIURL}api/Netsuite/GetTaxCodes";
                using (var client = new HttpClient())
                {
                    string jsonPayload = JsonConvert.SerializeObject(request);
                    var httpContent = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(apiUrl, httpContent);
                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResult = await response.Content.ReadAsStringAsync();
                        JObject rootObject = JsonConvert.DeserializeObject<JObject>(jsonResult);
                        var dataToken = rootObject?["data"];

                        if (dataToken != null)
                        {
                            TaxCodeApiResponse apiResponse = dataToken.ToObject<TaxCodeApiResponse>();
                            return Json(new { success = true, data = apiResponse });
                        }
                        return Json(new { success = false, message = "Không tìm thấy dữ liệu Tax Codes." });
                    }
                    return Json(new { success = false, message = "Lỗi khi gọi API Tax Codes từ NetSuite." });
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Error, $"Lỗi Tax Codes: {ex.Message}", currentUser.UserName);
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetProjectCodesBySubsidiary([FromBody] LocationRequest request)
        {
            if (request == null || request.subsidiaryId <= 0)
            {
                return Json(new { success = false, message = "Subsidiary ID không hợp lệ." });
            }
            var currentUser = _permissionService.GetCurrentUser();
            try
            {
                string apiUrl = $"{appConfig.BaseAPIURL}api/Netsuite/GetProjectCodes";
                using (var client = new HttpClient())
                {
                    string jsonPayload = JsonConvert.SerializeObject(request);
                    var httpContent = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await client.PostAsync(apiUrl, httpContent);
                    if (response.IsSuccessStatusCode)
                    {
                        string jsonResult = await response.Content.ReadAsStringAsync();
                        JObject rootObject = JsonConvert.DeserializeObject<JObject>(jsonResult);
                        var dataToken = rootObject?["data"];

                        if (dataToken != null)
                        {
                            ProjectCodeApiResponse apiResponse = dataToken.ToObject<ProjectCodeApiResponse>();
                            return Json(new { success = true, data = apiResponse });
                        }
                        return Json(new { success = false, message = "Không tìm thấy dữ liệu Project Codes." });
                    }
                    return Json(new { success = false, message = "Lỗi khi gọi API Project Codes từ NetSuite." });
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Error, $"Lỗi Project Codes: {ex.Message}", currentUser.UserName);
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpGet]
        [HttpPost]
        public async Task<IActionResult> GetPersonInCharge()
        {
            var currentUser = _permissionService.GetCurrentUser();
            try
            {
                // 1. Thử gọi API từ ViidooDBServiceAPI để lấy dữ liệu động từ NetSuite
                string apiUrl = $"{appConfig.BaseAPIURL}api/Netsuite/GetPersonInCharge";
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(5);
                    try
                    {
                        HttpResponseMessage response = await client.GetAsync(apiUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            string jsonResult = await response.Content.ReadAsStringAsync();
                            JObject rootObject = JsonConvert.DeserializeObject<JObject>(jsonResult);
                            var dataToken = rootObject?["data"];
                            if (dataToken != null)
                            {
                                var apiResponse = dataToken.ToObject<PersonInChargeApiResponse>();
                                if (apiResponse != null && apiResponse.employees != null && apiResponse.employees.Count > 0)
                                {
                                    return Json(new { success = true, data = apiResponse });
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Warning, $"ViidooDBServiceAPI GetPersonInCharge không phản hồi: {ex.Message}. Sử dụng danh sách danh mục nhân viên dự phòng.", currentUser?.UserName);
                    }
                }

                // 2. Fallback: Danh sách nhân viên chuẩn NetSuite Sandbox (đồng bộ chính xác theo NetSuite ERP)
                var fallbackList = GetCuratedPersonInChargeList();
                return Json(new
                {
                    success = true,
                    data = new PersonInChargeApiResponse
                    {
                        success = true,
                        total = fallbackList.Count,
                        employees = fallbackList
                    }
                });
            }
            catch (Exception ex)
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Error, $"Lỗi GetPersonInCharge: {ex.Message}", currentUser?.UserName);
                var fallbackList = GetCuratedPersonInChargeList();
                return Json(new
                {
                    success = true,
                    data = new PersonInChargeApiResponse
                    {
                        success = true,
                        total = fallbackList.Count,
                        employees = fallbackList
                    }
                });
            }
        }

        private static List<PersonInChargeItem> GetCuratedPersonInChargeList()
        {
            return new List<PersonInChargeItem>
            {
                new PersonInChargeItem { id = 2703, name = "Alex Chen", entityId = "VD-1462", email = "hchen@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = 2702, name = "Owen Zhang", entityId = "VD-1461", email = "ozhang@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = 2704, name = "Leon Liao", entityId = "VD-1463", email = "lliao@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = 14996, name = "Sean Jin", entityId = "VD-2138", email = "sjin@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = 24439, name = "Jack Liu", entityId = "VD-2360", email = "jliu@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = "aaron_jack", name = "Aaron - Jack", entityId = "Aaron - Jack", email = "jack.aaron@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = "jacky_patrisia", name = "Jacky - Patrisia", entityId = "Jacky - Patrisia", email = "jacky@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = "paul_vicky", name = "Paul - Vicky", entityId = "Paul - Vicky", email = "pvicky@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = "vincent_xcw", name = "Vincent - 谢财文", entityId = "Vincent - 谢财文", email = "xcw@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = "zmf_hsl", name = "赵敏芳 - 黄秀丽", entityId = "赵敏芳 - 黄秀丽", email = "hsl@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = "netsuite_support", name = "netsuite.support", entityId = "netsuite.support", email = "netsuite.support@sigmaworldwide.com", title = "Support" },
                new PersonInChargeItem { id = "nina_dang", name = "Nina Dang", entityId = "Nina Dang", email = "ndang@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = "npi_team", name = "NPI Team", entityId = "NPI Team", email = "npiteam@sigmaworldwide.com", title = "Team" },
                new PersonInChargeItem { id = "patrisia_yi", name = "Patrisia Yi", entityId = "Patrisia Yi", email = "pyi@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = "aaron_li", name = "Aaron Li", entityId = "Aaron Li", email = "ali@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = "adam_liu", name = "Adam Liu", entityId = "Adam Liu", email = "aliu@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = "alan_nguyen", name = "Alan Nguyen", entityId = "Alan Nguyen", email = "anguyen@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = "alan_shi", name = "Alan Shi", entityId = "Alan Shi", email = "ashi@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = "alan_zhou", name = "Alan Zhou", entityId = "Alan Zhou", email = "azhou@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = "alex_jin", name = "Alex Jin", entityId = "Alex Jin", email = "ajin@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = "alice_zhang", name = "Alice Zhang", entityId = "Alice Zhang", email = "azhang@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = "ben_lin", name = "Ben Lin", entityId = "Ben Lin", email = "blin@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = 2645, name = "Beth Guo", entityId = "Beth Guo", email = "bguo@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = 2700, name = "Yoyo Huang", entityId = "VD-1459", email = "yhuang@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = 8450, name = "James Kung", entityId = "VD-1954", email = "jkung@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = 2701, name = "Kenneth Tang", entityId = "VD-1460", email = "ktang@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = 3396, name = "Charlie McArthur", entityId = "VD-1547", email = "cmcarthur@sigmaworldwide.com", title = "PM" },
                new PersonInChargeItem { id = 2686, name = "Alexander Cobb", entityId = "VD-1448", email = "acobb@sigmaworldwide.com", title = "PM" }
            };
        }

        [HttpPost]
        public IActionResult RejectPO([FromBody] RejectPORequest request)
        {
            var currentUser = _permissionService.GetCurrentUser();
            if (request == null || string.IsNullOrWhiteSpace(request.Reason))
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.RejectPO, LogService.LogType.Error, $"Lý do từ chối PO bị trống. PONumber: {request?.PONumber}", currentUser.UserName);
                return Json(new { success = false, messageKey = "RejectReasonEmpty", message = "Lý do từ chối không được để trống." });
            }

            try
            {
                // TODO: Thực hiện logic cập nhật trạng thái PO thành 'Rejected' trong DB hoặc NetSuite tại đây
                string poNum = string.IsNullOrEmpty(request.PONumber) ? "N/A" : request.PONumber;

                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.RejectPO, LogService.LogType.Info, $"Đã từ chối PO {poNum} với lý do: {request.Reason}", currentUser.UserName);
                return Json(new { success = true, message = $"Đã từ chối PO {poNum} thành công!" });
            }
            catch (Exception ex)
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.RejectPO, LogService.LogType.Error, $"Lỗi khi xử lý từ chối PO: {ex.Message}", currentUser.UserName);
                return Json(new { success = false, message = "Lỗi khi xử lý từ chối PO: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateNetsuiteSO([FromBody] CreateNetsuiteSORequest request)
        {
            if (request == null || request.LineItems == null || request.LineItems.Count == 0)
            {
                return Json(new { success = false, messageKey = "EmptyPayloadError", message = "Dữ liệu truyền lên bị trống." });
            }
            var currentUser = _permissionService.GetCurrentUser();
            try
            {
                string poNumber = request.PONumber;
                string customer = request.Customer;
                string currency = request.Currency;
                int totalLines = request.LineItems.Count;

                // VÍ DỤ: Đọc các trường phụ từ RawOcrResponse nếu cần dùng để đẩy lên NetSuite
                if (!string.IsNullOrEmpty(request.RawOcrResponse))
                {
                    // Parse chuỗi RawOcrResponse thành JObject để tiêm Subsidiary vào header nếu cần
                    JObject ocrData = JsonConvert.DeserializeObject<JObject>(request.RawOcrResponse);
                    if (ocrData != null && request.Subsidiary > 0)
                    {
                        if (ocrData["header"] == null) ocrData["header"] = new JObject();

                        if (request.Subsidiary > 0) ocrData["header"]["subsidiary"] = request.Subsidiary;
                        if (request.Location > 0) ocrData["header"]["location"] = request.Location;
                        if (request.Subsidiary > 0) ocrData["header"]["subsidiary"] = request.Subsidiary;
                        if (request.Location > 0) ocrData["header"]["location"] = request.Location;
                        if (request.TaxCode > 0) ocrData["header"]["tax_code"] = request.TaxCode;
                        if (request.ProjectCode > 0) ocrData["header"]["project_code"] = request.ProjectCode;

                        //khi tạo PO thì lấy ngày hiện tại
                        ocrData["header"]["po_date"] = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss");

                        request.RawOcrResponse = JsonConvert.SerializeObject(ocrData);
                    }

                    //OCRPurchaseOrderRequest? dataRequest = JsonConvert.DeserializeObject<OCRPurchaseOrderRequest>(request.RawOcrResponse);
                    string apiUrl = $"{appConfig.BaseAPIURL}api/Netsuite/CreatePurchaseOrderAsync";

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

                                string createdPoNumber = parsedResponse.poNumber ?? request.PONumber;

                                // Trích xuất list tracking từ RawOcrResponse kết hợp mã PO từ response NetSuite để log
                                var trackingList = new List<PoOrderTrackingDto>();
                                string createdPoUrl = parsedResponse?.poUrl ?? "";
                                if (ocrData != null && ocrData["line_items"] is JArray rawLineItemsArray)
                                {
                                    foreach (var item in rawLineItemsArray)
                                    {
                                        trackingList.Add(new PoOrderTrackingDto
                                        {
                                            PartNumber = item["item"]?.ToString() ?? "",
                                            SigmaPO = createdPoNumber,
                                            Description = item["description"]?.ToString() ?? "",
                                            OrderQty = item["quantity"]?.Value<decimal>() ?? 0,
                                            OpenOrderQty = item["quantity"]?.Value<decimal>() ?? 0,
                                            PoUrl = createdPoUrl
                                        });
                                    }
                                }

                                string trackingJsonLog = JsonConvert.SerializeObject(trackingList);
                                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.NetsuitePOLogInfo, LogService.LogType.Info, trackingJsonLog, currentUser.UserName);

                                // Cập nhật Transaction State sang PO_CREATED để nếu refresh/mất mạng vẫn tiếp tục được ở Bước 4
                                if (!string.IsNullOrEmpty(request.TransactionId) && Guid.TryParse(request.TransactionId, out Guid txGuid))
                                {
                                    try
                                    {
                                        var existingLog = logger.GetTransactionState(txGuid);
                                        POTransactionStateModel? state = null;
                                        if (existingLog != null && !string.IsNullOrEmpty(existingLog.Content))
                                        {
                                            state = JsonConvert.DeserializeObject<POTransactionStateModel>(existingLog.Content);
                                        }
                                        if (state == null)
                                        {
                                            state = new POTransactionStateModel { TransactionId = request.TransactionId };
                                        }
                                        state.CustomerPONumber = poNumber;
                                        state.Customer = customer;
                                        state.Date = request.Date;
                                        state.Currency = currency;
                                        state.Status = "PO_CREATED";
                                        state.CurrentStep = 4;
                                        state.CreatedPoId = parsedResponse?.poId;
                                        state.CreatedPoNumber = createdPoNumber;
                                        state.CreatedPoUrl = createdPoUrl;
                                        state.Subsidiary = request.Subsidiary;
                                        state.Location = request.Location;
                                        state.TaxCode = request.TaxCode;
                                        state.ProjectCode = request.ProjectCode.ToString();
                                        state.ProjectCodeText = !string.IsNullOrEmpty(request.ProjectCodeText) ? request.ProjectCodeText : request.ProjectCode.ToString();
                                        state.ModifiedOn = DateTime.Now;

                                        logger.SaveTransactionState(txGuid, JsonConvert.SerializeObject(state), "PO_CREATED", currentUser.UserName);
                                    }
                                    catch (Exception txEx)
                                    {
                                        logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.POTransactionState, LogService.LogType.Warning, $"Không thể lưu state PO_CREATED: {txEx.Message}", currentUser.UserName);
                                    }
                                }

                                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Info, $"Tạo Purchase Order trên NetSuite thành công. PONumber: {poNumber}, Customer: {customer}, Currency: {currency}, TotalLines: {totalLines}, Netsuite PO ID: {parsedResponse?.poId}, Netsuite PO Number: {parsedResponse?.poNumber}", currentUser.UserName);
                                // Trả kết quả chuẩn về cho client
                                return Json(parsedResponse);
                            }
                            else
                            {
                                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Error, $"Lỗi khi tạo Purchase Order trên NetSuite. PONumber: {poNumber}, Customer: {customer}, Currency: {currency}, TotalLines: {totalLines}, ErrorMessage: {processResult?.Message}", currentUser.UserName);
                                return Json(new { success = false, message = "Lỗi khi tạo Purchase Order trên NetSuite: " + (processResult?.Message ?? "Không có thông tin chi tiết") });
                            }
                        }
                        else
                        {
                            logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Error, $"Lỗi khi gọi API tạo Purchase Order trên NetSuite. PONumber: {poNumber}, Customer: {customer}, Currency: {currency}, TotalLines: {totalLines}, StatusCode: {apiResponse.StatusCode}, ReasonPhrase: {apiResponse.ReasonPhrase}", currentUser.UserName);
                            return Json(new { success = false, message = $"{apiResponse.StatusCode} - {apiResponse.ReasonPhrase}" });
                        }
                    }
                }
                else
                {
                    logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Error, $"Dữ liệu Raw OCR Response bị trống, không thể tạo Purchase Order trên NetSuite. PONumber: {poNumber}, Customer: {customer}, Currency: {currency}, TotalLines: {totalLines}", currentUser.UserName);
                    return Json(new { success = false, message = "Dữ liệu Raw OCR Response bị trống, không thể tạo Purchase Order trên NetSuite." });
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Error, $"Lỗi hệ thống khi tạo Purchase Order trên NetSuite: {ex.Message}", currentUser.UserName);
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateSalesOrder([FromBody] CreateSalesOrderRequest request)
        {
            if (request == null || request.LineItems == null || request.LineItems.Count == 0)
            {
                return Json(new { success = false, messageKey = "EmptyPayloadError", message = "Dữ liệu truyền lên bị trống." });
            }
            var currentUser = _permissionService.GetCurrentUser();
            try
            {
                string soRefPO = request.PONumber;
                string customer = request.Customer;
                string currency = request.Currency;
                int totalLines = request.LineItems.Count;

                // Đồng bộ cập nhật giá mới từ request.LineItems vào RawOcrResponse nếu có
                if (!string.IsNullOrEmpty(request.RawOcrResponse))
                {
                    JObject ocrData = JsonConvert.DeserializeObject<JObject>(request.RawOcrResponse);
                    if (ocrData != null)
                    {
                        if (ocrData["header"] == null) ocrData["header"] = new JObject();

                        // Luôn cố định Subsidiary (1) và Location (105) là Sigma Worldwide theo quy định SO in ERP (Rule 8 & 9)
                        ocrData["header"]["subsidiary"] = 1; // No. 8: Sigma Worldwide
                        ocrData["header"]["location"] = 105; // No. 9: Sigma Worldwide
                        ocrData["header"]["subsidiary_name"] = "Sigma Worldwide"; // No. 8
                        ocrData["header"]["location_name"] = "Sigma Worldwide"; // No. 9

                        if (request.TaxCode > 0) ocrData["header"]["tax_code"] = request.TaxCode;
                        if (request.ProjectCode > 0)
                        {
                            ocrData["header"]["project_code"] = request.ProjectCode;
                            ocrData["header"]["project_code_id"] = request.ProjectCode;
                        }

                        // Thiết lập các trường theo chuẩn logic "SO in ERP" (16 điểm quy định)
                        ocrData["header"]["customform"] = !string.IsNullOrEmpty(request.CustomForm) ? request.CustomForm : "111"; // No. 1
                        ocrData["header"]["customer"] = customer; // No. 2
                        ocrData["header"]["po_number"] = !string.IsNullOrEmpty(request.CustomerPONumber) ? request.CustomerPONumber : request.PONumber; // No. 5
                        ocrData["header"]["so_date"] = !string.IsNullOrEmpty(request.Date) ? request.Date : DateTime.Now.ToString("yyyy-MM-dd"); // No. 6
                        ocrData["header"]["person_in_charge"] = request.PersonInCharge ?? ""; // No. 7: DRI PM
                        ocrData["header"]["dri_pm"] = request.PersonInCharge ?? ""; // No. 7
                        if (request.PersonInChargeId.HasValue && request.PersonInChargeId.Value > 0)
                        {
                            ocrData["header"]["person_in_charge_id"] = request.PersonInChargeId.Value;
                            ocrData["header"]["dri_pm_id"] = request.PersonInChargeId.Value;
                            ocrData["header"]["custbody_sig_pic"] = request.PersonInChargeId.Value;
                            ocrData["header"]["salesrep"] = request.PersonInChargeId.Value;
                        }
                        else if (!string.IsNullOrEmpty(request.PersonInCharge))
                        {
                            ocrData["header"]["custbody_sig_pic"] = request.PersonInCharge;
                        }

                        if (!string.IsNullOrEmpty(request.ProjectCodeStr))
                        {
                            ocrData["header"]["project_code"] = request.ProjectCodeStr; // No. 10: 3 digits
                            ocrData["header"]["project_code_str"] = request.ProjectCodeStr;
                        }
                        ocrData["header"]["po_type"] = !string.IsNullOrEmpty(request.PoType) ? request.PoType : "MP"; // No. 11: MP or NPI
                        ocrData["header"]["pr_type"] = !string.IsNullOrEmpty(request.PrType) ? request.PrType : "DRI"; // No. 12: DRI
                        ocrData["header"]["allow_cross_sub_fulfillment"] = true; // No. 14
                        ocrData["header"]["isCrossSubTransaction"] = true; // No. 14
                        ocrData["header"]["ref_po"] = soRefPO;

                        if (ocrData["line_items"] is JArray rawLineItemsArray)
                        {
                            for (int i = 0; i < rawLineItemsArray.Count && i < request.LineItems.Count; i++)
                            {
                                var itemObj = rawLineItemsArray[i];
                                var updatedLine = request.LineItems[i];
                                itemObj["price_each"] = updatedLine.price_each;
                                itemObj["amount"] = updatedLine.quantity * updatedLine.price_each;
                                itemObj["tax_code"] = 0; // No. 3: Tax Code 0 for SW
                                itemObj["expected_ship_date"] = !string.IsNullOrEmpty(updatedLine.expected_ship_date) ? updatedLine.expected_ship_date : (!string.IsNullOrEmpty(updatedLine.eta_date) ? updatedLine.eta_date : request.Date); // No. 15
                                itemObj["inventory_location"] = !string.IsNullOrEmpty(updatedLine.inventory_location) ? updatedLine.inventory_location : (!string.IsNullOrEmpty(request.InventoryLocation) ? request.InventoryLocation : "Sigma Asia"); // No. 16: Sigma Asia
                            }
                        }

                        // Tính lại totals
                        if (ocrData["totals"] != null)
                        {
                            decimal newGrandTotal = request.LineItems.Sum(x => x.quantity * x.price_each);
                            ocrData["totals"]["subtotal"] = newGrandTotal;
                            ocrData["totals"]["grand_total"] = newGrandTotal;
                        }

                        request.RawOcrResponse = JsonConvert.SerializeObject(ocrData);
                    }

                    string apiUrl = $"{appConfig.BaseAPIURL}api/Netsuite/CreateSalesOrderAsync";

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
                                string? jsonString = processResult.Message;
                                int jsonStartIndex = processResult.Message.IndexOf('{');
                                if (jsonStartIndex == -1)
                                {
                                    jsonStartIndex = processResult.Message.IndexOf('[');
                                }

                                if (jsonStartIndex != -1)
                                {
                                    jsonString = processResult.Message.Substring(jsonStartIndex);
                                }

                                var parsedResponse = JsonConvert.DeserializeObject<CreateSalesOrderResponse>(jsonString);
                                string createdSoNumber = parsedResponse?.soNumber ?? ("SO-" + soRefPO);

                                // Cập nhật Transaction State sang SO_COMPLETED
                                if (!string.IsNullOrEmpty(request.TransactionId) && Guid.TryParse(request.TransactionId, out Guid txGuid))
                                {
                                    try
                                    {
                                        var existingLog = logger.GetTransactionState(txGuid);
                                        POTransactionStateModel? state = null;
                                        if (existingLog != null && !string.IsNullOrEmpty(existingLog.Content))
                                        {
                                            state = JsonConvert.DeserializeObject<POTransactionStateModel>(existingLog.Content);
                                        }
                                        if (state == null)
                                        {
                                            state = new POTransactionStateModel { TransactionId = request.TransactionId };
                                        }
                                        state.Status = "SO_COMPLETED";
                                        state.CurrentStep = 4;
                                        state.CreatedSoId = parsedResponse?.soId;
                                        state.CreatedSoNumber = createdSoNumber;
                                        state.CreatedSoUrl = parsedResponse?.soUrl;
                                        state.PersonInCharge = request.PersonInCharge;
                                        state.PersonInChargeId = request.PersonInChargeId;
                                        if (request.ProjectCode > 0)
                                        {
                                            state.ProjectCode = request.ProjectCode.ToString();
                                        }
                                        if (!string.IsNullOrEmpty(request.ProjectCodeStr))
                                        {
                                            state.ProjectCodeText = request.ProjectCodeStr;
                                        }
                                        else if (!string.IsNullOrEmpty(request.ProjectCodeText))
                                        {
                                            state.ProjectCodeText = request.ProjectCodeText;
                                        }
                                        state.ModifiedOn = DateTime.Now;

                                        logger.SaveTransactionState(txGuid, JsonConvert.SerializeObject(state), "SO_COMPLETED", currentUser.UserName);
                                    }
                                    catch (Exception txEx)
                                    {
                                        logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.POTransactionState, LogService.LogType.Warning, $"Không thể lưu state SO_COMPLETED: {txEx.Message}", currentUser.UserName);
                                    }
                                }

                                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Info, $"Tạo Sales Order trên NetSuite thành công. SO Number: {createdSoNumber}, Ref PO: {soRefPO}, Customer: {customer}, Currency: {currency}, TotalLines: {totalLines}, SO ID: {parsedResponse?.soId}", currentUser.UserName);

                                return Json(new
                                {
                                    success = true,
                                    message = parsedResponse?.message ?? "Tạo Sales Order trên NetSuite thành công!",
                                    soId = parsedResponse?.soId,
                                    soNumber = createdSoNumber,
                                    soUrl = parsedResponse?.soUrl
                                });
                            }
                            else
                            {
                                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Error, $"Lỗi khi tạo Sales Order trên NetSuite. Ref PO: {soRefPO}, Customer: {customer}, ErrorMessage: {processResult?.Message}", currentUser.UserName);
                                return Json(new { success = false, message = "Lỗi khi tạo Sales Order trên NetSuite: " + (processResult?.Message ?? "Không có thông tin chi tiết") });
                            }
                        }
                        else
                        {
                            string errContent = await apiResponse.Content.ReadAsStringAsync();
                            logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Error, $"Lỗi khi gọi API tạo Sales Order trên NetSuite. Ref PO: {soRefPO}, StatusCode: {apiResponse.StatusCode}, ReasonPhrase: {apiResponse.ReasonPhrase}, Detail: {errContent}", currentUser.UserName);
                            return Json(new { success = false, message = $"{apiResponse.StatusCode} - {apiResponse.ReasonPhrase}" + (!string.IsNullOrWhiteSpace(errContent) ? $": {errContent}" : "") });
                        }
                    }
                }
                else
                {
                    logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Error, $"Dữ liệu Raw OCR Response bị trống khi tạo Sales Order. Ref PO: {soRefPO}", currentUser.UserName);
                    return Json(new { success = false, message = "Dữ liệu Raw OCR Response bị trống, không thể tạo Sales Order trên NetSuite." });
                }
            }
            catch (Exception ex)
            {
                logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.CreateNetsuitePO, LogService.LogType.Error, $"Lỗi hệ thống khi tạo Sales Order trên NetSuite: {ex.Message}", currentUser.UserName);
                return Json(new { success = false, message = "Lỗi hệ thống: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult SaveTransactionState([FromBody] POTransactionStateModel state)
        {
            var currentUser = _permissionService.GetCurrentUser();
            if (state == null || string.IsNullOrEmpty(state.TransactionId))
            {
                return Json(new { success = false, message = "Dữ liệu state hoặc TransactionId không hợp lệ" });
            }

            try
            {
                if (!Guid.TryParse(state.TransactionId, out Guid txGuid))
                {
                    txGuid = Guid.NewGuid();
                    state.TransactionId = txGuid.ToString();
                }

                // Bảo lưu thông tin quan trọng từ bản ghi trước (ví dụ file PDF, thông tin PO đã tạo)
                var existingLog = logger.GetTransactionState(txGuid);
                if (existingLog != null && !string.IsNullOrEmpty(existingLog.Content))
                {
                    try
                    {
                        var oldState = JsonConvert.DeserializeObject<POTransactionStateModel>(existingLog.Content);
                        if (oldState != null)
                        {
                            if (string.IsNullOrEmpty(state.PdfPath) && !string.IsNullOrEmpty(oldState.PdfPath))
                                state.PdfPath = oldState.PdfPath;
                            if (string.IsNullOrEmpty(state.PdfFileName) && !string.IsNullOrEmpty(oldState.PdfFileName))
                                state.PdfFileName = oldState.PdfFileName;
                            if (string.IsNullOrEmpty(state.CreatedPoNumber) && !string.IsNullOrEmpty(oldState.CreatedPoNumber))
                            {
                                state.CreatedPoNumber = oldState.CreatedPoNumber;
                                state.CreatedPoId = oldState.CreatedPoId;
                                state.CreatedPoUrl = oldState.CreatedPoUrl;
                            }
                            if (string.IsNullOrEmpty(state.ProjectCodeText) && !string.IsNullOrEmpty(oldState.ProjectCodeText))
                            {
                                state.ProjectCodeText = oldState.ProjectCodeText;
                            }
                            if (string.IsNullOrEmpty(state.ProjectCode) && !string.IsNullOrEmpty(oldState.ProjectCode))
                            {
                                state.ProjectCode = oldState.ProjectCode;
                            }
                        }
                    }
                    catch { }
                }

                state.ModifiedOn = DateTime.Now;
                if (string.IsNullOrEmpty(state.CreatedBy))
                {
                    state.CreatedBy = currentUser?.UserName ?? "System";
                }

                string stateJson = JsonConvert.SerializeObject(state);
                int result = logger.SaveTransactionState(txGuid, stateJson, state.Status ?? "PROCESSING", currentUser?.UserName ?? "System");

                return Json(new { success = result > 0, transactionId = state.TransactionId });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetTransactionState(string transactionId)
        {
            if (string.IsNullOrEmpty(transactionId) || !Guid.TryParse(transactionId, out Guid txGuid))
            {
                return Json(new { success = false, message = "TransactionId không hợp lệ" });
            }

            try
            {
                var log = logger.GetTransactionState(txGuid);
                if (log == null || string.IsNullOrEmpty(log.Content))
                {
                    return Json(new { success = false, message = "Không tìm thấy dữ liệu tiến trình" });
                }

                var state = JsonConvert.DeserializeObject<POTransactionStateModel>(log.Content);
                return Json(new { success = true, data = state });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult LogPoEdits([FromBody] PoEditLogRequest request)
        {
            var currentUser = _permissionService.GetCurrentUser();
            try
            {
                if (request != null)
                {
                    string logMessage = $"[PO EDIT AUDIT] PO: {request.PONumber} | BEFORE: {request.BeforeState} | AFTER: {request.AfterState}";
                    logger.Log(LogService.LogApp.SigmaTools, LogService.LogAction.ConfigPOManual, LogService.LogType.Info, logMessage, currentUser.UserName);
                }
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult ExportPoTrackingToExcel([FromBody] ExportTrackingRequest request)
        {
            if (!User.Identity?.IsAuthenticated ?? true)
            {
                return Unauthorized(new { success = false, message = "Unauthorized" });
            }

            try
            {
                // 1. Chuẩn hóa khoảng thời gian
                DateTime fDate = request?.FromDate ?? new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                DateTime tDate = request?.ToDate ?? DateTime.Now.Date;

                if (fDate.Date > DateTime.Now.Date) fDate = DateTime.Now.Date;
                if (tDate.Date > DateTime.Now.Date) tDate = DateTime.Now.Date;
                tDate = tDate.Date.AddHours(23).AddMinutes(59).AddSeconds(59);

                if (fDate > tDate) fDate = tDate.Date;

                // 2. Lấy toàn bộ dữ liệu log từ Service
                var logList = logger.GetLogs(LogService.LogAction.NetsuitePOLogInfo, fDate, tDate);
                var allTrackingItems = new List<PoOrderTrackingDisplayModel>();

                if (logList != null)
                {
                    foreach (var log in logList)
                    {
                        if (string.IsNullOrEmpty(log.Content)) continue;
                        try
                        {
                            var items = JsonConvert.DeserializeObject<List<PoOrderTrackingDto>>(log.Content);
                            if (items != null && items.Count > 0)
                            {
                                foreach (var item in items)
                                {
                                    allTrackingItems.Add(new PoOrderTrackingDisplayModel
                                    {
                                        CreatedOn = log.CreatedOn,
                                        CreatedBy = log.CreatedBy ?? "System",
                                        PartNumber = item.PartNumber,
                                        SigmaPO = item.SigmaPO,
                                        Description = item.Description,
                                        OrderQty = item.OrderQty,
                                        OpenOrderQty = item.OpenOrderQty,
                                        PoUrl = item.PoUrl
                                    });
                                }
                            }
                        }
                        catch { continue; }
                    }
                }

                allTrackingItems = allTrackingItems.OrderByDescending(x => x.CreatedOn).ToList();

                // 3. Khởi tạo File Excel bằng ClosedXML
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("PO Tracking History");

                    // Tạo Header
                    worksheet.Cell(1, 1).Value = "Created Time";
                    worksheet.Cell(1, 2).Value = "Creator";
                    worksheet.Cell(1, 3).Value = "PO Number";
                    worksheet.Cell(1, 4).Value = "Part Number";
                    worksheet.Cell(1, 5).Value = "Description";
                    worksheet.Cell(1, 6).Value = "Order Qty";
                    worksheet.Cell(1, 7).Value = "Open Order Qty";
                    worksheet.Cell(1, 8).Value = "NetSuite Link";

                    // Định dạng Header
                    var headerRow = worksheet.Row(1);
                    headerRow.Style.Font.Bold = true;
                    headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#007BFF");
                    headerRow.Style.Font.FontColor = XLColor.White;

                    // Đổ dữ liệu vào các dòng
                    int rowIdx = 2;
                    foreach (var item in allTrackingItems)
                    {
                        worksheet.Cell(rowIdx, 1).Value = item.CreatedOn.ToString("yyyy-MM-dd HH:mm:ss");
                        worksheet.Cell(rowIdx, 2).Value = item.CreatedBy;
                        worksheet.Cell(rowIdx, 3).Value = item.SigmaPO;
                        worksheet.Cell(rowIdx, 4).Value = item.PartNumber;
                        worksheet.Cell(rowIdx, 5).Value = item.Description;
                        worksheet.Cell(rowIdx, 6).Value = item.OrderQty;
                        worksheet.Cell(rowIdx, 7).Value = item.OpenOrderQty;
                        worksheet.Cell(rowIdx, 8).Value = item.PoUrl;

                        rowIdx++;
                    }

                    worksheet.Columns().AdjustToContents();

                    // Xuất ra Stream
                    using (var stream = new MemoryStream())
                    {
                        workbook.SaveAs(stream);
                        var content = stream.ToArray();
                        string fileName = $"PO_Tracking_History_{fDate:yyyyMMdd}_{tDate:yyyyMMdd}.xlsx";

                        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = "Lỗi khi xuất file Excel: " + ex.Message });
            }
        }
    }

    public class PoEditLogRequest
    {
        public string PONumber { get; set; }
        public string BeforeState { get; set; }
        public string AfterState { get; set; }
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

    // Request Model dùng để nhận dữ liệu từ bước Create PO
    public class CreateNetsuiteSORequest
    {
        public string? TransactionId { get; set; }
        public string PONumber { get; set; }
        public string Date { get; set; }
        public string Customer { get; set; }
        public string Requisitioner { get; set; }
        public string Currency { get; set; }

        // Các trường cấu hình NetSuite bổ sung
        public int Subsidiary { get; set; }
        public int Location { get; set; }
        public int TaxCode { get; set; }
        public int ProjectCode { get; set; }
        public string? ProjectCodeText { get; set; }

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

    public class CreateSalesOrderRequest
    {
        public string? TransactionId { get; set; }
        public string PONumber { get; set; }
        public string CustomerPONumber { get; set; }
        public string Customer { get; set; }
        public string Date { get; set; }
        public string Currency { get; set; }
        public int Subsidiary { get; set; }
        public int Location { get; set; }
        public int TaxCode { get; set; }
        public int ProjectCode { get; set; }
        public int CreatedPoId { get; set; }
        public string CreatedPoUrl { get; set; }
        public List<POLineItem> LineItems { get; set; }
        public string RawOcrResponse { get; set; }

        // Bổ sung các tham số theo logic "SO in ERP"
        public string? PersonInCharge { get; set; } // No. 7: DRI PM
        public int? PersonInChargeId { get; set; } // No. 7: DRI PM Employee ID
        public string? PoType { get; set; } = "MP"; // No. 11: MP or NPI
        public string? PrType { get; set; } = "DRI"; // No. 12: DRI for purchasing team
        public string? ProjectCodeStr { get; set; } // No. 10: 3 digits
        public string? ProjectCodeText { get; set; }
        public bool AllowCrossSubsidiaryFulfillment { get; set; } = true; // No. 14
        public string? InventoryLocation { get; set; } = "Sigma Asia"; // No. 16: Sigma Asia
        public string? CustomForm { get; set; } = "111"; // No. 1: Sigma Worldwide Sales order (111)
    }

    public class CreateSalesOrderResponse
    {
        public bool success { get; set; }
        public string message { get; set; }
        public int? soId { get; set; }
        public string soNumber { get; set; }
        public string soUrl { get; set; }
    }

    public class PoOrderTrackingDto
    {
        public string PartNumber { get; set; }
        public string SigmaPO { get; set; }
        public string Description { get; set; }
        public decimal OrderQty { get; set; }
        public decimal OpenOrderQty { get; set; }
        public string PoUrl { get; set; } // Thêm thuộc tính lưu URL PO
    }

    public class ExportTrackingRequest
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }

    public class PersonInChargeItem
    {
        public object id { get; set; }
        public string name { get; set; }
        public string entityId { get; set; }
        public string email { get; set; }
        public string title { get; set; }
    }

    public class PersonInChargeApiResponse
    {
        public bool success { get; set; }
        public int total { get; set; }
        public List<PersonInChargeItem> employees { get; set; }
        public string message { get; set; }
    }
}
