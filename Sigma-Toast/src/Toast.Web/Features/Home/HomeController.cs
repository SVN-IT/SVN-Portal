using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Toast.Web.Features.Home;

// Trang chủ khu quản trị: /toast/admin
[Authorize]
[Route("toast/admin")]
public sealed class HomeController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
