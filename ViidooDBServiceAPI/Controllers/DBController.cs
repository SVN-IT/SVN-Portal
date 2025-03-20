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

        [Route("GetAndUploadProductionResultData")]
        [HttpPost]
        public BODataProcessResult GetAndUploadProductionResultData()
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                processResult = odooRpcDBService.GetProductionResultData().Result;
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        [Route("GetAndUploadProductionResultDataXMLRPC")]
        [HttpPost]
        public BODataProcessResult GetAndUploadProductionResultDataXMLRPC()
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                processResult = dBService.GetProductionResultData();
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        [Route("GetAndUploadProductionTemplateData")]
        [HttpPost]
        public BODataProcessResult GetAndUploadProductionTemplateData()
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                processResult = dBService.GetProductTemplateData();
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }
    }
}
