using Newtonsoft.Json;
using SVN_Portal.Services.Configurations;
using SVNShareLib;
using SVNShareLib.Request;

namespace SVN_Portal.Areas.TwistAdmin.Services
{
    // Lấy thông tin WO từ Viindoo qua ViidooDBServiceAPI (cùng API trang Twist dùng ở TwistController.GetWOInfo)
    public class WorkOrderLookupService
    {
        const string WoPrefix = "NM/MO/";
        const int TimeoutSeconds = 30;

        readonly APIConfiguration apiConfig;

        public WorkOrderLookupService(APIConfiguration apiConfig)
        {
            this.apiConfig = apiConfig;
        }

        // Trả về (wo, lỗi). wo gồm: name, product_code, product_name, product_qty, product_id, origin
        public async Task<(Dictionary<string, string>? wo, string? error)> GetAsync(string woCode)
        {
            woCode = (woCode ?? "").Trim();
            if (woCode.Length == 0) return (null, "Vui lòng nhập mã WO.");
            if (!woCode.Contains(WoPrefix, StringComparison.OrdinalIgnoreCase)) woCode = WoPrefix + woCode;

            var http = new HttpClientHelper<BODataProcessResult>(apiConfig.BaseURL, TimeoutSeconds);
            var request = new InputProductDataRequest { WorkOrderNumber = woCode, LotNumber = "" };
            var result = await http.PostRequest("api/ViindooConnect/GetWorkOrder", request, CancellationToken.None);
            if (result == null) return (null, "Không tìm thấy WO.");
            if (!result.OK) return (null, result.Message ?? "Không lấy được thông tin WO.");

            var info = JsonConvert.DeserializeObject<WorkOrderInfo>(result.Content?.ToString() ?? "");
            if (info?.OrderInfo == null) return (null, "Dữ liệu WO không hợp lệ.");

            var order = info.OrderInfo;
            // product_name dạng "[7100405899] Decor Metal Key Rail..." -> tách mã và tên
            string rawName = order.TryGetValue("product_name", out string? n) ? n ?? "" : "";
            string code = "", name = rawName;
            int start = rawName.IndexOf('['), end = rawName.IndexOf(']');
            if (start != -1 && end > start)
            {
                code = rawName.Substring(start + 1, end - start - 1).Trim();
                name = rawName.Substring(end + 1).Trim();
            }

            var wo = new Dictionary<string, string>
            {
                ["name"] = order.TryGetValue("name", out string? woName) && !string.IsNullOrEmpty(woName) ? woName : woCode,
                ["product_code"] = code,
                ["product_name"] = name,
                ["product_qty"] = FormatQty(order.TryGetValue("product_qty", out string? qty) ? qty : null),
                ["product_id"] = order.TryGetValue("product_id", out string? pid) ? pid ?? "" : "",
                ["origin"] = order.TryGetValue("origin", out string? origin) && origin != "False" ? origin ?? "" : ""
            };
            return (wo, null);
        }

        // Odoo trả số lượng dạng "1200.0" -> "1200"
        static string FormatQty(string? qty) =>
            decimal.TryParse(qty, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal d)
                ? d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)
                : qty ?? "";
    }
}
