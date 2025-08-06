using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using SVNShareLib;
using SVNShareLib.Request;
using ViidooDBServiceAPI.Services;

namespace ViidooDBServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ViindooConnectController : Controller
    {
        ViindooDBConfig dbConfig;
        OdooAPIService odooAPIService;

        public ViindooConnectController(ViindooDBConfig dbConfig, OdooAPIService odooAPIService)
        {
            this.dbConfig = dbConfig;
            this.odooAPIService = odooAPIService;
        }
        [Route("ProductionRead")]
        [HttpPost]
        public async Task<BODataProcessResult> ProductionRead(int id)
        {
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            try
            {
                JsonRpcRequest request = new JsonRpcRequest();
                request.Id = 230;
                request.Jsonrpc = "2.0";
                request.Method = "call";
                request.Params = new ParamsData
                {
                    Args = new List<object> { new List<int> { id }, new List<string> { "confirm_cancel", "show_lock", "move_byproduct_ids", "state",
                    "show_serial_mass_produce", "check_ids", "check_todo", "reservation_state", "date_planned_finished", "is_locked", "qty_produced",
                    "unreserve_visible", "reserve_visible", "consumption", "is_planned", "show_allocation", "workorder_ids", "eco_count", "purchase_order_count",
                    "sale_order_count", "mrp_production_child_count", "mrp_production_source_count", "mrp_production_backorder_count", "unbuild_count", "scrap_count",
                    "delivery_count", "alert_count", "package_count", "account_moves_count", "maintenance_count", "document_count", "overview_progress", "priority",
                    "name", "id", "use_create_components_lots", "show_lot_ids", "product_tracking", "show_valuation", "product_id", "product_tmpl_id",
                    "forecasted_issue", "company_id", "product_description_variants", "bom_id", "qty_producing", "product_qty", "product_uom_category_id",
                    "product_uom_id", "product_packaging_id", "lot_producing_id", "date_planned_start", "delay_alert_date", "json_popover",
                    "components_availability_state", "components_availability", "show_final_lots", "production_location_id", "move_finished_ids",
                    "move_raw_ids", "picking_type_id", "location_src_id", "warehouse_id", "location_dest_id", "origin", "date_deadline", "display_name" } },
                    Model = "mrp.production",
                    Method = "read",
                    Kwargs = new Kwargs
                    {
                        Context = new Context
                        {
                            Lang = "vi_VN",
                            Tz = "Asia/Ho_Chi_Minh",
                            Uid = 2,
                            Allowed_Company_Ids = new List<int> { 1 },
                            Bin_Size = true,
                            Params = new ParamsContext
                            {
                                //Id = 39637,
                                Cids = 1,
                                Menu_Id = 248,
                                Action = 431,
                                Model = "mrp.production",
                                View_Type = "list"
                            },
                            Default_Company_Id = 1
                        }
                    }
                };

                HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(dbConfig.ServerUrl, 1000);
                var result = await httpClientHelper.PostRequest("web/dataset/call_kw/mrp.production/read", request, new CancellationToken(false));
                return result;
            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;
                
            }
            return bODataProcessResult;
        }

        [Route("ProductionReadv1")]
        [HttpPost]
        public async Task<BODataProcessResult> ProductionReadv1(int id)
        {
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            try
            {
                bODataProcessResult = await odooAPIService.LoginAsync();
                if (bODataProcessResult.OK)
                {
                    var array = await odooAPIService.ReadProductionAsync(id, bODataProcessResult.UserID, bODataProcessResult.DataType);
                }


            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

            }
            return bODataProcessResult;
        }

        [Route("InputProductionByProductID")]
        [HttpPost]
        public async Task<BODataProcessResult> InputProductionByProductID(string name, int qty_producing)
        {
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            try
            {
                bODataProcessResult = await odooAPIService.LoginAsync();
                if (bODataProcessResult.OK)
                {
                    //Lấy dữ liệu lệnh sản xuất
                    var productionOrderInfo = await odooAPIService.ReadProductionByProductIDAsync(name, bODataProcessResult.UserID, bODataProcessResult.DataType);

                    // Thực hiên tiêu hao nghuyên vật liệu theo BOM
                    var moveRawConsumeInfo = await odooAPIService.ConsumeMaterialsByBOMAsync(productionOrderInfo, bODataProcessResult.UserID, bODataProcessResult.DataType, qty_producing);

                    var result = ((JObject)moveRawConsumeInfo["result"])["value"]["move_raw_ids"] as JArray;

                    var moveRawList = new List<object>();

                    if (result != null) 
                    {
                        foreach (var item in result) 
                        {
                            var id = (int)item[1];
                            if(id != 0)
                            {
                                var detail = item[2] as JObject;
                                var date = detail?["date"]?.ToString();
                                var date_deadline = detail?["date_deadline"]?.ToString();

                                decimal quantityDone = 0;
                                try
                                {
                                    quantityDone = decimal.Parse(detail?["quantity_done"]?.ToString());
                                }
                                catch
                                {
                                    quantityDone = 0;
                                }
                                
                                if (quantityDone != 0)
                                {
                                    var moveRaw = new object[]
                                    {
                                        1,
                                        id,
                                        new {
                                            date = date,
                                            date_deadline = date_deadline,
                                            quantity_done = quantityDone
                                        }
                                    };
                                    moveRawList.Add(moveRaw);
                                }
                                else 
                                {
                                    var moveRaw = new object[]
                                    {
                                        4,
                                        id,
                                        false
                                    };
                                    moveRawList.Add(moveRaw);
                                }
                                
                            }
                            
                        }

                        // Danh sách thành phần tiêu hao
                        object[] move_raw_ids = moveRawList.ToArray();

                        int mrp_production_id = int.Parse(productionOrderInfo["id"]);

                        var saveResult = await odooAPIService.SaveProductionOrderAsync(mrp_production_id, qty_producing, move_raw_ids, bODataProcessResult.UserID, bODataProcessResult.DataType);

                        var markDoneResult = await odooAPIService.MarkDoneProductionOrderAsync(mrp_production_id, bODataProcessResult.UserID, bODataProcessResult.DataType);

                        var backOrderOnchangeResult = await odooAPIService.BackOrderOnchange(mrp_production_id, bODataProcessResult.UserID, bODataProcessResult.DataType);

                        var backorder_id = await odooAPIService.BackOrderCreate(mrp_production_id, bODataProcessResult.UserID, bODataProcessResult.DataType);

                        var backorderResult = await odooAPIService.BackOrderAction(mrp_production_id, backorder_id, bODataProcessResult.UserID, bODataProcessResult.DataType);

                        bODataProcessResult.OK = true;
                        bODataProcessResult.Message = "Hoàn thành lệnh sản xuất";
                    }
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
