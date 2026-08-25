using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace SVN_Portal.Controllers
{
    public class TwistController : Controller
    {
        private readonly IWebHostEnvironment _env;

        public TwistController(IWebHostEnvironment env)
        {
            _env = env;
        }

        public IActionResult Index()
        {
            return View();
        }

        // Lấy thông tin WO (giả lập)
        [HttpGet]
        public IActionResult GetWOInfo(string wo)
        {
            // TODO: Lấy từ DB thực tế
            if (wo == "WO123")
                return Json(new { productCode = "7100406070", quantity = 100 });
            return Json(new { error = "WO không hợp lệ" });
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

        // In tem (giả lập)
        [HttpPost]
        public IActionResult PrintLabel([FromBody] PrintLabelRequest req)
        {
            // TODO: Xử lý in tem
            return Json(new { success = true });
        }

        // Nhập kết quả sản xuất (giả lập)
        [HttpPost]
        public IActionResult SubmitProduction([FromBody] PrintLabelRequest req)
        {
            // TODO: Lưu kết quả sản xuất
            return Json(new { success = true });
        }

        public Dictionary<string, List<string>> GetData()
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
    }

    public class PrintLabelRequest
    {
        public string wo { get; set; }
        public List<string> pcsList { get; set; }
    }
}
