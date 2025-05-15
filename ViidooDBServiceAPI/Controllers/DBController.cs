using Microsoft.AspNetCore.Mvc;
using SVNShareLib;
using SVNShareLib.Request;
using System.Collections.Generic;
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
        ViindooDBConfig dBConfig;
        ViindooDataService viinDataService;
        JsonRpcDataService jsonRpcDataService;
        public DBController(DBService dBService, 
            OdooRpcDBService odooRpcDBService,
            ViindooDataService viinDataService,
            ViindooDBConfig dBConfig,
            JsonRpcDataService jsonRpcDataService)
        {
            this.dBService = dBService;
            this.odooRpcDBService = odooRpcDBService;
            this.dBConfig = dBConfig;
            this.viinDataService = viinDataService;
            this.jsonRpcDataService = jsonRpcDataService;
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

        [Route("GetProductionResult")]
        [HttpPost]
        public BODataProcessResult GetProductionResult()
        {
            BODataProcessResult totalDataProcessResult = new BODataProcessResult();
            try
            {
                string objectName = "mrp.production";
                var queryConfig = dBConfig.QueryConfig.FirstOrDefault(x => x.TableName == objectName);

                BODataProcessResult productionResult = new BODataProcessResult();
                productionResult = viinDataService.GetViindooDataV1(queryConfig);

                BODataProcessResult callProcessResult = new BODataProcessResult();
                callProcessResult = viinDataService.CallSPToUpdateResult();
                if(productionResult.OK && callProcessResult.OK)
                {
                    totalDataProcessResult.OK = true;
                }
                else
                {
                    totalDataProcessResult.OK = false;
                }

                totalDataProcessResult.Message = "Get production result: " + productionResult.Message + " / " + "Call update: " + callProcessResult.Message;
            }
            catch (Exception ex)
            {
                totalDataProcessResult.Message = totalDataProcessResult.Message + " / " + ex.Message;
            }
            return totalDataProcessResult;
        }

        [Route("GetDataFromViindooV1")]
        [HttpPost]
        public BODataProcessResult GetDataFromViindooV1()
        {
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            List<BODataProcessResult> processResults = new List<BODataProcessResult>();
            try
            {
                List<string> objectNames = dBConfig.ObjectList.Split(',').ToList();
                foreach (string objectName in objectNames) 
                {
                    var queryConfig = dBConfig.QueryConfig.FirstOrDefault(x => x.TableName == objectName);
                    BODataProcessResult processResult = new BODataProcessResult();
                    processResult = viinDataService.GetViindooDataV1(queryConfig);
                    processResults.Add(processResult);
                }

                //BODataProcessResult callProcessResult = new BODataProcessResult();
                //callProcessResult = viinDataService.CallSPToUpdateResult();
                //processResults.Add(callProcessResult);

            }
            catch (Exception ex)
            {
                BODataProcessResult processResult = new BODataProcessResult();
                processResult.Message = ex.Message;
                processResults.Add(processResult);
            }
            bODataProcessResult.Content = processResults;
            return bODataProcessResult;
        }

        [Route("GetDataFromViindooV2")]
        [HttpPost]
        public async Task<BODataProcessResult> GetDataFromViindooV2()
        {
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            List<BODataProcessResult> processResults = new List<BODataProcessResult>();
            try
            {
                List<string> objectNames = dBConfig.ObjectList.Split(',').ToList();
                foreach (string objectName in objectNames)
                {
                    BODataProcessResult processResult = new BODataProcessResult();
                    processResult = await jsonRpcDataService.GetDataByJsonRpc(objectName);
                    processResults.Add(processResult);
                }

                BODataProcessResult callProcessResult = new BODataProcessResult();
                callProcessResult = jsonRpcDataService.CallSPToUpdateResult();
                processResults.Add(callProcessResult);

            }
            catch (Exception ex)
            {
                BODataProcessResult processResult = new BODataProcessResult();
                processResult.Message = ex.Message;
                processResults.Add(processResult);
            }
            bODataProcessResult.Content = processResults;
            return bODataProcessResult;
        }

        [Route("GetDataFromViindooV3")]
        [HttpPost]
        public object GetDataFromViindooV3(ViindooDataRequest dataRequest)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                processResult = viinDataService.GetViindooDataV2(dataRequest);

            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult.Content;
        }

        [Route("UpdateProductionQty")]
        [HttpPost]
        public BODataProcessResult UpdateProductionQty(ViindooDataRequest dataRequest)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                var productionResult = viinDataService.GetViindooDataV2(dataRequest);
                if(productionResult != null && productionResult.OK)
                {
                    for (int i = 0; i < dataRequest.listDomain.Count; i++)
                    {
                        if (dataRequest.listDomain[i].Contains("name"))
                        {
                            dataRequest.listDomain[i] = dataRequest.listDomain[i].Replace("name", "reference");
                        }
                    }

                    dataRequest.TableName = "stock.move";
                    dataRequest.Fields = "";
                    dataRequest.Limit = 0;
                    dataRequest.Order = "";
                    var stockMoveResult = viinDataService.GetViindooDataV2(dataRequest);
                    if(stockMoveResult != null && processResult.OK)
                    {
                    }
                }
            }
            catch (Exception ex) 
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

        [Route("GetPackageBySeri")]
        [HttpPost]
        public BODataProcessResult GetPackageBySeri(string seriNumber)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                processResult = viinDataService.GetPackageBySeri(seriNumber);
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
