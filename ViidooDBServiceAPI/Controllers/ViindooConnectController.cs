using Microsoft.AspNetCore.Mvc;

namespace ViidooDBServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ViindooConnectController : Controller
    {
        [Route("GetDataFromViindoo")]
        [HttpPost]
        public IActionResult Index()
        {
            return View();
        }
    }
}
