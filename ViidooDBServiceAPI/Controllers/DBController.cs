using Microsoft.AspNetCore.Mvc;
using SVNShareLib;
using System.Threading.Tasks;
using ViidooDBServiceAPI.Services;

namespace ViidooDBServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DBController : Controller
    {
        DBService dBService;
        OdooRpcDBService odooRpcDBService;
        public DBController(DBService dBService, OdooRpcDBService odooRpcDBService)
        {
            this.dBService = dBService;
            this.odooRpcDBService = odooRpcDBService;
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
