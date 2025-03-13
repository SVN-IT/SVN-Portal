using Microsoft.AspNetCore.Mvc;
using SVNShareLib;
using ViidooDBServiceAPI.Services;

namespace ViidooDBServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DBController : Controller
    {
        DBService dBService;
        public DBController(DBService dBService)
        {
            this.dBService = dBService;
        }

        [Route("GetDataFromViindoo")]
        [HttpPost]
        public BODataProcessResult GetDataFromViindoo()
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                processResult = dBService.GetProductionResult();
            }
            catch(Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }
    }
}
