using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SVN_Portal.Areas.TwistAdmin.Models;
using SVN_Portal.Areas.TwistAdmin.Services;
using SVN_Portal.DAL.DataPortal;
using SVN_Portal.Services.Configurations;

namespace SVN_Portal.Areas.TwistAdmin.Controllers
{
    // Màn In tem: nhập WO -> chọn mẫu tem -> điền biến {KEY} -> xem trước -> in. Truy cập tại /twist/admin/print
    // Server TCP: server gửi ZPL tới máy in. TCP/USB qua Node: server chỉ trả ZPL đã điền, trình duyệt gửi tới agent localhost.
    [Route("twist/admin/print")]
    public class LabelPrintController : TwistAdminControllerBase
    {
        const int DefaultPort = 9100;
        const int MaxCopies = 100;
        static readonly Regex Ipv4Regex = new(@"^((25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(25[0-5]|2[0-4]\d|1?\d?\d)$");

        readonly string connectionString;
        readonly IOptionsMonitor<LabelVariablesConfig> variablesConfig;
        readonly APIConfiguration apiConfig;
        readonly IWebHostEnvironment env;

        public LabelPrintController(DBConfiguration dBConfiguration,
            IOptionsMonitor<LabelVariablesConfig> variablesConfig,
            APIConfiguration apiConfig,
            IWebHostEnvironment env)
        {
            connectionString = dBConfiguration.LocalSvnConnectionString;
            this.variablesConfig = variablesConfig;
            this.apiConfig = apiConfig;
            this.env = env;
        }

        // ===== Trang =====

        [HttpGet("")]
        public async Task<IActionResult> Index()
        {
            return View(await ReadPrinters());
        }

        // ===== API JSON =====

