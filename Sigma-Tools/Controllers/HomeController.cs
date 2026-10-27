using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Sigma_Tools.Models;

namespace Sigma_Tools.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        string accessMode = "Tools";
        string controllerName = "Home";
        string indexPage = "Index";
        string titleName = "Home Page";

        ViewBag.AccessMode = accessMode;
        ViewBag.ControllerName = controllerName;
        ViewBag.IndexPage = indexPage;
        ViewBag.TitleName = titleName;

        return View();
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
