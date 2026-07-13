using Microsoft.AspNetCore.Mvc;

namespace SVN_Portal.Controllers
{
    public class DashboardController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
