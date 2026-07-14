using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
using SVNShareLib.Request;
using SVNShareLib;

namespace ViidooDBServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductionController : Controller
    {
        APIConfig aPIConfiguration;
        SVNDBConfig dBConfiguration;
        public ProductionController(APIConfig aPIConfiguration, SVNDBConfig dBConfiguration)
        {
            this.aPIConfiguration = aPIConfiguration;
            this.dBConfiguration = dBConfiguration;
        }

        [HttpPost("InputProductionResultLog")]
        public async Task<BODataProcessResult> InputProductionResultLog(ProductionDataV1 data)
        {
            bool isInputToViindoo = false;
            BODataProcessResult processResult = new BODataProcessResult();
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
            SVN_ProductionInputLogDataPortal dataPortal = new SVN_ProductionInputLogDataPortal(dBConfiguration.ConnectionString);
            try
            {
                List<LotScanedRequest> lotScaneds = new List<LotScanedRequest>();
                var dataSearial = data.Products.Where(x => x.Has_tracking == "serial").Select(y =>
                {
                    LotScanedRequest lotScaned = new LotScanedRequest
                    {
                        product_id = y.Product_id,
                        lotNumber = y.Serial_code,
                        tracking = "serial"
                    };
                    lotScaneds.Add(lotScaned);
                    return y;
                }).ToList();
                var dataLot = data.Products.Where(x => x.Has_tracking == "lot").Select(y =>
                {
                    LotScanedRequest lotScaned = new LotScanedRequest
                    {
                        product_id = y.Product_id,
                        lotNumber = y.Serial_code,
                        tracking = "lot"
                    };
                    lotScaneds.Add(lotScaned);
                    return y;
                }).ToList();
                InputProductDataRequest dataRequest = new InputProductDataRequest()
                {
                    WorkOrderNumber = data.Name,
                    LotNumber = data.Serial,
                    Quality = int.Parse(data.Quantity),
                    LotScaneds = lotScaneds
                };

                string[] parts = data.SubName.Split('-');

                if (parts.Length == 1)
                {
                    string prefix = parts[0]; // "NM/MO/02638"

                    data.SubName = $"{prefix}-001";
                }

                //Xử lý nhập KQSX vào SVNDB
                SVN_ProductionInputLogUI productDataUI = new SVN_ProductionInputLogUI();
                productDataUI.wo_code = data.SubName;
                productDataUI.serial_code = data.Serial;
                productDataUI.master_wo_code = data.Name;
                productDataUI.product_id = int.Parse(data.ProductID);
                productDataUI.product_qty = decimal.Parse(data.Quantity);
                productDataUI.product_type = data.ProductTracking;
                productDataUI.date_finished = DateTime.Now;
                productDataUI.state = "Used";
                productDataUI.component_list = JsonConvert.SerializeObject(lotScaneds);
                productDataUI.API_function = $"{aPIConfiguration.BaseURL}{aPIConfiguration.InputProductionByWorkOrderv1URL}";
                productDataUI.API_parameters = JsonConvert.SerializeObject(dataRequest);
                productDataUI.status = "Not synchronized";
                productDataUI.total_qty = decimal.Parse(data.TotalQuantity);
                //productDataUI.remain_qty = decimal.Parse(data.TotalQuantity) - decimal.Parse(data.Quantity);
                var insertResult = await dataPortal.InsertAsync(productDataUI);

                // Thực hiện cập nhật tiêu hao thành phần
                if (lotScaneds != null && lotScaneds.Count > 0)
                {
                    foreach (var item in lotScaneds)
                    {
                        if (item.tracking == "serial")
                        {
                            var existComponentLog = await dataPortal.GetByProductIDAndSerialCodeAsync(item.product_id, item.lotNumber);
                            if (existComponentLog != null)
                            {
                                existComponentLog.state = "Consumed";
                                existComponentLog.consumed_wo_code = data.SubName;
                                var updateResult = await dataPortal.UpdateAsync(existComponentLog);
                                if (updateResult)
                                {
                                    processResult.OK = true;
                                }
                                else
                                {
                                    processResult.OK = false;
                                    processResult.Message = processResult.Message + Environment.NewLine + $"Failed to update component log with serial {item.lotNumber} as consumed.";
                                }
                            }
                            else
                            {
                                //trong trường hợp là nvl nhập kho thì ko cần check nữa vì bên trên đã check rồi
                                processResult.OK = true;
                            }
                        }
                        else if (item.tracking == "lot")
                        {

                        }
                    }
                    if (processResult.OK)
                    {
                        processResult.Message = "Input production result successfully";
                    }
                }
                else
                {
                    if (insertResult > 0)
                    {
                        processResult.OK = true;
                        processResult.Message = "Input production result successfully";
                    }
                    else
                    {
                        processResult.OK = false;
                        processResult.Message = "Failed to input production result into local database.";
                    }
                }

                //TempData.Remove("MasterWorkOrderName");
                //TempData["MasterWorkOrderName"] = data.Name;
                //TempData.Keep("MasterWorkOrderName");


                if (isInputToViindoo)
                {
                    var result = await httpClientHelper.PostRequest(aPIConfiguration.InputProductionByWorkOrderv1URL, dataRequest, new CancellationToken(false));
                    if (result != null)
                    {
                        //TempData.Remove("WorkOrderName");
                        //TempData["WorkOrderName"] = data.SubName;
                        //TempData.Keep("WorkOrderName");
                        if (result.OK)
                        {
                            string operation = "";

                            processResult.OK = true;
                            processResult.Message = $"{operation} - {data.Name.Split("-")[0].Replace("/", "%2f")} - {processResult.Message}";
                            return processResult;
                        }
                        else
                        {
                            processResult.OK = false;
                            if (!string.IsNullOrWhiteSpace(result.Message))
                            {
                                processResult.Message = result.Message;
                            }
                            else
                            {
                                processResult.Message = "Không có dữ liệu";
                            }

                        }

                    }
                }
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }
    }
}
