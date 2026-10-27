using System.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SVN_Portal.Areas.TwistAdmin.Models;
using SVN_Portal.Areas.TwistAdmin.Services;
using SVN_Portal.DAL.DataPortal;
using SVN_Portal.DAL.DTO;
using SVN_Portal.Services.Configurations;

namespace SVN_Portal.Areas.TwistAdmin.Controllers
{
    // Màn hình quản lý mẫu tem (bảng SVN_Label_Template), truy cập tại /twist/admin/label-template
    [Route("twist/admin/label-template")]
    public class LabelTemplateController : TwistAdminControllerBase
    {
        const int PartNumberMaxLength = 50;

        readonly string connectionString;
        readonly IOptionsMonitor<LabelTemplateOptions> options;

        public LabelTemplateController(DBConfiguration dBConfiguration, IOptionsMonitor<LabelTemplateOptions> options)
        {
            connectionString = dBConfiguration.LocalSvnConnectionString;
            this.options = options;
        }

        // ===== Trang =====

        [HttpGet("")]
        public IActionResult Index()
        {
            return View(options.CurrentValue);
        }

        // ===== API JSON =====

        [HttpGet("api")]
        public async Task<IActionResult> GetAll()
        {
            var dataPortal = new SVN_label_templateDataPortal(connectionString);
            try
            {
                return Json(new { success = true, data = await dataPortal.GetAll() });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = "Không tải được danh sách mẫu tem: " + ex.Message });
            }
        }

        [HttpGet("api/{id:int}")]
        public async Task<IActionResult> GetById(int id)
        {
            var dataPortal = new SVN_label_templateDataPortal(connectionString);
            try
            {
                var data = await dataPortal.GetById(id);
                if (data == null)
                {
                    return Json(new { success = false, error = "Không tìm thấy mẫu tem." });
                }
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("api")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] LabelTemplateRequest req)
        {
            var data = Normalize(req);
            string? error = Validate(data, null);
            if (error != null)
            {
                return Json(new { success = false, error });
            }

            var dataPortal = new SVN_label_templateDataPortal(connectionString);
            try
            {
                int id = await dataPortal.Create(data);
                return Json(new { success = true, id, message = "Tạo mới thành công" });
            }
            catch (SqlException ex) when (IsDuplicateKey(ex))
            {
                return Json(new { success = false, error = DuplicateMessage(data) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("api/{id:int}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(int id, [FromBody] LabelTemplateRequest req)
        {
            var dataPortal = new SVN_label_templateDataPortal(connectionString);
            try
            {
                var current = await dataPortal.GetById(id);
                if (current == null)
                {
                    return Json(new { success = false, error = "Không tìm thấy mẫu tem, có thể đã bị xoá." });
                }

                var data = Normalize(req);
                data.id = id;
                data.updatedAt = DateTime.Now;
                string? error = Validate(data, current);
                if (error != null)
                {
                    return Json(new { success = false, error });
                }

                await dataPortal.Update(data);
                return Json(new { success = true, message = "Cập nhật thành công" });
            }
            catch (SqlException ex) when (IsDuplicateKey(ex))
            {
                return Json(new { success = false, error = DuplicateMessage(Normalize(req)) });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        [HttpPost("api/{id:int}/delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var dataPortal = new SVN_label_templateDataPortal(connectionString);
            try
            {
                int affected = await dataPortal.Delete(id);
                if (affected == 0)
                {
                    return Json(new { success = false, error = "Không tìm thấy mẫu tem, có thể đã bị xoá." });
                }
                return Json(new { success = true, message = "Xoá thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // Xem trước hình tem. Không đụng DB: client gửi ZPL đang xem/đang soạn lên.
        [HttpPost("api/preview")]
        [ValidateAntiForgeryToken]
        public IActionResult Preview([FromBody] ZplPreviewRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.zplData))
            {
                return Json(new { success = false, error = "Chưa có nội dung ZPL để xem." });
            }

            // Giống lúc in thật (TwistController.PrintLabel): thay {PO}. Không nhập PO thì giữ nguyên chữ {PO}.
            string zpl = string.IsNullOrEmpty(req.po) ? req.zplData : req.zplData.Replace("{PO}", req.po.Trim());
            try
            {
                var result = ZplPreviewService.Render(zpl, req.size, req.dpmm);
                if (result.TotalLabels == 0)
                {
                    return Json(new { success = false, error = "Không tìm thấy tem nào (thiếu ^XA ... ^XZ?)." });
                }
                return Json(new
                {
                    success = true,
                    images = result.Images,
                    totalLabels = result.TotalLabels,
                    unsupportedCommands = result.UnsupportedCommands,
                    widthMm = result.WidthMm,
                    heightMm = result.HeightMm,
                    dpmm = result.Dpmm
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = "Không vẽ được mẫu tem: " + ex.Message });
            }
        }

        // ===== Helpers =====

        static SVN_label_templateUI Normalize(LabelTemplateRequest req)
        {
            return new SVN_label_templateUI
            {
                partNumber = (req.partNumber ?? "").Trim(),
                type = (req.type ?? "").Trim(),
                operation = (req.operation ?? "").Trim(),
                size = (req.size ?? "").Trim(),
                zplData = req.zplData ?? ""
            };
        }

        // current = bản ghi đang sửa (null khi tạo mới). Giá trị cũ không có trong file cấu hình vẫn được giữ nguyên khi sửa.
        string? Validate(SVN_label_templateUI data, SVN_label_templateUI? current)
        {
            var opt = options.CurrentValue;

            if (string.IsNullOrEmpty(data.partNumber))
                return "Vui lòng nhập part number.";
            if (data.partNumber.Length > PartNumberMaxLength)
                return $"Part number tối đa {PartNumberMaxLength} ký tự.";
            if (!IsAllowed(opt.Types, data.type, current?.type))
                return "Loại tem không hợp lệ.";
            if (!IsAllowed(opt.Operations, data.operation, current?.operation))
                return "Công đoạn không hợp lệ.";
            if (!IsAllowed(opt.Sizes, data.size, current?.size))
                return "Size không hợp lệ.";
            if (string.IsNullOrWhiteSpace(data.zplData))
                return "Vui lòng nhập nội dung ZPL.";
            return null;
        }

        static bool IsAllowed(List<LabelTemplateOption> list, string value, string? currentValue)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            if (list.Any(o => string.Equals(o.Value, value, StringComparison.OrdinalIgnoreCase)))
                return true;
            return currentValue != null && string.Equals(currentValue, value, StringComparison.OrdinalIgnoreCase);
        }

        // 2627: vi phạm UNIQUE constraint, 2601: trùng unique index
        static bool IsDuplicateKey(SqlException ex) => ex.Number == 2627 || ex.Number == 2601;

        static string DuplicateMessage(SVN_label_templateUI data) =>
            $"Mẫu tem {data.partNumber} · {data.type} · {data.operation} đã tồn tại.";
    }
}
