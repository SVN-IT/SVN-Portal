using Microsoft.AspNetCore.Mvc;
using Sigma_Tools.Models;
using Sigma_Tools.Services.Authorizations;
using System.Diagnostics;

namespace Sigma_Tools.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IPermissionService _permissionService;

    public HomeController(ILogger<HomeController> logger, IPermissionService permissionService)
    {
        _logger = logger;
        _permissionService = permissionService;
    }

    public IActionResult Index()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return RedirectToAction("Login", "Account");
        }

        if (!_permissionService.HasRight("home", "read"))
        {
            return RedirectToAction("AccessDenied", "Account");
        }

        string accessMode = "Tools";
        string controllerName = "Home";
        string indexPage = "Index";
        string titleName = "Home Page";

        ViewBag.AccessMode = accessMode;
        ViewBag.ControllerName = controllerName;
        ViewBag.IndexPage = indexPage;
        ViewBag.TitleName = titleName;

        return RedirectToAction("POInbound", "Oracle");
        //return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
