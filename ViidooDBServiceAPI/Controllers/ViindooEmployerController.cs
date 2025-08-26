using Microsoft.AspNetCore.Mvc;
using SVNShareLib;
using ViidooDBServiceAPI.Services;

namespace ViidooDBServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ViindooEmployerController : Controller
    {
        ViindooDBConfig dbConfig;
        OdooAPIService odooAPIService;
        public ViindooEmployerController(ViindooDBConfig dbConfig, OdooAPIService odooAPIService)
        {
            this.dbConfig = dbConfig;
            this.odooAPIService = odooAPIService;
        }

        [Route("GetEmployeeCategory")]
        [HttpPost]
        public async Task<BODataProcessResult> GetEmployeeCategory()
        {
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            try
            {
                bODataProcessResult = await odooAPIService.LoginAsync();
                if (bODataProcessResult.OK)
                {
                    var array = await odooAPIService.GetEmployeeCategory(bODataProcessResult.UserID, bODataProcessResult.DataType);
                    bODataProcessResult.OK = true;
                    bODataProcessResult.Message = "Get Employee Category Success";
                    bODataProcessResult.Content = array;
                }


            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

            }
            return bODataProcessResult;
        }

        [Route("GetEmployeeInfomation")]
        [HttpPost]
        public async Task<BODataProcessResult> GetEmployeeInfomation()
        {
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            try
            {
                bODataProcessResult = await odooAPIService.LoginAsync();
                if (bODataProcessResult.OK)
                {
                    var array = await odooAPIService.GetEmployeeInfomation(bODataProcessResult.UserID, bODataProcessResult.DataType);
                    bODataProcessResult.OK = true;
                    bODataProcessResult.Message = "Get Employee Category Success";
                    bODataProcessResult.Content = array;
                }


            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

            }
            return bODataProcessResult;
        }
    }
}
