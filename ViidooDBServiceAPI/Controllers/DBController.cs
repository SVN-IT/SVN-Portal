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
        public List<BODataProcessResult> GetDataFromViindoo()
        {
            List<BODataProcessResult> processResults = new List<BODataProcessResult>();
            try
            {
                processResults = dBService.GetData();
            }
            catch(Exception ex)
            {
                BODataProcessResult processResult = new BODataProcessResult();
                processResult.Message = ex.Message;
                processResults.Add(processResult);
            }
            return processResults;
        }
    }
}
