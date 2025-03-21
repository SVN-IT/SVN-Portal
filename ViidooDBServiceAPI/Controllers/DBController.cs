using Microsoft.AspNetCore.Mvc;
using SVNShareLib;
using SVNShareLib.Request;
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
        public BODataProcessResult GetDataFromViindoo(ViindooDataRequest dataRequest)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                switch (dataRequest.TableName) { 
                    case "stock.move.line.consume.rel":
                        processResult = dBService.GetStockMoveLineConsumeRelData();
                        break;
                    case "stock.move.line":
                        processResult = dBService.GetStockMoveLineData();
                        break;
                    case "stock.move":
                        processResult = dBService.GetStockMoveData();
                        //processResult = odooRpcDBService.GetStockMoveData().Result;
                        break;
                    case "mrp.production": //done
                        processResult = dBService.GetProductionResultData();
                        break;
                    case "product.template": //done
                        processResult = dBService.GetProductTemplateData();
                        break;
                    case "mrp.bom": //done
                        processResult = dBService.GetBomData();
                        break;
                    case "mrp.bom.line": //done
                        processResult = dBService.GetBomLineData();
                        break;
                    case "stock.lot": //done
                        processResult = dBService.GetStockLotData();
                        break;
                    case "product.category":
                        processResult = dBService.GetProdCatData();
                        break;
                }
            }
            catch(Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
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
