using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toast.Web.Features.Viindoo;

namespace Toast.Web.Features.WorkOrderLookup;

// Tra cứu lệnh sản xuất trên Viindoo (chỉ đọc), /toast/admin/work-order
[Authorize]
[Route("toast/admin/work-order")]
public sealed class WorkOrderLookupController(WorkOrderLookupService lookup, ViindooCatalog catalog) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();

    // Dự án (từ nhóm sản phẩm) cho ô lọc
    [HttpGet("api/projects")]
    public Task<IActionResult> Projects() => Safe(async () => new { success = true, projects = await catalog.ProjectsAsync() });

    [HttpGet("api")]
    public Task<IActionResult> Search(string? q, string? project, string? kind, string? state, int offset = 0, string? sort = null, string? dir = null) =>
        Safe(async () => new { success = true, data = await lookup.SearchAsync(q, project, kind, state, offset, sort, dir) });

    [HttpGet("api/detail")]
    public Task<IActionResult> Detail(string? wo) => Safe(async () =>
    {
        var (detail, error) = await lookup.DetailAsync(wo);
        return detail == null ? new { success = false, error } : (object)new { success = true, detail };
    });

    async Task<IActionResult> Safe(Func<Task<object>> action)
    {
        try
        {
            return Json(await action());
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = "Không đọc được Viindoo: " + ex.Message });
        }
    }
}
