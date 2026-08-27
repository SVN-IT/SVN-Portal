using DocumentFormat.OpenXml.Bibliography;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PrinterServices.Objects;
using SVN_Portal.DAL.DataPortal;
using SVN_Portal.Services.Configurations;
using SVN_Portal.Services.Helpers;
using SVNShareLib;
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
            connectionString = dBConfiguration.GetConnectionString();
            this.toolsHelper = toolsHelper;
        }

        public IActionResult Index()
        {
            return View();
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

                        return Json(new { woCode = wo, productCode = code, quantity = product_qty, productName = name });
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
                return Json(new { pcsPerBox = int.Parse(arr[2]) });
            return Json(new { error = "Không tìm thấy mã sản phẩm" });
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
                    return Json(new { isValid = false, error = $"Mã PCS không khớp với mã sản phẩm {productCode}!" });
                }
            }

            return Json(new { isValid = false, error = "Không tìm thấy mã sản phẩm trong hệ thống!" });
        }

        // In tem (giả lập)
        [HttpPost]
        public async Task<IActionResult> PrintLabel([FromBody] PrintLabelRequest req)
        {
            SVN_label_templateDataPortal dataPortal = new SVN_label_templateDataPortal(connectionString);
            try
            {
                var labelInfo = await dataPortal.ReadByID(req.productCode, "Box", "Twist");
                if (labelInfo == null) 
                {
                    return Json(new { success = false, error = $"Không có mẫu tem để in cho partNumber {req.productCode}" });
                }

                var printResult = toolsHelper.PrintTwistLabelByTCP(labelInfo.zplData, req.printerConfig, 1);
                if (printResult == null)
                {
                    return Json(new { success = false, error = "Print failed" });
                }

                if (!printResult.OK) 
                {
                    return Json(new { success = false, error = printResult.Message });
                }    

                // TODO: Xử lý in tem
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
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
            try
            {
                InputProductDataRequest dataRequest = new InputProductDataRequest()
                {
                    WorkOrderNumber = req.wo,
                    LotNumber = "",
                    Quality = req.productQty
                };
                var result = await httpClientHelper.PostRequest(aPIConfiguration.InputProductionByWorkOrderv1URL, dataRequest, new CancellationToken(false));
                if(result == null)
                {
                    return Json(new { success = false, error = "Input production result failed" });
                }

                if (!result.OK)
                {
                    return Json(new { success = false, error = result.Message });
                }

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
            // 1. Tìm đường dẫn tuyệt đối đến file JSON trong wwwroot/data
            string filePath = Path.Combine(_env.WebRootPath, "data", "twist_sku_alias.json");

            // Check nếu file tồn tại
            if (!System.IO.File.Exists(filePath))
            {
                return null;
            }

            // 2. Đọc toàn bộ nội dung file text
            string jsonContent = System.IO.File.ReadAllText(filePath);

            // 3. Deserialize thành Dictionary bằng Newtonsoft.Json
            var result = JsonConvert.DeserializeObject<Dictionary<string, List<string>>>(jsonContent);

            // 4. Sử dụng dữ liệu (Ví dụ: lấy phần tử đầu tiên)
            // string barcode = result["7100406070"][0];

            return result;
        }

        [HttpGet]
        public async Task<IActionResult> GetPrinters()
        {
            SVN_Printer_InfoDataPortal printerDataPortal = new SVN_Printer_InfoDataPortal(connectionString);
            try
            {
                List<PrinterConfigData> printerConfigDatas = await printerDataPortal.ReadListByType("Twist"); //ReadListByType("Twist")
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
        public IActionResult SavePrinterConfig([FromBody] PrinterConfigData printer)
        {
            try
            {
                if (printer == null || string.IsNullOrEmpty(printer.ID_Printer))
                {
                    return Json(new { success = false, message = "Dữ liệu máy in không hợp lệ!" });
                }

                // TODO: Viết code lưu/cập nhật thông tin máy in vào Database tại đây
                // Ví dụ: _printerService.SaveOrUpdate(printer);

                return Json(new { success = true, message = "Lưu cấu hình máy in thành công!" });
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
        public PrinterConfigData printerConfig { get; set; }
    }

    public class InputResult
    {
        public string wo { get; set; }
        public int productQty { get; set; }
    }

    public class PrintLabelReprintRequest
    {
        public string productCode { get; set; }
        public string labelType { get; set; }
        public int printQty { get; set; }
        public PrinterConfigData printerConfig { get; set; }
    }
}