        // Tra WO + danh sách mẫu tem của sản phẩm thuộc WO
        [HttpGet("api/wo")]
        public async Task<IActionResult> GetWorkOrder(string wo)
        {
            try
            {
                var (info, error) = await new WorkOrderLookupService(apiConfig).GetAsync(wo);
                if (info == null) return Json(new { success = false, error });

                var templates = string.IsNullOrEmpty(info["product_code"])
                    ? new List<DAL.DTO.SVN_label_templateUI>()
                    : await new SVN_label_templateDataPortal(connectionString).GetByPartNumber(info["product_code"]);
                return Json(new { success = true, wo = info, templates });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = "Không lấy được thông tin WO: " + ex.Message });
            }
        }

        // Tìm mẫu tem theo part number (in không theo WO)
        [HttpGet("api/templates")]
        public async Task<IActionResult> SearchTemplates(string q)
        {
            if (string.IsNullOrWhiteSpace(q)) return Json(new { success = true, templates = Array.Empty<object>() });
            try
            {
                return Json(new { success = true, templates = await new SVN_label_templateDataPortal(connectionString).SearchByPartNumber(q.Trim()) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Lấy ZPL của mẫu + danh sách biến kèm giá trị tự điền
        [HttpPost("api/resolve")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve([FromBody] LabelPrintResolveRequest req)
        {
            try
            {
                var template = await new SVN_label_templateDataPortal(connectionString).GetById(req.templateId);
                if (template == null) return Json(new { success = false, error = "Không tìm thấy mẫu tem." });

                // Mã SP để tra file JSON: ưu tiên của WO, không có WO thì lấy part number của mẫu
                string productCode = req.wo != null && req.wo.TryGetValue("product_code", out string? code) && !string.IsNullOrEmpty(code)
                    ? code
                    : template.partNumber;
                var variables = NewVariableService().Resolve(template.zplData ?? "", req.wo, productCode, User.Identity?.Name);
                return Json(new { success = true, template, variables });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Trả ZPL đã điền biến (dùng cho chế độ TCP/USB qua Node: trình duyệt tự gửi tới agent)
        [HttpPost("api/render")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Render([FromBody] LabelPrintRenderRequest req)
        {
            var (zpl, error) = await BuildZpl(req);
            return Json(zpl == null ? new { success = false, error, zpl = (string?)null } : new { success = true, error = (string?)null, zpl });
        }

        // Server TCP: server gửi ZPL đã điền tới máy in
        [HttpPost("api/print")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Print([FromBody] LabelPrintSendRequest req)
        {
            if (req.copies < 1 || req.copies > MaxCopies)
                return Json(new { success = false, error = $"Số bản phải từ 1 đến {MaxCopies}." });
            try
            {
                var (zpl, error) = await BuildZpl(req);
                if (zpl == null) return Json(new { success = false, error });

                var (printer, printerError) = await ResolvePrinter(req.printerId, req.ip, req.port);
                if (printer == null) return Json(new { success = false, error = printerError });

                var result = await TcpPrintService.SendAsync(printer.Ip, printer.Port, zpl, req.copies);
                if (!result.Ok) return Json(new { success = false, error = result.Error });

                return Json(new
                {
                    success = true,
                    message = $"Đã gửi {req.copies} bản tới {printer.Name} ({printer.Ip}:{printer.Port})",
                    bytes = result.Bytes,
                    elapsedMs = result.ElapsedMs
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Kiểm tra máy in có phản hồi không (chỉ mở/đóng kết nối, không in)
        [HttpPost("api/test-connection")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestConnection([FromBody] LabelPrintTestRequest req)
        {
            var (printer, error) = await ResolvePrinter(req.printerId, req.ip, req.port);
            if (printer == null) return Json(new { success = false, error });

            var result = await TcpPrintService.TestAsync(printer.Ip, printer.Port);
            return Json(new { success = result.Ok, error = result.Error, elapsedMs = result.ElapsedMs });
        }

        // ===== Helpers =====

        LabelVariableService NewVariableService() => new(variablesConfig.CurrentValue, env.WebRootPath);

        // ZPL lấy lại từ DB theo templateId (không tin ZPL từ trình duyệt), rồi điền biến
        async Task<(string? zpl, string? error)> BuildZpl(LabelPrintRenderRequest req)
        {
            var template = await new SVN_label_templateDataPortal(connectionString).GetById(req.templateId);
            if (template == null) return (null, "Không tìm thấy mẫu tem.");

            string? error = NewVariableService().Apply(template.zplData ?? "", req.values, out string zpl);
            return error == null ? (zpl, null) : (null, error);
        }

        // printerId có giá trị: tra trong SVN_Printer_Info. Không có: dùng IP/Port nhập tay.
        async Task<(LabelPrintPrinter? printer, string? error)> ResolvePrinter(string? printerId, string? ip, int? port)
        {
            if (!string.IsNullOrWhiteSpace(printerId))
            {
                var printer = (await ReadPrinters()).FirstOrDefault(p => p.Id == printerId);
                return printer == null ? (null, "Không tìm thấy máy in trong hệ thống.") : (printer, null);
            }

            ip = (ip ?? "").Trim();
            int p = port ?? DefaultPort;
            if (!Ipv4Regex.IsMatch(ip)) return (null, "Địa chỉ IP không hợp lệ.");
            if (p < 1 || p > 65535) return (null, "Port phải từ 1 đến 65535.");
            return (new LabelPrintPrinter { Id = "", Name = "Máy in nhập tay", Ip = ip, Port = p }, null);
        }

        async Task<List<LabelPrintPrinter>> ReadPrinters()
        {
            var list = await new SVN_Printer_InfoDataPortal(connectionString).ReadList();
            return (list ?? new())
                .Where(p => !string.IsNullOrWhiteSpace(p.ID_Printer) && !string.IsNullOrWhiteSpace(p.IP_Printer))
                .Select(p => new LabelPrintPrinter
                {
                    Id = p.ID_Printer,
                    Name = string.IsNullOrWhiteSpace(p.Name_Printer) ? p.ID_Printer : p.Name_Printer,
                    Ip = p.IP_Printer.Trim(),
                    Port = int.TryParse(p.Port_Printer, out int port) && port > 0 && port <= 65535 ? port : DefaultPort
                })
                .OrderBy(p => p.Name)
                .ToList();
        }
    }
}
