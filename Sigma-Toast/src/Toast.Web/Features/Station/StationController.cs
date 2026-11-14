using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Toast.Web.Features.Printer;

namespace Toast.Web.Features.Station;

// Màn công nhân: quét mã và in tự động, truy cập tại /toast (không cần đăng nhập admin)
[AllowAnonymous]
[Route("toast")]
public sealed class StationController(PrinterLookup printers, IOptionsMonitor<StationOptions> options) : Controller
{
    // Danh sách máy in cho modal Cấu hình máy in (dùng chung với màn In tem); RequirePin = Toast:RequireLeaderPin
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewData["RequirePin"] = options.CurrentValue.RequireLeaderPin;
        ViewData["ScanSyncEvery"] = Math.Max(0, options.CurrentValue.ScanSyncEvery);
        return View(await printers.ListAsync());
    }
}
