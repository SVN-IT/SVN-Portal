using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toast.Web.Features.Viindoo;

namespace Toast.Web.Features.ProductLookup;

// Tra cứu sản phẩm trên Viindoo (chỉ đọc), /toast/admin/viindoo-product
[Authorize]
[Route("toast/admin/viindoo-product")]
public sealed class ProductLookupController(ProductLookupService lookup, ViindooCatalog catalog) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpGet("api/projects")]
    public Task<IActionResult> Projects() => Safe(async () => new { success = true, projects = await catalog.ProjectsAsync() });

    [HttpGet("api")]
    public Task<IActionResult> Search(string? q, string? project, string? kind, int offset = 0, string? sort = null, string? dir = null) =>
        Safe(async () => new { success = true, data = await lookup.SearchAsync(q, project, kind, offset, sort, dir) });

    [HttpGet("api/{id:int}")]
    public Task<IActionResult> Detail(int id) => Safe(async () =>
    {
        var (detail, error) = await lookup.DetailAsync(id);
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
