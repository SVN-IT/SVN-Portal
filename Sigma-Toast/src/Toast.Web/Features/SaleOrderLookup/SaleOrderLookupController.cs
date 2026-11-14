using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Toast.Web.Features.Viindoo;

namespace Toast.Web.Features.SaleOrderLookup;

// Tra cứu đơn bán trên Viindoo (chỉ đọc), /toast/admin/sale-order: các dòng đơn (PO / Lot / deadline theo dòng)
// và các nhóm WO ghép theo số lượng. WO bấm được mở /toastv2 để xem tem.
[Authorize]
[Route("toast/admin/sale-order")]
public sealed class SaleOrderLookupController(ViindooRpc viindoo) : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();

    [HttpGet("api")]
    public async Task<IActionResult> Lookup(string? so)
    {
        try
        {
            var (order, error) = await viindoo.GetSaleOrderOverviewAsync(so);
            return order == null ? Json(new { success = false, error }) : Json(new { success = true, order });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = "Không đọc được Viindoo: " + ex.Message });
        }
    }
}
