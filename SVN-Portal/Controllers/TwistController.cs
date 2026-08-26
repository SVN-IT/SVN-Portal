using DocumentFormat.OpenXml.Bibliography;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using PrinterServices.Objects;
using SVN_Portal.DAL.DataPortal;
using SVN_Portal.Services.Configurations;
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

        public TwistController(IWebHostEnvironment env, 
            APIConfiguration aPIConfiguration,
            DBConfiguration dBConfiguration)
        {
            _env = env;
            this.aPIConfiguration = aPIConfiguration;
            this.dBConfiguration = dBConfiguration;
            connectionString = dBConfiguration.GetConnectionString();
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
        public IActionResult PrintLabel([FromBody] PrintLabelRequest req)
        {
            // TODO: Xử lý in tem
            return Json(new { success = true });
        }

        // Nhập kết quả sản xuất (giả lập)
        [HttpPost]
        public IActionResult SubmitProduction([FromBody] InputResult req)
        {
            // TODO: Lưu kết quả sản xuất
            return Json(new { success = true });
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
                List<PrinterConfigData> printerConfigDatas = await printerDataPortal.ReadList();
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
}
