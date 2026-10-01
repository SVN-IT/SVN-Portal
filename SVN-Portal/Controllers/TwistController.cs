using Dapper;
using DocumentFormat.OpenXml.Bibliography;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PrinterServices.Objects;
using SVN_Portal.DAL.DataPortal;
using SVN_Portal.DAL.DTO;
using SVN_Portal.Services.Configurations;
using SVN_Portal.Services.Helpers;
using SVNShareLib;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
using SVNShareLib.Request;
using System.Linq.Expressions;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace SVN_Portal.Controllers
{
    public class TwistController : Controller
    {
        private readonly IWebHostEnvironment _env;
        APIConfiguration aPIConfiguration;
        string connectionString;
        DBConfiguration dBConfiguration;
        ToolsHelper toolsHelper;

        public TwistController(IWebHostEnvironment env,
            ToolsHelper toolsHelper,
            APIConfiguration aPIConfiguration,
            DBConfiguration dBConfiguration)
        {
            _env = env;
            this.aPIConfiguration = aPIConfiguration;
            this.dBConfiguration = dBConfiguration;
            connectionString = dBConfiguration.LocalSvnConnectionString;
            this.toolsHelper = toolsHelper;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Dashboard()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardData(string date)
        {
            try
            {
                DateTime targetDate = string.IsNullOrEmpty(date)
                    ? DateTime.Today
                    : DateTime.Parse(date);

                var fromDate   = targetDate.Date;
                var toDate     = targetDate.Date.AddDays(1).AddSeconds(-1);
                var dateStr    = targetDate.ToString("yyyyMMdd");

                var localConn      = dBConfiguration.LocalSvnConnectionString;
                var printLogPortal = new SVN_PrintLogTwistDataPortal(localConn);
                var inputLogPortal = new SVN_ProductionInputLogDataPortal(localConn);
                var viindooPortal  = new SVN_production_resultDataPortal(localConn);

                // 1. Viindoo — lọc Twist theo ngày, lấy product codes chạy hôm nay
                var viindooList = await viindooPortal.ReadList(dateStr);
                var twistViindoo = viindooList?
                    .Where(v => v.Operation != null && v.Operation.StartsWith("Twist-") && v.Type_value == "Production Qty")
                    .ToList() ?? new List<SVN_production_resultUI>();

                var viindooQtyByCode = twistViindoo
                    .GroupBy(v => v.Operation.Replace("Twist-", "").Trim())
                    .ToDictionary(g => g.Key,
                        g => (int)g.Sum(v => v.Time1 + v.Time2 + v.Time3 + v.Time4 + v.Time5 + v.Time6));

                // Product codes hôm nay
                var allCodes = viindooQtyByCode.Keys.ToList();

                // 2. ProductMapping — dùng LocalSvnConnectionString (10.10.99.10, svn_pentaho)
                var codeToProductId = new Dictionary<string, int>();
                List<object> rawMapSample;
                using (System.Data.IDbConnection conn = new System.Data.SqlClient.SqlConnection(dBConfiguration.LocalSvnConnectionString))
                {
                    var mapRows = await conn.QueryAsync(
                        "SELECT product_id, Operation FROM dbo.ProductMapping WHERE Operation LIKE 'Twist%'");
                    var mapList = mapRows.ToList();
                    rawMapSample = mapList.Select(r => (object)new {
                        pid = r.product_id,
                        op  = r.Operation?.ToString() ?? ""
                    }).ToList<object>();
                    foreach (var r in mapList)
                    {
                        string op = r.Operation?.ToString() ?? "";
                        if (op.StartsWith("Twist", StringComparison.OrdinalIgnoreCase) && op.Length > 6)
                        {
                            // bỏ qua "Twist" + 1 ký tự phân cách (-, –, —, space...)
                            string code = op.Substring(6).Trim();
                            int id = Convert.ToInt32(r.product_id);
                            codeToProductId[code] = id;
                        }
                    }
                }

                // 3. Input logs theo ngày (filter date_finished), group by product_id
                var inputLogs       = await inputLogPortal.GetDataFromDateToDateFinishedAsync(fromDate, toDate, "Not synchronized");
                var inputLogsSynced = await inputLogPortal.GetDataFromDateToDateFinishedAsync(fromDate, toDate, "Synchronized");
                var allInputLogs    = (inputLogs ?? new List<SVN_ProductionInputLogUI>())
                    .Concat(inputLogsSynced ?? new List<SVN_ProductionInputLogUI>())
                    .ToList();

                var inputByProductId = allInputLogs
                    .GroupBy(x => x.product_id)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.product_qty));

                var inputCountByProductId = allInputLogs
                    .GroupBy(x => x.product_id)
                    .ToDictionary(g => g.Key, g => g.Count());

                var woByProductId = allInputLogs
                    .GroupBy(x => x.product_id)
                    .ToDictionary(g => g.Key, g =>
                    {
                        var wos = g.Select(x => x.master_wo_code)
                                   .Where(w => !string.IsNullOrWhiteSpace(w))
                                   .Distinct().ToList();
                        // nếu có code dạng NM/MO/xxxxx thì chỉ lấy loại đó, bỏ code số thuần
                        var fullWos = wos.Where(w => w.Contains("/")).ToList();
                        return string.Join(", ", fullWos.Any() ? fullWos : wos);
                    });

                // 4. Print log summary theo ngày
                List<SVN_PrintLogTwistUI> printSummary = new List<SVN_PrintLogTwistUI>();
                try { printSummary = await printLogPortal.GetSummaryByDateAsync(targetDate) ?? printSummary; } catch { }
                var printByProductId    = printSummary.ToDictionary(p => p.product_id, p => p.print_qty);
                var printByCode        = printSummary.ToDictionary(p => p.product_code, p => p.print_qty);
                var printCountByPid    = printSummary.ToDictionary(p => p.product_id, p => p.print_count);
                var printCountByCode   = printSummary.ToDictionary(p => p.product_code, p => p.print_count);

                var rows = allCodes.Select(code =>
                {
                    int pid        = codeToProductId.ContainsKey(code) ? codeToProductId[code] : 0;
                    int viindooQty = viindooQtyByCode.ContainsKey(code) ? viindooQtyByCode[code] : 0;
                    int inputQty   = pid > 0 && inputByProductId.ContainsKey(pid) ? (int)inputByProductId[pid] : 0;
                    int inputCount = pid > 0 && inputCountByProductId.ContainsKey(pid) ? inputCountByProductId[pid] : 0;
                    string woCodes = pid > 0 && woByProductId.ContainsKey(pid) ? woByProductId[pid] : "";
                    int printQty   = pid > 0 && printByProductId.ContainsKey(pid)
                                        ? printByProductId[pid]
                                        : printByCode.ContainsKey(code) ? printByCode[code] : 0;
                    int printCount = pid > 0 && printCountByPid.ContainsKey(pid)
                                        ? printCountByPid[pid]
                                        : printCountByCode.ContainsKey(code) ? printCountByCode[code] : 0;

                    return new
                    {
                        product_code      = code,
                        product_id        = pid,
                        wo_codes          = woCodes,
                        print_qty         = printQty,
                        print_count       = printCount,
                        input_qty         = inputQty,
                        input_count       = inputCount,
                        viindoo_qty       = viindooQty,
                        gap_print_input   = printQty - inputQty,
                        gap_input_viindoo = inputQty - viindooQty,
                        gap_print_viindoo = printQty - viindooQty
                    };
                })
                .OrderBy(r => r.product_code)
                .ToList();

                // Debug: check Twist records in input logs (no date/status filter)
                IEnumerable<dynamic> twistInputSample = new List<dynamic>();
                var twistPidList = string.Join(",", codeToProductId.Values.Distinct());
                if (!string.IsNullOrEmpty(twistPidList))
                {
                    using var dbDebug = new System.Data.SqlClient.SqlConnection(connectionString);
                    twistInputSample = await dbDebug.QueryAsync(
                        $"SELECT TOP 10 product_id, product_qty, master_wo_code, date_finished, status FROM SVN_ProductionInputLogs WHERE product_id IN ({twistPidList}) ORDER BY date_finished DESC");
                }

                return Json(new {
                    success = true,
                    data = rows,
                    date = targetDate.ToString("yyyy-MM-dd"),
                    _debug = new {
                        mappingCount     = codeToProductId.Count,
                        inputLogsCount   = allInputLogs.Count,
                        twistInputSample = twistInputSample
                    }
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Lấy thông tin WO (giả lập)
        [HttpGet]
        public async Task<IActionResult> GetWOInfo(string wo)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(wo) && !wo.Contains("NM/MO/"))
                {
                    wo = $"NM/MO/{wo}";
                }
                string error = string.Empty;
                HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
                InputProductDataRequest dataRequest = new InputProductDataRequest()
                {
                    WorkOrderNumber = wo,
                    LotNumber = ""
                };
                var result = await httpClientHelper.PostRequest("api/ViindooConnect/GetWorkOrder", dataRequest, new CancellationToken(false));
                if (result != null)
                {
                    if (result.OK)
                    {
                        string woJsonContent = result.Content.ToString();
                        WorkOrderInfo workOrderInfo = JsonConvert.DeserializeObject<WorkOrderInfo>(woJsonContent);

                        string input = workOrderInfo.OrderInfo["product_name"];
                        string code = "";
                        string name = "";

                        int start = input.IndexOf('[');
                        int end = input.IndexOf(']');
                        if (start != -1 && end != -1 && end > start)
                        {
                            code = input.Substring(start + 1, end - start - 1).Trim();
                            name = input.Substring(end + 1).Trim();
                        }

                        int product_qty = workOrderInfo.OrderInfo.ContainsKey("product_qty") ? Convert.ToInt32(workOrderInfo.OrderInfo["product_qty"]) : 0;
                        int product_id = workOrderInfo.OrderInfo.ContainsKey("product_id") ? Convert.ToInt32(workOrderInfo.OrderInfo["product_id"]) : 0;

                        return Json(new { woCode = wo, productCode = code, quantity = product_qty, productName = name, product_id = product_id });
                    }
                    else
                    {
                        error = result.Message;
                        return Json(new { error = error });
                    }
                }
                else
                {
                    error = "Không tìm thấy WO.";
                    return Json(new { error = error });
                }
            }
            catch (Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        // Lấy số PCS/hộp từ file JSON
        [HttpGet]
        public IActionResult GetPcsPerBox(string productCode)
        {
            var twistData = GetData();
            if (twistData != null && twistData.TryGetValue(productCode, out var arr))
            {
                bool isEU = arr.Count > 1 && arr[1].EndsWith("-EU", StringComparison.OrdinalIgnoreCase);
                string poNumber = isEU ? GetPOForCode(productCode) : "";
                return Json(new {
                    pcsPerBox = int.Parse(arr[2]),
                    isEU = isEU,
                    skuName = arr.Count > 1 ? arr[1] : "",
                    poNumber = poNumber
                });
            }
            return Json(new { error = "Không tìm thấy mã sản phẩm" });
        }

        [HttpGet]
        public IActionResult GetEUCodes()
        {
            var twistData = GetData();
            var poData = GetPOData();
            if (twistData == null) return Json(new List<object>());

            var euCodes = twistData
                .Where(kvp => kvp.Value.Count > 1 && kvp.Value[1].EndsWith("-EU", StringComparison.OrdinalIgnoreCase))
                .Select(kvp => new {
                    code = kvp.Key,
                    skuName = kvp.Value[1],
                    barcode = kvp.Value[0],
                    poNumber = poData.TryGetValue(kvp.Key, out var po) ? (po ?? "") : ""
                })
                .ToList();

            return Json(euCodes);
        }

        [HttpPost]
        public IActionResult SaveEUPO([FromBody] SaveEUPORequest req)
        {
            try
            {
                string filePath = Path.Combine(_env.WebRootPath, "data", "twist_infoPO_EU.json");
                var poData = GetPOData();
                poData[req.code] = req.poNumber ?? "";
                System.IO.File.WriteAllText(filePath, JsonConvert.SerializeObject(poData, Formatting.Indented));
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult ValidatePcs(string productCode, string pcs)
        {
            var twistData = GetData();
            if (twistData != null && twistData.TryGetValue(productCode, out var arr))
            {
                // Kiểm tra mã PCS scan được (arr[0] chứa mã Barcode)
                if (arr.Count > 0 && arr[0].Equals(pcs, StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { isValid = true });
                }
                else
                {
                    return Json(new { isValid = false, isMismatch = true, error = $"Mã sản phẩm quét không khớp với mã sản phẩm {productCode}!" });
                }
            }

            return Json(new { isValid = false, error = "Không tìm thấy mã sản phẩm trong hệ thống!" });
        }

        [HttpPost]
        public async Task<IActionResult> PrintLabel([FromBody] PrintLabelRequest req)
        {
            SVN_label_templateDataPortal dataPortal = new SVN_label_templateDataPortal(connectionString);
            SVN_PrintLogTwistDataPortal printLogDataPortal = new SVN_PrintLogTwistDataPortal(dBConfiguration.LocalSvnConnectionString);
            try
            {
                var labelInfo = await dataPortal.ReadByID(req.productCode, "Box", "Twist");
                if (labelInfo == null) 
                {
                    //Nếu trong trường hợp là con EMEA thì không cần phải in tem box
                    var twistData = GetData();
                    bool isEmea = false;
                    // Kiểm tra xem mã sản phẩm này có phải là dòng EMEA hay không
                    if (twistData != null && twistData.TryGetValue(req.productCode, out var arr))
                    {
                        if (arr.Count > 1 && arr[1].Equals("EMEA", StringComparison.OrdinalIgnoreCase))
                        {
                            isEmea = true;
                        }
                    }
                    // Nếu LÀ hàng EMEA -> Cho qua (Success), báo thông tin không cần in tem Box
                    if (isEmea)
                    {
                        return Json(new { success = true });
                    }

                    // Nếu NÃO PHẢI hàng EMEA mà lại không có tem -> Báo lỗi thiếu mẫu tem
                    return Json(new { success = false, error = $"Không có mẫu tem để in cho partNumber {req.productCode}" });
                }

                string zplData = (labelInfo.zplData ?? "").Replace("{PO}", req.poNumber ?? "");
                var printResult = toolsHelper.PrintTwistLabelByTCP(zplData, req.printerConfig, 1);
                if (printResult == null)
                {
                    return Json(new { success = false, error = "Print failed" });
                }

                if (!printResult.OK)
                {
                    return Json(new { success = false, error = printResult.Message });
                }

                // Lưu log in tem — lỗi ở đây không được block luồng in chính
                try
                {
                    await printLogDataPortal.InsertAsync(new SVN_PrintLogTwistUI
                    {
                        wo_code      = req.woCode,
                        product_id   = req.productId,
                        product_code = req.productCode,
                        print_qty    = req.printQty,
                        print_time   = DateTime.Now
                    });
                }
                catch { /* bảng chưa tạo hoặc lỗi log — không ảnh hưởng luồng in */ }

                return Json(new { success = true });
            }
            catch (Exception ex) 
            {
                return Json(new { success = false, error = ex.Message });
            }
            
        }

        // Nhập kết quả sản xuất (giả lập)
        [HttpPost]
        public async Task<IActionResult> SubmitProduction([FromBody] InputResult req)
        {
            SVN_ProductionInputLogDataPortal dataPortal = new SVN_ProductionInputLogDataPortal(dBConfiguration.LocalSvnConnectionString);
            //HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
            try
            {
                InputProductDataRequest dataRequest = new InputProductDataRequest()
                {
                    WorkOrderNumber = req.wo,
                    LotNumber = "",
                    Quality = req.productQty
                };
                SVN_ProductionInputLogUI productDataUI = new SVN_ProductionInputLogUI();
                productDataUI.wo_code = req.wo;
                productDataUI.product_id = req.productId;
                productDataUI.serial_code = "";
                productDataUI.master_wo_code = req.wo;
                productDataUI.product_qty = req.productQty;
                productDataUI.product_type = "none";
                productDataUI.date_finished = DateTime.Now;
                productDataUI.state = "Used";
                productDataUI.API_function = $"{aPIConfiguration.BaseURL}{aPIConfiguration.InputProductionByWorkOrderv1URL}";
                productDataUI.API_parameters = JsonConvert.SerializeObject(dataRequest);
                productDataUI.status = "Not synchronized";
                var insertResult = await dataPortal.InsertAsync(productDataUI);
                if (insertResult <= 0)
                {
                    return Json(new { success = false, error = "Input production result failed" });
                }


                //var result = await httpClientHelper.PostRequest(aPIConfiguration.InputProductionByWorkOrderv1URL, dataRequest, new CancellationToken(false));
                //if(result == null)
                //{
                //    return Json(new { success = false, error = "Input production result failed" });
                //}

                //if (!result.OK)
                //{
                //    return Json(new { success = false, error = result.Message });
                //}

                // TODO: Lưu kết quả sản xuất
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        private Dictionary<string, List<string>> GetData()
        {
            string filePath = Path.Combine(_env.WebRootPath, "data", "twist_sku_alias.json");
            if (!System.IO.File.Exists(filePath)) return null;
            string jsonContent = System.IO.File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(jsonContent);
        }

        private Dictionary<string, string> GetPOData()
        {
            string filePath = Path.Combine(_env.WebRootPath, "data", "twist_infoPO_EU.json");
            if (!System.IO.File.Exists(filePath)) return new Dictionary<string, string>();
            string json = System.IO.File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }

        private string GetPOForCode(string productCode)
        {
            var poData = GetPOData();
            return poData.TryGetValue(productCode, out var po) ? (po ?? "") : "";
        }

        [HttpGet]
        public async Task<IActionResult> GetPrinters()
        {
            SVN_Printer_InfoDataPortal printerDataPortal = new SVN_Printer_InfoDataPortal(dBConfiguration.LocalSvnConnectionString);
            try
            {
                // Ưu tiên máy in loại Twist, nếu không có thì lấy tất cả
                List<PrinterConfigData> printerConfigDatas = await printerDataPortal.ReadListByType("Twist");
                if (printerConfigDatas == null || printerConfigDatas.Count == 0)
                {
                    printerConfigDatas = await printerDataPortal.ReadList();
                }
                if (printerConfigDatas != null && printerConfigDatas.Count > 0)
                {
                    return Json(printerConfigDatas);
                }
                else
                {
                    return Json(new { error = "Không tìm thấy máy in trong hệ thống." });
                }
            }
            catch(Exception ex)
            {
                return Json(new { error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SavePrinterConfig([FromBody] PrinterConfigData printer)
        {
            try
            {
                if (printer == null || string.IsNullOrEmpty(printer.ID_Printer))
                {
                    return Json(new { success = false, message = "Dữ liệu máy in không hợp lệ!" });
                }

                SVN_Printer_InfoDataPortal printerDataPortal = new SVN_Printer_InfoDataPortal(dBConfiguration.LocalSvnConnectionString);
                int result = await printerDataPortal.Update(printer);
                if (result > 0)
                {
                    return Json(new { success = true, message = "Lưu cấu hình máy in thành công!" });
                }
                else
                {
                    return Json(new { success = false, message = "Không tìm thấy máy in để cập nhật." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Lấy danh sách mã sản phẩm từ file json để hiển thị lên Selectbox in lại
        [HttpGet]
        public IActionResult GetProductCodes()
        {
            var twistData = GetData();
            if (twistData != null)
            {
                var keys = twistData.Keys.ToList();
                return Json(keys);
            }
            return Json(new List<string>());
        }

        // Action xử lý in lại tem
        [HttpPost]
        public async Task<IActionResult> PrintLabelReprint([FromBody] PrintLabelReprintRequest req)
        {
            SVN_label_templateDataPortal dataPortal = new SVN_label_templateDataPortal(connectionString);
            try
            {
                // Lấy mẫu tem dựa trên productCode, loại tem (Box/Carton)
                var labelInfo = await dataPortal.ReadByID(req.productCode, req.labelType, "Twist");
                if (labelInfo == null)
                {
                    return Json(new { success = false, error = $"Không có mẫu tem {req.labelType} để in cho productCode {req.productCode}" });
                }

                // Gọi hàm in với số lượng bản in req.printQty
                var printResult = toolsHelper.PrintTwistLabelByTCP(labelInfo.zplData, req.printerConfig, req.printQty);
                if (printResult == null)
                {
                    return Json(new { success = false, error = "Print failed" });
                }

                if (!printResult.OK)
                {
                    return Json(new { success = false, error = printResult.Message });
                }

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }
    }

    public class PrintLabelRequest
    {
        public string productCode { get; set; }
        public string woCode { get; set; }
        public int productId { get; set; }
        public int printQty { get; set; }
        public string poNumber { get; set; }
        public PrinterConfigData printerConfig { get; set; }
    }

    public class SaveEUPORequest
    {
        public string code { get; set; }
        public string poNumber { get; set; }
    }

    public class InputResult
    {
        public string wo { get; set; }
        public int productQty { get; set; }
        public int productId { get; set; }
    }

    public class PrintLabelReprintRequest
    {
        public string productCode { get; set; }
        public string labelType { get; set; }
        public int printQty { get; set; }
        public PrinterConfigData printerConfig { get; set; }
    }
}
