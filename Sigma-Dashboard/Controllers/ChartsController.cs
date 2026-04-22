using Microsoft.AspNetCore.Mvc;

namespace Sigma_Dashboard.Controllers
{
    public class ChartsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Chartjs()
        {
            return View();
        }
    }
}
