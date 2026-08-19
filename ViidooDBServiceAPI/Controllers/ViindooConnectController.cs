using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SVNShareLib;
using SVNShareLib.DAL;
using SVNShareLib.DAL.NewDashboard;
using SVNShareLib.DTO;
using SVNShareLib.Request;
using SVNShareLib.Utils;
using System;
using System.Security.Cryptography;
using ViidooDBServiceAPI.Services;

namespace ViidooDBServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ViindooConnectController : Controller
    {
        ViindooDBConfig dbConfig;
        OdooAPIService odooAPIService;
        SVNDBConfig svnDBConfig;
        ConvertDataService convertDataService;

        public ViindooConnectController(ViindooDBConfig dbConfig, OdooAPIService odooAPIService, SVNDBConfig svnDBConfig, ConvertDataService convertDataService)
        {
            this.dbConfig = dbConfig;
            this.odooAPIService = odooAPIService;
            this.svnDBConfig = svnDBConfig;
            this.convertDataService = convertDataService;
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
                if(string.IsNullOrWhiteSpace(dbConfig.SessionID) || dbConfig.UserID == 0)
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }

                var array = await odooAPIService.ReadProductionAsync(id, dbConfig.UserID, dbConfig.SessionID);


            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

                if(!string.IsNullOrWhiteSpace(bODataProcessResult.Message) && bODataProcessResult.Message.Contains("Odoo Session Expired"))
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }

            }
            return bODataProcessResult;
        }

        [Route("GetProductionResult")]
        [HttpPost]
        public async Task<BODataProcessResult> GetProductionResult()
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                if (string.IsNullOrWhiteSpace(dbConfig.SessionID) || dbConfig.UserID == 0)
                {
                    processResult = await odooAPIService.LoginAsync();
                    if (!processResult.OK)
                    {
                        return processResult;
                    }
                    dbConfig.SessionID = processResult.DataType;
                    dbConfig.UserID = processResult.UserID;
                }

                DateTime today = DateTime.Today;
                DateTime fromDate = today.AddHours(-7);
                DateTime toDate = today.AddHours(16).AddMinutes(59);

                List<int> excludeIds = new List<int>();

                //Lấy ra các lệnh sản xuất đã dc đồng bộ rồi
                mrp_productionDataPortal dataPortal = new mrp_productionDataPortal(svnDBConfig.ConnectionString);
                var existData = dataPortal.GetDataProductFromTimeToTime(fromDate, toDate);
                if (existData != null && existData.Count > 0)
                {
                    excludeIds = existData.Select(x => x.id).ToList();
                }

                int currentDataCount = existData != null ? existData.Count : 0;

                var productionOrders = await odooAPIService.ReadProductionResultAsync(fromDate, toDate, excludeIds, dbConfig.UserID, dbConfig.SessionID);
                if(productionOrders == null || productionOrders.Count == 0)
                {
                    processResult.OK = false;
                    processResult.Message = $"{DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss")} | Không có lệnh sản xuất nào hoàn thành trong khoảng thời gian này";
                    return processResult;
                }

                try
                {
                    var newmrpProduction = ConvertDynamicToUI(productionOrders);
                    if (newmrpProduction == null || productionOrders.Count == 0) 
                    {
                        processResult.OK = false;
                        processResult.Message = $"{DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss")} | Không có lệnh sản xuất nào hoàn thành trong khoảng thời gian này";
                        return processResult;
                    }
                    var insertResult = dataPortal.InsertBulk(newmrpProduction);
                    if (insertResult <= 0)
                    {
                        processResult.OK = false;
                        processResult.Message = $"{DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss")} | Data trong SVN_mrp_production: {currentDataCount}  | Data từ Viindoo chưa có trong SVN_mrp_production: {productionOrders.Count} | Insert thất bại: {productionOrders.Count}";
                        return processResult;
                    }

                    processResult.OK = true;
                    processResult.Message = $"{DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss")} | Data trong SVN_mrp_production: {currentDataCount}  | Data từ Viindoo chưa có trong SVN_mrp_production: {productionOrders.Count} | Insert thành công: {productionOrders.Count}";
                    return processResult;
                }
                catch(Exception ex)
                {
                    processResult.OK = false;
                    processResult.Message = $"{DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss")} | ConvertDynamicToUI - Lỗi khi chuyển đổi dữ liệu lệnh sản xuất: {ex.Message}";
                    return processResult;
                }
            }
            catch(Exception ex)
            {
                processResult.OK = false;
                processResult.Message = $"{DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss")} | Lỗi không xác định: {ex.Message}";

                if (!string.IsNullOrWhiteSpace(processResult.Message) && processResult.Message.Contains("Odoo Session Expired"))
                {
                    processResult = await odooAPIService.LoginAsync();
                    if (!processResult.OK)
                    {
                        return processResult;
                    }
                    dbConfig.SessionID = processResult.DataType;
                    dbConfig.UserID = processResult.UserID;
                }
            }
            return processResult;
        }

        [Route("GetWorkOrder")]
        [HttpPost]
        public async Task<BODataProcessResult> GetWorkOrder(InputProductDataRequest dataRequest)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                if (string.IsNullOrWhiteSpace(dbConfig.SessionID) || dbConfig.UserID == 0)
                {
                    processResult = await odooAPIService.LoginAsync();
                    if (!processResult.OK)
                    {
                        return processResult;
                    }
                    dbConfig.SessionID = processResult.DataType;
                    dbConfig.UserID = processResult.UserID;
                }


                var productionOrderInfo = await odooAPIService.ReadProductionByProductIDAsync(dataRequest.WorkOrderNumber, dbConfig.UserID, dbConfig.SessionID);
                if (productionOrderInfo == null)
                {
                    processResult.OK = false;
                    processResult.Message = "Không tìm thấy lệnh sản xuất: " + dataRequest.WorkOrderNumber;
                    return processResult;
                }

                if (int.Parse(productionOrderInfo["qty_producing"]) != 0)
                {
                    processResult.OK = false;
                    processResult.Message = "Lệnh: " + productionOrderInfo["name"] + " đã hoàn thành";
                    return processResult;
                }

                //Lấy move_id 
                var str_move_ids = productionOrderInfo["move_raw_ids"].Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Split(",");
                var move_ids = Array.ConvertAll(str_move_ids, int.Parse);
                //Lấy company_id
                var arrCompanyID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["company_id"]);
                var company_id = Convert.ToInt32(arrCompanyID[0]);
                var stockMoveInfo = await odooAPIService.GetStockMoveByIDAsync(move_ids, company_id, dbConfig.UserID, dbConfig.SessionID);
                if (stockMoveInfo == null || stockMoveInfo.Count == 0)
                {
                    processResult.OK = false;
                    processResult.Message = "Không tìm thấy danh sách thành phần của lệnh sản xuất: " + dataRequest.WorkOrderNumber;
                    return processResult;
                }

                Dictionary<string, string> workOrder = new Dictionary<string, string>();
                workOrder["name"] = productionOrderInfo["name"];
                workOrder["product_name"] = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["product_id"].ToString())[1].ToString();
                workOrder["product_qty"] = productionOrderInfo["product_qty"];
                workOrder["product_tracking"] = productionOrderInfo["product_tracking"];
                workOrder["product_id"] = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["product_id"].ToString())[0].ToString();
                workOrder["origin"] = productionOrderInfo["origin"];


                List<Dictionary<string, string>> stockMoveInfoList = new List<Dictionary<string, string>>();
                foreach (var item in stockMoveInfo)
                {
                    Dictionary<string, string> stockMove = new Dictionary<string, string>();

                    //Lấy product_id
                    var arrMarterialProductID = JsonConvert.DeserializeObject<object[]>(item["product_id"].ToString());
                    var product_name = arrMarterialProductID[1].ToString();

                    var arrLocationID = JsonConvert.DeserializeObject<object[]>(item["location_id"].ToString());
                    var location_name = arrLocationID[1].ToString();

                    var has_tracking = item["has_tracking"].ToString();
                    stockMove["product_id"] = arrMarterialProductID[0].ToString();
                    stockMove["product_name"] = product_name;
                    stockMove["location_name"] = location_name;
                    stockMove["has_tracking"] = has_tracking;
                    stockMoveInfoList.Add(stockMove);
                }

                WorkOrderInfo workOrderInfo = new WorkOrderInfo
                {
                    OrderInfo = workOrder,
                    StockMoveInfo = stockMoveInfoList
                };
                processResult.OK = true;
                processResult.Message = "Lấy thông tin lệnh sản xuất thành công: " + dataRequest.WorkOrderNumber;
                processResult.Content = workOrderInfo;
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;

                if (!string.IsNullOrWhiteSpace(processResult.Message) && processResult.Message.Contains("Odoo Session Expired"))
                {
                    processResult = await odooAPIService.LoginAsync();
                    if (!processResult.OK)
                    {
                        return processResult;
                    }
                    dbConfig.SessionID = processResult.DataType;
                    dbConfig.UserID = processResult.UserID;
                }
            }
            return processResult;
        }


        /// <summary>
        /// Nhập kết quả sản xuất đơn giản
        /// Không có truy vết theo mã serial
        /// </summary>
        /// <param name="dataRequest"></param>
        /// <returns></returns>
        [Route("InputProductionByWorkOrder")]
        [HttpPost]
        public async Task<BODataProcessResult> InputProductionByWorkOrder(ProductDataRequest dataRequest)
        {
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            try
            {
                if (string.IsNullOrWhiteSpace(dbConfig.SessionID) || dbConfig.UserID == 0)
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }

                //Lấy dữ liệu lệnh sản xuất
                var productionOrderInfo = await odooAPIService.ReadProductionByProductIDAsync(dataRequest.seriNumber, dbConfig.UserID, dbConfig.SessionID);

                // Thực hiên tiêu hao nghuyên vật liệu theo BOM
                var moveRawConsumeInfo = await odooAPIService.ConsumeMaterialsByBOMAsync(productionOrderInfo, dbConfig.UserID, dbConfig.SessionID, dataRequest.count);

                var result = ((JObject)moveRawConsumeInfo["result"])["value"]["move_raw_ids"] as JArray;

                var moveRawList = new List<object>();

                if (result != null)
                {
                    foreach (var item in result)
                    {
                        var id = (int)item[1];
                        if (id != 0)
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

                    var saveResult = await odooAPIService.SaveProductionOrderAsync(mrp_production_id, dataRequest.count, move_raw_ids, dbConfig.UserID, dbConfig.SessionID);

                    var markDoneResult = await odooAPIService.MarkDoneProductionOrderAsync(mrp_production_id, dbConfig.UserID, dbConfig.SessionID);

                    var backOrderOnchangeResult = await odooAPIService.BackOrderOnchange(mrp_production_id, dbConfig.UserID, dbConfig.SessionID);

                    var backorder_id = await odooAPIService.BackOrderCreate(mrp_production_id, dbConfig.UserID, dbConfig.SessionID);

                    var backorderResult = await odooAPIService.BackOrderAction(mrp_production_id, backorder_id, dbConfig.UserID, dbConfig.SessionID);

                    bODataProcessResult.OK = true;
                    bODataProcessResult.Message = "Hoàn thành lệnh sản xuất";
                }


            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

                if (!string.IsNullOrWhiteSpace(bODataProcessResult.Message) && bODataProcessResult.Message.Contains("Odoo Session Expired"))
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }

            }
            return bODataProcessResult;
        }

        [Route("InputProductionByWorkOrderv1")]
        [HttpPost]
        public async Task<BODataProcessResult> InputProductionByWorkOrderv1(InputProductDataRequest dataRequest)
        {
            LogService logger = new LogService(svnDBConfig.ConnectionString);
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            try
            {
                if (string.IsNullOrWhiteSpace(dbConfig.SessionID) || dbConfig.UserID == 0)
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }

                //Lấy dữ liệu lệnh sản xuất
                var productionOrderInfo = await odooAPIService.ReadProductionByProductIDAsync(dataRequest.WorkOrderNumber, dbConfig.UserID, dbConfig.SessionID);
                if (productionOrderInfo == null)
                {
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Không tìm thấy lệnh sản xuất cho mã seri: " + dataRequest.WorkOrderNumber);
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Không tìm thấy lệnh sản xuất cho mã seri: " + dataRequest.WorkOrderNumber;
                    return bODataProcessResult;
                }
                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Bắt đầu thực hiện lệnh sản xuất: " + productionOrderInfo["name"]);

                var str_move_ids = productionOrderInfo["move_raw_ids"].Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Split(",");
                var move_ids = Array.ConvertAll(str_move_ids, int.Parse);

                var productTracking = productionOrderInfo["product_tracking"]?.ToString();

                //Lấy product_id
                var arrProductID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["product_id"]);
                var product_id = Convert.ToInt32(arrProductID[0]);

                //Lấy company_id
                var arrCompanyID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["company_id"]);
                var company_id = Convert.ToInt32(arrCompanyID[0]);

                //Get stock move
                var stockMoveInfo = await odooAPIService.GetStockMoveByIDAsync(move_ids, company_id, dbConfig.UserID, dbConfig.SessionID);

                //Lấy các thành phần được theo dõi theo mã lot hoặc serial
                List<Dictionary<string, object>> stockMoveSerialInfo = new List<Dictionary<string, object>>();
                if (stockMoveInfo != null)
                {
                    foreach (var item in stockMoveInfo)
                    {
                        var token = (JToken)item["has_tracking"];
                        if (token.Type == JTokenType.String && token?.ToString() == "serial")
                        {
                            stockMoveSerialInfo.Add(item);
                        }
                    }
                }

                if (stockMoveSerialInfo.Count > 0)
                {
                    //Dữ liệu dùng để thay đổi stock move line có serial theo lệnh sản xuất
                    List<Dictionary<string, string>> stockMoveLineSerials = new List<Dictionary<string, string>>();

                    foreach (var item in stockMoveSerialInfo)
                    {
                        var str_move_line_ids = item["move_line_ids"].ToString().Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Split(",").ToList();
                        //var move_line_ids = Array.ConvertAll(str_move_line_ids, int.Parse);

                        //Lấy product_id
                        var arrMarterialProductID = JsonConvert.DeserializeObject<object[]>(item["product_id"].ToString());
                        var product_material_id = Convert.ToInt32(arrMarterialProductID[0]);

                        var move_id = int.Parse(item["id"].ToString());

                        if (dataRequest.LotScaneds.Count > 0)
                        {
                            var lotScaned = dataRequest.LotScaneds.FirstOrDefault(x => x.product_id == product_material_id);
                            int lot_id_info = 0;
                            if (lotScaned != null)
                            {
                                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Xử lý thành phần có mã serial: " + lotScaned.lotNumber + " và mã nguyên liệu là: " + arrMarterialProductID[1] + " cho lsx: " + productionOrderInfo["name"]);
                                //Bước cần thay đổi theo cách mới để lấy stock move line theo mã lot
                                //B1: Lấy ra stock move line đầu tiên trong danh sách thuộc stock move - checked
                                Dictionary<string, string> firstStockMoveLine = new Dictionary<string, string>();
                                if (str_move_line_ids != null && str_move_line_ids.Count > 0)
                                {
                                    firstStockMoveLine = await odooAPIService.GetStockMoveLineByID(Convert.ToInt32(str_move_line_ids[0]), Convert.ToInt32(productionOrderInfo["id"]), item, dbConfig.UserID, dbConfig.SessionID);

                                    //B2: Tìm kiếm lot id theo lot name - checked
                                    lot_id_info = await odooAPIService.GetLotInfo(lotScaned.lotNumber, Convert.ToInt32(productionOrderInfo["id"]), item, dbConfig.UserID, dbConfig.SessionID);
                                    if (lot_id_info == 0)
                                    {
                                        logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Mã lot " + lotScaned.lotNumber + " không tìm thấy cho lsx: " + productionOrderInfo["name"]);
                                        bODataProcessResult.OK = false;
                                        bODataProcessResult.Message = "Mã lot " + lotScaned.lotNumber + " không tìm thấy ";
                                        return bODataProcessResult;
                                    }

                                }
                                else
                                {
                                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Thành phần " + arrMarterialProductID[1].ToString() + " Chưa có số serial nào co lsx: " + productionOrderInfo["name"]);
                                    bODataProcessResult.OK = false;
                                    bODataProcessResult.Message = "Thành phần " + arrMarterialProductID[1].ToString() + " Chưa có số serial nào. ";
                                    return bODataProcessResult;
                                }
                                //var stockMoveLineSerial = await odooAPIService.GetStockMoveLineByLotNameAsync(lotScaned.lotNumber, product_material_id, str_move_line_ids, dbConfig.UserID, dbConfig.SessionID);
                                ////var stockMoveLineSerial = await odooAPIService.GetLotByNameAndProductIDAsync(move_id, productionOrderInfo["name"], lotScaned.lotNumber, product_material_id, dbConfig.UserID, dbConfig.SessionID);
                                //if (stockMoveLineSerial == null)
                                //{
                                //    bODataProcessResult.OK = false;
                                //    bODataProcessResult.Message = "Mã lot " + lotScaned.lotNumber + " không tìm thấy " ;
                                //    return bODataProcessResult;
                                //}

                                var stockMoveLineSerial = firstStockMoveLine;
                                if (stockMoveLineSerial == null || stockMoveLineSerial.Count == 0)
                                {
                                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Mã lot " + lotScaned.lotNumber + " không tìm thấy cho lệnh sx: " + productionOrderInfo["name"]);
                                    bODataProcessResult.OK = false;
                                    bODataProcessResult.Message = "Mã lot " + lotScaned.lotNumber + " không tìm thấy ";
                                    return bODataProcessResult;
                                }
                                stockMoveLineSerial["lot_id"] = lot_id_info.ToString();
                                stockMoveLineSerial["move_line_ids"] = item["move_line_ids"].ToString();
                                stockMoveLineSerial["location_id"] = item["location_id"].ToString();
                                stockMoveLineSerial["location_dest_id"] = item["location_dest_id"].ToString();
                                stockMoveLineSerial["warehouse_id"] = item["warehouse_id"].ToString();
                                stockMoveLineSerial["picking_type_id"] = item["picking_type_id"].ToString();
                                stockMoveLineSerial["company_id"] = item["company_id"].ToString();
                                stockMoveLineSerial["mo_id"] = productionOrderInfo["id"].ToString();
                                stockMoveLineSerials.Add(stockMoveLineSerial);
                            }
                        }
                    }

                    //B3: Lưu stock move lại
                    if (stockMoveLineSerials.Count > 0)
                    {
                        //Thực hiện cập nhật stock move line theo mã lot
                        foreach (var item in stockMoveLineSerials)
                        {
                            var str_move_line_ids = item["move_line_ids"].ToString().Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Split(",");

                            //Tạo danh sách stock move line để cập nhật
                            var move_line_ids = Array.ConvertAll(str_move_line_ids, int.Parse)
                                .Select(id =>
                                {
                                    int move_line_id = int.Parse(item["id"]);
                                    if (id == move_line_id)
                                    {
                                        return new object[] { 1, id, new { lot_id = int.Parse(item["lot_id"]), qty_done = 1 } };
                                    }
                                    else
                                    {
                                        return new object[] { 4, id, false };
                                    }
                                }).ToArray();
                            var stockMoveWriteResult = await odooAPIService.SaveSerialStockMoveAsync(item, move_line_ids, dbConfig.UserID, dbConfig.SessionID);
                            if (stockMoveWriteResult == null || stockMoveWriteResult["result"].ToString() != "True")
                            {
                                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Mã lot id" + item["lot_id"] + " không tiêu hao thành công cho thành phần product id: " + item["product_id"] + " lsx: " + productionOrderInfo["name"]);
                                bODataProcessResult.OK = false;
                                bODataProcessResult.Message = "Mã lot id" + item["lot_id"] + " không tiêu hao thành công cho thành phần product id: " + item["product_id"];
                                return bODataProcessResult;
                            }
                            logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Mã lot id" + item["lot_id"] + " tiêu hao thành công cho thành phần product id: " + item["product_id"] + " lsx: " + productionOrderInfo["name"]);
                        }
                    }
                }


                int lot_id = 0;
                string lot_name = string.Empty;
                if (!string.IsNullOrWhiteSpace(productTracking) && productTracking == "serial")
                {
                    var stockLotInfo = await odooAPIService.LotSearchAsync(dataRequest.LotNumber, product_id, company_id, dbConfig.UserID, dbConfig.SessionID);
                    if (stockLotInfo == null)
                    {
                        stockLotInfo = await odooAPIService.CreateLotAsync(dataRequest.LotNumber, product_id, company_id, dbConfig.UserID, dbConfig.SessionID);
                        stockLotInfo = await odooAPIService.LotSearchAsync(dataRequest.LotNumber, product_id, company_id, dbConfig.UserID, dbConfig.SessionID);
                    }

                    if (stockLotInfo != null)
                    {
                        lot_id = (int)stockLotInfo.Last[0];
                        lot_name = (string)stockLotInfo.Last[1];
                    }
                    else
                    {
                        logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Mã lot " + dataRequest.LotNumber + " không tìm thấy để nhập cho lệnh sản xuất: " + productionOrderInfo["name"]);
                        bODataProcessResult.OK = false;
                        bODataProcessResult.Message = "Không tìm thấy hoặc tạo được mã lô: " + dataRequest.LotNumber;
                        return bODataProcessResult;
                    }
                }

                //Để trành không sử dụng lại mã lot đã dùng rồi
                var checkLotInfo = await odooAPIService.CheckUsedLotIDAsync(lot_id, dbConfig.UserID, dbConfig.SessionID);
                if (checkLotInfo != null)
                {
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Mã lô " + dataRequest.LotNumber + " đã được sử dụng cho lệnh sản xuất " + checkLotInfo["name"]);
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Mã lô " + dataRequest.LotNumber + " đã được sử dụng cho lệnh sản xuất " + checkLotInfo["name"];
                    return bODataProcessResult;
                }
                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Mã lot " + dataRequest.LotNumber + " được chuẩn bị để tiêu hao cho cho lệnh sản xuất " + productionOrderInfo["name"]);

                // Thực hiên tiêu hao nghuyên vật liệu theo BOM
                Dictionary<string, object> moveRawConsumeInfo = new Dictionary<string, object>();
                if (productionOrderInfo["product_tracking"] != "serial")
                {
                    moveRawConsumeInfo = await odooAPIService.ConsumeMaterialsByBOMAsyncv1(productionOrderInfo, dbConfig.UserID, dbConfig.SessionID, dataRequest.Quality, lot_id);
                }
                else
                {
                    //Xử lý tiêu hao nvl theo mã serial tiêu hao theo qty_produce hoặc lot_producing_id
                    for (int i = 0; i < 4; i++)
                    {
                        moveRawConsumeInfo = await odooAPIService.ConsumeMaterialsByBOMAsyncv2(productionOrderInfo, dbConfig.UserID, dbConfig.SessionID, dataRequest.Quality, lot_id, i);
                        var move_raw_ids = ((JObject)moveRawConsumeInfo["result"])["value"]["move_raw_ids"] as JArray;
                        if (move_raw_ids != null && move_raw_ids.Count > 0)
                        {
                            break;
                        }
                    }
                }

                //Thực hiện tính lại nguyên vật liệu trong trường hợp lỗi
                var result = ((JObject)moveRawConsumeInfo["result"])["value"]["move_raw_ids"] as JArray;
                var workOrderResult = ((JObject)moveRawConsumeInfo["result"])["value"]["workorder_ids"] as JArray;

                var moveRawList = new List<object>();
                var workOrderList = new List<object>();

                if (result != null)
                {
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Tiêu hao NVL thành công cho lệnh sản xuất " + productionOrderInfo["name"] + (!string.IsNullOrWhiteSpace(dataRequest.LotNumber) ? " với số seri: " + dataRequest.LotNumber : ""));
                    foreach (var item in result)
                    {
                        var id = (int)item[1];
                        if (id != 0)
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

                    if (workOrderResult != null)
                    {
                        foreach (var item in workOrderResult)
                        {
                            var id = (int)item[1];
                            if (id != 0)
                            {
                                var detail = item[2] as JObject;

                                decimal qty_producing = 0;
                                try
                                {
                                    qty_producing = decimal.Parse(detail?["qty_producing"]?.ToString());
                                }
                                catch
                                {
                                    qty_producing = 0;
                                }

                                decimal duration_expected = 0;
                                try
                                {
                                    duration_expected = decimal.Parse(detail?["duration_expected"]?.ToString());
                                }
                                catch
                                {
                                    duration_expected = 0;
                                }

                                int finished_lot_id = 0;
                                try
                                {
                                    finished_lot_id = int.Parse(detail?["finished_lot_id"][0]?.ToString());
                                }
                                catch
                                {
                                    finished_lot_id = 0;
                                }

                                if (qty_producing != 0)
                                {
                                    var workOrder = new object[]
                                    {
                                        1,
                                        id,
                                        new {
                                            qty_producing = qty_producing,
                                            duration_expected = duration_expected,
                                            finished_lot_id = finished_lot_id
                                        }
                                    };
                                    workOrderList.Add(workOrder);
                                }
                                else
                                {
                                    var workOrder = new object[]
                                    {
                                        4,
                                        id,
                                        false
                                    };
                                    workOrderList.Add(workOrder);
                                }
                            }
                        }
                    }

                    // Danh sách thành phần tiêu hao
                    object[] move_raw_ids = moveRawList.ToArray();
                    object[] work_order_ids = workOrderList.ToArray();

                    int mrp_production_id = int.Parse(productionOrderInfo["id"]);

                    var saveResult = await odooAPIService.SaveProductionOrderAsyncv1(mrp_production_id, lot_id, dataRequest.Quality, move_raw_ids, work_order_ids, dbConfig.UserID, dbConfig.SessionID);
                    if (saveResult == null || saveResult["result"].ToString() != "True")
                    {
                        logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Lỗi không lưu được lệnh sản xuất " + productionOrderInfo["name"] + (!string.IsNullOrWhiteSpace(dataRequest.LotNumber) ? " với số seri: " + dataRequest.LotNumber : ""));
                        bODataProcessResult.OK = false;
                        bODataProcessResult.Message = "Lỗi không lưu được lệnh sản xuất ";
                        return bODataProcessResult;
                    }
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Lưu lệnh sản xuất thành công " + productionOrderInfo["name"] + (!string.IsNullOrWhiteSpace(dataRequest.LotNumber) ? " với số seri: " + dataRequest.LotNumber : ""));

                    var markDoneResult = await odooAPIService.MarkDoneProductionOrderAsync(mrp_production_id, dbConfig.UserID, dbConfig.SessionID);

                    var backOrderOnchangeResult = await odooAPIService.BackOrderOnchange(mrp_production_id, dbConfig.UserID, dbConfig.SessionID);

                    if (!dataRequest.IsLastOrder)
                    {
                        var backorder_id = await odooAPIService.BackOrderCreate(mrp_production_id, dbConfig.UserID, dbConfig.SessionID, lot_id);

                        var backorderResult = await odooAPIService.BackOrderAction(mrp_production_id, backorder_id, dbConfig.UserID, dbConfig.SessionID);
                    }



                    bODataProcessResult.OK = true;
                    bODataProcessResult.Message = "Hoàn thành lệnh sản xuất";
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Hoàn thành lệnh sản xuất " + productionOrderInfo["name"] + (!string.IsNullOrWhiteSpace(dataRequest.LotNumber) ? " với số seri: " + dataRequest.LotNumber : ""));
                }
                else
                {
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Tiêu hao NVL thất bại cho lệnh sản xuất " + productionOrderInfo["name"]);
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Lỗi không tiêu hao được nguyên vật liệu";
                }


            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

                if (!string.IsNullOrWhiteSpace(bODataProcessResult.Message) && bODataProcessResult.Message.Contains("Odoo Session Expired"))
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }

            }
            return bODataProcessResult;
        }


        /// <summary>
        /// Thực hiện tiêu hao trong cả trường hợp không có dữ phần
        /// </summary>
        /// <param name="dataRequest"></param>
        /// <returns></returns>
        [Route("InputProductionByWorkOrderv2")]
        [HttpPost]
        public async Task<BODataProcessResult> InputProductionByWorkOrderv2(InputProductDataRequest dataRequest)
        {
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            bODataProcessResult = await InputProductionResultToViindoo(dataRequest);
            return bODataProcessResult;
        }

        /// <summary>
        /// Thực hiện tiêu hao trong cả trường hợp không có dữ phần
        /// </summary>
        /// <returns></returns>
        [Route("SynchProductionResultDataToViindoo")]
        [HttpPost]
        public async Task<BODataProcessResult> SynchProductionResultDataToViindoo()
        {
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            List<BODataProcessResult> SyncBODataResults = new List<BODataProcessResult>();
            SVN_ProductionInputLogDataPortal dataPortal = new SVN_ProductionInputLogDataPortal(svnDBConfig.ConnectionString);
            SVN_AppSetting_v1DataPortal appSettingDataPortal = new SVN_AppSetting_v1DataPortal(svnDBConfig.ConnectionString);
            var synchTimeData = await appSettingDataPortal.GetSettingByGroupAndKey("SynchPDInputMinutesTime", "SVNCoreAPI");
            int synchTime = 10;
            if (synchTimeData != null && int.TryParse(synchTimeData.Value, out int result))
            {
                synchTime = result;
            }

            try
            {
                //DateTime timeBefore = DateTime.Now.AddMinutes(-synchTime);
                DateTime timeBefore = DateTime.Today;
                var inputtedData = await dataPortal.GetDataByDateFinishedAsync(timeBefore);
                //var inputtedData = await dataPortal.GetDataByIdAsync(82);
                if (inputtedData != null)
                {
                    //bỏ hết các trường hợp serial_code bị trống
                    inputtedData = inputtedData.Where(x => !string.IsNullOrWhiteSpace(x.serial_code)).ToList();

                    foreach (var item in inputtedData)
                    {
                        InputProductDataRequest request = new InputProductDataRequest();
                        request = JsonConvert.DeserializeObject<InputProductDataRequest>(item.API_parameters);
                        var inputResult = await InputProductionResultToViindoo(request);
                        if(inputResult.OK)
                        {
                            item.status = "synch success";
                            inputResult.Message = DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss") + " - " + item.wo_code + " - " + inputResult.Message;
                            SyncBODataResults.Add(inputResult);
                        }
                        else
                        {
                            item.status = "synch failed";
                            inputResult.Message = DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss") + " - " + item.wo_code + " - " + inputResult.Message;
                            SyncBODataResults.Add(inputResult);
                        }
                        var updateResult = await dataPortal.UpdateAsync(item);
                    }
                    var successCount = SyncBODataResults.Count(x => x.OK);
                    var failedCount = SyncBODataResults.Count(x => !x.OK);
                    if (failedCount > 0)
                    {
                        bODataProcessResult.OK = false;
                        bODataProcessResult.Message = $"{DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss")} | Đồng bộ hoàn tất với {successCount} bản ghi thành công và {failedCount} bản ghi lỗi";
                        bODataProcessResult.Content = SyncBODataResults;
                    }
                    else
                    {
                        bODataProcessResult.OK = true;
                        bODataProcessResult.Message = $"{DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss")} | Đồng bộ hoàn tất với {successCount} bản ghi thành công";
                    }
                }
            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

                if (!string.IsNullOrWhiteSpace(bODataProcessResult.Message) && bODataProcessResult.Message.Contains("Odoo Session Expired"))
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }
            }
            return bODataProcessResult;
        }

        /// <summary>
        /// Thực hiện tiêu hao trong cả trường hợp không có dữ phần
        /// </summary>
        /// <returns></returns>
        [Route("ReSynchFailedProductionResultDataToViindoo")]
        [HttpPost]
        public async Task<BODataProcessResult> ReSynchFailedProductionResultDataToViindoo(SynchPDDataRequest dataRequest)
        {
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            List<BODataProcessResult> SyncBODataResults = new List<BODataProcessResult>();
            SVN_ProductionInputLogDataPortal dataPortal = new SVN_ProductionInputLogDataPortal(svnDBConfig.ConnectionString);
            

            try
            {
                DateTime fromDate = dataRequest.FromDate;
                DateTime toDate = dataRequest.ToDate;

                var inputtedData = await dataPortal.GetDataFromDateToDateFinishedAsync(fromDate, toDate, dataRequest.Status);
                //var inputtedData = await dataPortal.GetDataByIdAsync(82);
                if (inputtedData != null)
                {
                    //bỏ hết các trường hợp serial_code bị trống
                    inputtedData = inputtedData.Where(x => !string.IsNullOrWhiteSpace(x.serial_code)).ToList();

                    foreach (var item in inputtedData)
                    {
                        InputProductDataRequest request = new InputProductDataRequest();
                        request = JsonConvert.DeserializeObject<InputProductDataRequest>(item.API_parameters);
                        var inputResult = await InputProductionResultToViindoo(request);
                        if (inputResult.OK)
                        {
                            item.status = "synch success";
                            inputResult.Message = item.wo_code + " - " + inputResult.Message;
                            SyncBODataResults.Add(inputResult);
                        }
                        else
                        {
                            item.status = "synch failed";
                            inputResult.Message = item.wo_code + " - " + inputResult.Message;
                            SyncBODataResults.Add(inputResult);
                        }
                        var updateResult = await dataPortal.UpdateAsync(item);
                    }
                    var successCount = SyncBODataResults.Count(x => x.OK);
                    var failedCount = SyncBODataResults.Count(x => !x.OK);
                    if (failedCount > 0)
                    {
                        bODataProcessResult.OK = false;
                        bODataProcessResult.Message = $"{DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss")} | Đồng bộ hoàn tất với {successCount} bản ghi thành công và {failedCount} bản ghi lỗi";
                        bODataProcessResult.Content = SyncBODataResults;
                    }
                    else
                    {
                        bODataProcessResult.OK = true;
                        bODataProcessResult.Message = $"{DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss")} | Đồng bộ hoàn tất với {successCount} bản ghi thành công";
                    }
                }
                else
                {
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Không tìm thấy dữ liệu cần đồng bộ";
                }
            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

                if (!string.IsNullOrWhiteSpace(bODataProcessResult.Message) && bODataProcessResult.Message.Contains("Odoo Session Expired"))
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }
            }
            return bODataProcessResult;
        }

        /// <summary>
        /// Check mã lot thành phẩm có tồn tại ko
        /// </summary>
        /// <param name="dataRequest"></param>
        /// <returns></returns>
        [Route("SearchSerialLotFG")]
        [HttpPost]
        public async Task<BODataProcessResult> SearchSerialLotFG(ProductDataRequest dataRequest)
        {
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            try
            {
                int lot_id = 0;
                string lot_name = string.Empty;

                if (string.IsNullOrWhiteSpace(dbConfig.SessionID) || dbConfig.UserID == 0)
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }

                //Kiểm tra mã lot thành phẩm có tồn tại không
                var stockLotInfo = await odooAPIService.LotSearchAsync(dataRequest.lotNumber, dataRequest.product_id, 1, dbConfig.UserID, dbConfig.SessionID);
                //if (stockLotInfo == null)
                //{
                //    bODataProcessResult.OK = false;
                //    bODataProcessResult.Message = "Không tìm thấy mã lot: " + dataRequest.lotNumber;
                //    return bODataProcessResult;
                //}
                if (stockLotInfo == null)
                {
                    stockLotInfo = await odooAPIService.CreateLotAsync(dataRequest.lotNumber, dataRequest.product_id, 1, dbConfig.UserID, dbConfig.SessionID);
                    stockLotInfo = await odooAPIService.LotSearchAsync(dataRequest.lotNumber, dataRequest.product_id, 1, dbConfig.UserID, dbConfig.SessionID);
                }

                if (stockLotInfo != null)
                {
                    lot_id = (int)stockLotInfo.Last[0];
                    lot_name = (string)stockLotInfo.Last[1];
                }
                else
                {
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Không tìm thấy hoặc tạo được mã lô: " + dataRequest.lotNumber;
                    return bODataProcessResult;
                }

                //Để trành không sử dụng lại mã lot đã dùng rồi
                var checkLotInfo = await odooAPIService.CheckUsedLotIDAsync(lot_id, dbConfig.UserID, dbConfig.SessionID);
                if (checkLotInfo != null)
                {
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Mã lô " + dataRequest.lotNumber + " đã được sử dụng cho lệnh sản xuất " + checkLotInfo["name"];
                    return bODataProcessResult;
                }

                bODataProcessResult.OK = true;
            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

                if (!string.IsNullOrWhiteSpace(bODataProcessResult.Message) && bODataProcessResult.Message.Contains("Odoo Session Expired"))
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }
            }
            return bODataProcessResult;
        }

        /// <summary>
        /// Kiểm tra mã lot của các thành phần đã được sử dụng chưa
        /// </summary>
        /// <param name="dataRequest"></param>
        /// <returns></returns>
        [Route("GetUsedLotForComponemt")]
        [HttpPost]
        public async Task<BODataProcessResult> GetUsedLotForComponemt(ProductDataRequest dataRequest)
        {
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            try
            {
                if (string.IsNullOrWhiteSpace(dbConfig.SessionID) || dbConfig.UserID == 0)
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }

                var lot_id_info = await odooAPIService.GetLotInfoInWarehoure(dataRequest.lotNumber, dataRequest.product_id, dbConfig.UserID, dbConfig.SessionID);
                if (lot_id_info == 0)
                {
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Mã lot " + dataRequest.lotNumber + " không tìm thấy ";
                    return bODataProcessResult;
                }

                if (dataRequest.hasTracking == "serial")
                {
                    var result = await odooAPIService.GetUsedLotForComponemtAsync(dataRequest.seriNumber, dataRequest.lotNumber, dataRequest.product_id, dbConfig.UserID, dbConfig.SessionID);
                    if (result != null && result.Count > 0)
                    {
                        bODataProcessResult.OK = false;
                        bODataProcessResult.Message = "Mã lot: " + dataRequest.lotNumber + " đã được sử dụng cho lệnh sản xuất: " + result["reference"];
                        bODataProcessResult.Content = result;
                    }
                    else
                    {
                        bODataProcessResult.OK = true;
                        bODataProcessResult.Message = "Mã lot: " + dataRequest.lotNumber + " chưa được sử dụng";
                    }
                }
                else if (dataRequest.hasTracking == "lot")
                {
                    var result = await odooAPIService.GetLotRemainingQtyAsync(dataRequest.lotNumber, dataRequest.product_id, dbConfig.SessionID);
                    if (result != 0)
                    {
                        bODataProcessResult.OK = true;
                        bODataProcessResult.Message = "Mã lot: " + dataRequest.lotNumber + " tồn tại với số lượng còn lại là: " + result;
                    }
                    else
                    {
                        bODataProcessResult.OK = false;
                        bODataProcessResult.Message = "Không tìm thấy mã lot: " + dataRequest.lotNumber + " hoặc tồn của mã lot đã hết";
                    }
                }
            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

                if (!string.IsNullOrWhiteSpace(bODataProcessResult.Message) && bODataProcessResult.Message.Contains("Odoo Session Expired"))
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }
            }
            return bODataProcessResult;
        }

        /// <summary>
        /// Lấy thông tin sản phẩm theo mã
        /// </summary>
        /// <param name="code"></param>
        /// <returns></returns>
        [Route("GetProductItemByCode")]
        [HttpGet]
        public async Task<BODataProcessResult> GetProductItemByCode(string code)
        {
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            try
            {
                if (string.IsNullOrWhiteSpace(dbConfig.SessionID) || dbConfig.UserID == 0)
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }
                //dbConfig.SessionID = "abc";
                //dbConfig.UserID = 1;

                var result = await odooAPIService.GetProductItemByCode(code, dbConfig.UserID, dbConfig.SessionID);
                if (result != null && result.Count > 0)
                {
                    bODataProcessResult.OK = true;
                    bODataProcessResult.Message = "Lấy thông tin sản phẩm" + code + " thành công";
                    bODataProcessResult.Content = result["x_quantity_per_code"];
                }
                else
                {
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Không tìm thấy sản phẩm có mã: " + code;
                }
            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

                if (!string.IsNullOrWhiteSpace(bODataProcessResult.Message) && bODataProcessResult.Message.Contains("Odoo Session Expired"))
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }
            }
            return bODataProcessResult;
        }

        /// <summary>
        /// Lấy thông tin sản phẩm theo mã - rồi thực hiên insert hoặc là update
        /// </summary>
        /// <param name="dataRequest"></param>
        /// <returns></returns>
        [Route("InsertProductItem")]
        [HttpPost]
        public async Task<BODataProcessResult> InsertWriteProductItem(ItemDataRequest dataRequest)
        {
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            try
            {
                if (string.IsNullOrWhiteSpace(dbConfig.SessionID) || dbConfig.UserID == 0)
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }

                var result = await odooAPIService.SearchProductTemplate(dataRequest.ItemCode, dbConfig.UserID, dbConfig.SessionID);
                if (result != 0)
                {
                    var updateResult = await odooAPIService.WriteProductTemplate(result, dataRequest.ItemCode, dataRequest.ItemName, dbConfig.UserID, dbConfig.SessionID);
                    if (updateResult)
                    {
                        bODataProcessResult.OK = true;
                        bODataProcessResult.Message = "Cập nhật item thành công";
                        bODataProcessResult.Content = result;
                    }
                    else
                    {
                        bODataProcessResult.OK = false;
                        bODataProcessResult.Message = "Cập nhật item thất bại";
                    }
                }
                else
                {
                    var createResult = await odooAPIService.CreateProductTemplate(dataRequest.ItemCode, dataRequest.ItemName, dbConfig.UserID, dbConfig.SessionID);
                    if (createResult != 0)
                    {
                        bODataProcessResult.OK = true;
                        bODataProcessResult.Message = "Tạo item thành công";
                        bODataProcessResult.Content = createResult;
                    }
                    else
                    {
                        bODataProcessResult.OK = false;
                        bODataProcessResult.Message = "Tạo item thất bại";
                    }
                }
            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

                if (!string.IsNullOrWhiteSpace(bODataProcessResult.Message) && bODataProcessResult.Message.Contains("Odoo Session Expired"))
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }
            }
            return bODataProcessResult;
        }

        [Route("InsertBOM")]
        [HttpPost]
        public async Task<BODataProcessResult> InsertBOM(BOMDataRequest dataRequest)
        {
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            try
            {
                if (string.IsNullOrWhiteSpace(dbConfig.SessionID) || dbConfig.UserID == 0)
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }

                var productResult = await odooAPIService.SearhProductItem(dataRequest.ItemCode, dbConfig.UserID, dbConfig.SessionID);
                try
                {
                    int productItemID = productResult.result[0].id;
                    dataRequest.ProductID = productItemID;
                }
                catch
                {
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Item " + dataRequest.ItemCode + " chưa tồn tại";
                    return bODataProcessResult;
                }

                try
                {
                    int productTempItemID = productResult.result[0].product_tmpl_id[0];
                    dataRequest.ProductTempID = productTempItemID;

                    //Lấy productTemp để lấy đơn vị ra
                    var productTemp = await odooAPIService.SearhProductTemp(productTempItemID, dbConfig.UserID, dbConfig.SessionID);
                    try
                    {
                        int UomID = productTemp.result[0].uom_id[0];
                        dataRequest.ProductUomlID = UomID;
                    }
                    catch
                    {
                        bODataProcessResult.OK = false;
                        bODataProcessResult.Message = "ID product template " + productTempItemID + " chưa tồn tại";
                        return bODataProcessResult;
                    }
                }
                catch
                {
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Item " + dataRequest.ItemCode + " chưa tồn tại";
                    return bODataProcessResult;
                }

                if (dataRequest.Items != null && dataRequest.Items.Count > 0)
                {
                    foreach (var item in dataRequest.Items)
                    {
                        var productSubItemResult = await odooAPIService.SearhProductItem(item.ItemCode, dbConfig.UserID, dbConfig.SessionID);
                        try
                        {
                            int productItemID = productSubItemResult.result[0].id;
                            item.ProductID = productItemID;

                        }
                        catch
                        {
                            bODataProcessResult.OK = false;
                            bODataProcessResult.Message = "Item " + item.ItemCode + " chưa tồn tại";
                            return bODataProcessResult;
                        }

                        try
                        {
                            int productTempItemID = productSubItemResult.result[0].product_tmpl_id[0];

                            //Lấy productTemp để lấy đơn vị ra
                            var productTemp = await odooAPIService.SearhProductTemp(productTempItemID, dbConfig.UserID, dbConfig.SessionID);
                            try
                            {
                                int UomID = productTemp.result[0].uom_id[0];
                                item.ProductUomlID = UomID;
                            }
                            catch
                            {
                                bODataProcessResult.OK = false;
                                bODataProcessResult.Message = "ID product template " + productTempItemID + " chưa tồn tại";
                                return bODataProcessResult;
                            }
                        }
                        catch
                        {
                            bODataProcessResult.OK = false;
                            bODataProcessResult.Message = "Item " + item.ItemCode + " chưa tồn tại";
                            return bODataProcessResult;
                        }
                    }
                }

                var result = await odooAPIService.InsertBOM(dataRequest, dbConfig.UserID, dbConfig.SessionID);
                if (result != 0)
                {
                    bODataProcessResult.OK = true;
                    bODataProcessResult.Message = "Tạo BOM thành công";
                    bODataProcessResult.Content = result;
                }
                else
                {
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Tạo BOM thất bại";
                }
            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

                if (!string.IsNullOrWhiteSpace(bODataProcessResult.Message) && bODataProcessResult.Message.Contains("Odoo Session Expired"))
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }
            }
            return bODataProcessResult;
        }

        [Route("GetBOMDetails")]
        [HttpPost]
        public async Task<BODataProcessResult> GetBOMDetails(BOMDataRequest dataRequest)
        {
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            try
            {
                if (string.IsNullOrWhiteSpace(dbConfig.SessionID) || dbConfig.UserID == 0)
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }

                var productSubItemResult = await odooAPIService.SearhProductItem(dataRequest.ItemCode, dbConfig.UserID, dbConfig.SessionID);
                int product_tmpl_id = 0;
                try
                {
                    product_tmpl_id = productSubItemResult.result[0].product_tmpl_id[0];

                }
                catch
                {
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Item " + dataRequest.ItemCode + " chưa tồn tại";
                    return bODataProcessResult;
                }

                if (product_tmpl_id != 0)
                {
                    var bomResponse = await odooAPIService.SearchBOMByProductTemplateID(product_tmpl_id, dbConfig.UserID, dbConfig.SessionID);

                    if (bomResponse == null || bomResponse.result == null || ((JArray)bomResponse.result).Count == 0)
                    {
                        bODataProcessResult.OK = false;
                        bODataProcessResult.Message = $"BOM infomation for Item code {product_tmpl_id} not found";
                        return bODataProcessResult;
                    }

                    int GetIdFromMany2one(dynamic field)
                    {
                        if (field is JArray && ((JArray)field).Count > 0)
                        {
                            return (int)field[0];
                        }
                        return 0;
                    }

                    var bomData = bomResponse.result[0];
                    int bomId = bomData.id;
                    // Lấy danh sách ID của các Bom Lines (Odoo trả về dạng mảng ID)
                    var lineIds = bomData["bom_line_ids"]?.ToObject<List<int>>();
                    mrp_bomUI bomUI = new mrp_bomUI();
                    bomUI.id = bomId;
                    bomUI.active = bomData.active ?? false;
                    bomUI.company_id = GetIdFromMany2one(bomData.company_id);
                    bomUI.product_tmpl_id = GetIdFromMany2one(bomData.product_tmpl_id);
                    bomUI.product_qty = bomData.product_qty ?? 0;
                    bomUI.product_uom_id = GetIdFromMany2one(bomData.product_uom_id);
                    bomUI.allow_operation_dependencies = bomData.allow_operation_dependencies ?? false;
                    bomUI.code = bomData.code?.ToString() ?? string.Empty;
                    bomUI.type = bomData.type?.ToString() ?? string.Empty;
                    bomUI.ready_to_produce = bomData.ready_to_produce?.ToString() ?? string.Empty;
                    bomUI.version = bomData.version ?? 0;

                    // Sửa lỗi previous_bom_id ở đây
                    bomUI.previous_bom_id = GetIdFromMany2one(bomData.previous_bom_id);

                    bomUI.consumption = bomData.consumption?.ToString() ?? string.Empty;
                    bomUI.picking_type_id = GetIdFromMany2one(bomData.picking_type_id);
                    bomUI.create_date = bomData.create_date ?? DateTime.MinValue;
                    bomUI.write_date = bomData.write_date ?? DateTime.MinValue;


                    List<mrp_bom_lineUI> bomLines = new List<mrp_bom_lineUI>();
                    if (lineIds != null && lineIds.Count > 0)
                    {
                        var linesResponse = await odooAPIService.GetBomLinesByIdsAsync(lineIds, dbConfig.UserID, dbConfig.SessionID);

                        if (linesResponse != null && linesResponse.result != null)
                        {
                            foreach (var line in linesResponse.result)
                            {
                                bomLines.Add(new mrp_bom_lineUI
                                {
                                    id = line.id,
                                    company_id = GetIdFromMany2one(line.company_id),
                                    sequence = line.sequence ?? 0,
                                    product_id = GetIdFromMany2one(line.product_id),
                                    product_tmpl_id = GetIdFromMany2one(line.product_tmpl_id),
                                    standard_qty = line.standard_qty ?? 0,
                                    loss_rate = line.loss_rate ?? 0,
                                    product_qty = line.product_qty ?? 0,
                                    product_uom_id = GetIdFromMany2one(line.product_uom_id),
                                    manual_consumption = line.manual_consumption ?? false,
                                    cost_share = line.cost_share ?? 0,
                                    bom_id = GetIdFromMany2one(line.bom_id),

                                    // Đối với DateTime, Odoo trả về string hoặc false, nên dùng ép kiểu an toàn
                                    create_date = line.create_date ?? DateTime.MinValue,
                                    write_date = line.write_date ?? DateTime.MinValue
                                });
                            }
                        }
                    }

                    if (bomUI.product_tmpl_id != 0)
                    {
                        mrp_bom_newDataPortal mrp_Bom_NewDataPortal = new mrp_bom_newDataPortal(svnDBConfig.ConnectionString);
                        var existingBom = mrp_Bom_NewDataPortal.GetDataByProductTemplateID(bomUI.product_tmpl_id);
                        if (existingBom != null)
                        {
                            if (existingBom.id != bomUI.id)
                            {
                                var deleteResult = mrp_Bom_NewDataPortal.Delete(existingBom.id);
                                if (deleteResult)
                                {
                                    var insertResult = mrp_Bom_NewDataPortal.Insert(bomUI);
                                }
                            }
                            else
                            {
                                if (existingBom.write_date < bomUI.write_date)
                                {
                                    var updateResult = mrp_Bom_NewDataPortal.Update(bomUI);
                                }
                            }
                        }
                        else
                        {
                            var insertResult = mrp_Bom_NewDataPortal.Insert(bomUI);
                        }

                        if (bomLines != null && bomLines.Count > 0)
                        {
                            mrp_bom_line_newDataPortal mrp_Bom_Line_NewDataPortal = new mrp_bom_line_newDataPortal(svnDBConfig.ConnectionString);
                            var existingBomLines = mrp_Bom_Line_NewDataPortal.GetDataByBomID(bomUI.id);
                            foreach (var line in bomLines)
                            {
                                if (existingBomLines != null)
                                {
                                    var existingLine = existingBomLines.FirstOrDefault(x => x.product_id == line.product_id);
                                    if (existingLine != null)
                                    {
                                        if (existingLine.id != line.id)
                                        {
                                            var deleteResult = mrp_Bom_Line_NewDataPortal.Delete(existingLine.id);
                                            if (deleteResult)
                                            {
                                                var insertResult = mrp_Bom_Line_NewDataPortal.Insert(line);
                                            }
                                        }
                                        else
                                        {
                                            if (existingLine.write_date < line.write_date)
                                            {
                                                var updateResult = mrp_Bom_Line_NewDataPortal.Update(line);
                                            }
                                        }

                                    }
                                    else
                                    {
                                        var insertResult = mrp_Bom_Line_NewDataPortal.Insert(line);
                                    }
                                }
                                else
                                {
                                    var insertResult = mrp_Bom_Line_NewDataPortal.Insert(line);
                                }
                            }
                        }
                    }
                }
                else
                {
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = $"Item code {dataRequest.ItemCode} not found";
                }
            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

                if (!string.IsNullOrWhiteSpace(bODataProcessResult.Message) && bODataProcessResult.Message.Contains("Odoo Session Expired"))
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }
            }
            return bODataProcessResult;
        }

        #region private methods
        private async Task<BODataProcessResult> InputProductionResultToViindoo(InputProductDataRequest dataRequest)
        {
            LogService logger = new LogService(svnDBConfig.ConnectionString);
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            try
            {
                if (string.IsNullOrWhiteSpace(dbConfig.SessionID) || dbConfig.UserID == 0)
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }

                //Lấy dữ liệu lệnh sản xuất
                var productionOrderInfo = await odooAPIService.ReadProductionByProductIDAsync(dataRequest.WorkOrderNumber, dbConfig.UserID, dbConfig.SessionID);
                if (productionOrderInfo == null)
                {
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Không tìm thấy lệnh sản xuất cho mã seri: " + dataRequest.WorkOrderNumber);
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Không tìm thấy lệnh sản xuất cho mã seri: " + dataRequest.WorkOrderNumber;
                    return bODataProcessResult;
                }
                int remainingQty = int.Parse(productionOrderInfo["product_qty"]);
                if (dataRequest.Quality >= remainingQty)
                {
                    dataRequest.IsLastOrder = true;
                }

                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Bắt đầu thực hiện lệnh sản xuất: " + productionOrderInfo["name"]);

                var str_move_ids = productionOrderInfo["move_raw_ids"].Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Split(",");
                var move_ids = Array.ConvertAll(str_move_ids, int.Parse);

                var productTracking = productionOrderInfo["product_tracking"]?.ToString();

                //Lấy product_id
                var arrProductID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["product_id"]);
                var product_id = Convert.ToInt32(arrProductID[0]);

                //Lấy company_id
                var arrCompanyID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["company_id"]);
                var company_id = Convert.ToInt32(arrCompanyID[0]);

                //Get stock move
                var stockMoveInfo = await odooAPIService.GetStockMoveByIDAsync(move_ids, company_id, dbConfig.UserID, dbConfig.SessionID);

                //Lấy các thành phần được theo dõi theo mã lot hoặc serial
                List<Dictionary<string, object>> stockMoveSerialInfo = new List<Dictionary<string, object>>();
                if (stockMoveInfo != null)
                {
                    foreach (var item in stockMoveInfo)
                    {
                        var token = (JToken)item["has_tracking"];
                        if (token.Type == JTokenType.String && token?.ToString() == "serial" || token.Type == JTokenType.String && token?.ToString() == "lot")
                        {
                            stockMoveSerialInfo.Add(item);
                        }
                    }
                }

                if (stockMoveSerialInfo.Count > 0)
                {
                    //Dữ liệu dùng để thay đổi stock move line có serial theo lệnh sản xuất
                    List<Dictionary<string, string>> stockMoveLineSerials = new List<Dictionary<string, string>>();

                    foreach (var item in stockMoveSerialInfo)
                    {
                        var str_move_line_ids = item["move_line_ids"].ToString().Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Split(",").ToList();
                        //var move_line_ids = Array.ConvertAll(str_move_line_ids, int.Parse);

                        //Lấy product_id
                        var arrMarterialProductID = JsonConvert.DeserializeObject<object[]>(item["product_id"].ToString());
                        var product_material_id = Convert.ToInt32(arrMarterialProductID[0]);

                        var move_id = int.Parse(item["id"].ToString());

                        if (dataRequest.LotScaneds.Count > 0)
                        {
                            var lotScaned = dataRequest.LotScaneds.FirstOrDefault(x => x.product_id == product_material_id);
                            int lot_id_info = 0;
                            if (lotScaned != null)
                            {
                                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Xử lý thành phần có mã serial: " + lotScaned.lotNumber + " và mã nguyên liệu là: " + arrMarterialProductID[1] + " cho lsx: " + productionOrderInfo["name"]);
                                //Bước cần thay đổi theo cách mới để lấy stock move line theo mã lot
                                //B1: Lấy ra stock move line đầu tiên trong danh sách thuộc stock move - checked
                                Dictionary<string, string> firstStockMoveLine = new Dictionary<string, string>();

                                lot_id_info = await odooAPIService.GetLotInfo(lotScaned.lotNumber, Convert.ToInt32(productionOrderInfo["id"]), item, dbConfig.UserID, dbConfig.SessionID);
                                if (lot_id_info == 0)
                                {
                                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Mã lot " + lotScaned.lotNumber + " không tìm thấy cho lsx: " + productionOrderInfo["name"]);
                                    bODataProcessResult.OK = false;
                                    bODataProcessResult.Message = "Mã lot " + lotScaned.lotNumber + " không tìm thấy ";
                                    return bODataProcessResult;
                                }

                                if (str_move_line_ids != null && str_move_line_ids.Count > 0)
                                {
                                    if (str_move_line_ids.Count == 1 && str_move_line_ids[0] == "[]")
                                    {
                                        var createResult = await odooAPIService.CreateLotComponentForMO(lot_id_info, Convert.ToInt32(productionOrderInfo["id"]), item, dbConfig.UserID, dbConfig.SessionID);
                                        if (createResult == null || createResult["result"].ToString() != "True")
                                        {
                                            logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Lỗi không tiêu hao mã lot " + lotScaned.lotNumber + " cho NVL" + item["product_id"] + " cho lsx: " + productionOrderInfo["name"]);
                                            bODataProcessResult.OK = false;
                                            bODataProcessResult.Message = "Lỗi không tạo được stock move line cho mã lot " + lotScaned.lotNumber;
                                            return bODataProcessResult;
                                        }
                                        firstStockMoveLine["is_created_stock_move_line"] = "True";
                                    }
                                    else
                                    {
                                        firstStockMoveLine = await odooAPIService.GetStockMoveLineByID(Convert.ToInt32(str_move_line_ids[0]), Convert.ToInt32(productionOrderInfo["id"]), item, dbConfig.UserID, dbConfig.SessionID);
                                        firstStockMoveLine["is_created_stock_move_line"] = "False";
                                    }

                                }
                                else
                                {
                                    //Nếu Không được dữ phần thì thực hiện gán sô seri cho stock move
                                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Thành phần " + arrMarterialProductID[1].ToString() + " Chưa có số serial nào co lsx: " + productionOrderInfo["name"]);
                                    bODataProcessResult.OK = false;
                                    bODataProcessResult.Message = "Thành phần " + arrMarterialProductID[1].ToString() + " Chưa có số serial nào. ";
                                    return bODataProcessResult;
                                }
                                //var stockMoveLineSerial = await odooAPIService.GetStockMoveLineByLotNameAsync(lotScaned.lotNumber, product_material_id, str_move_line_ids, dbConfig.UserID, dbConfig.SessionID);
                                ////var stockMoveLineSerial = await odooAPIService.GetLotByNameAndProductIDAsync(move_id, productionOrderInfo["name"], lotScaned.lotNumber, product_material_id, dbConfig.UserID, dbConfig.SessionID);
                                //if (stockMoveLineSerial == null)
                                //{
                                //    bODataProcessResult.OK = false;
                                //    bODataProcessResult.Message = "Mã lot " + lotScaned.lotNumber + " không tìm thấy " ;
                                //    return bODataProcessResult;
                                //}

                                var stockMoveLineSerial = firstStockMoveLine;
                                if (stockMoveLineSerial == null || stockMoveLineSerial.Count == 0)
                                {
                                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Mã lot " + lotScaned.lotNumber + " không tìm thấy cho lệnh sx: " + productionOrderInfo["name"]);
                                    bODataProcessResult.OK = false;
                                    bODataProcessResult.Message = "Mã lot " + lotScaned.lotNumber + " không tìm thấy ";
                                    return bODataProcessResult;
                                }
                                stockMoveLineSerial["lot_id"] = lot_id_info.ToString();
                                stockMoveLineSerial["move_line_ids"] = item["move_line_ids"].ToString();
                                stockMoveLineSerial["location_id"] = item["location_id"].ToString();
                                stockMoveLineSerial["location_dest_id"] = item["location_dest_id"].ToString();
                                stockMoveLineSerial["warehouse_id"] = item["warehouse_id"].ToString();
                                stockMoveLineSerial["picking_type_id"] = item["picking_type_id"].ToString();
                                stockMoveLineSerial["company_id"] = item["company_id"].ToString();
                                stockMoveLineSerial["mo_id"] = productionOrderInfo["id"].ToString();
                                stockMoveLineSerials.Add(stockMoveLineSerial);
                            }
                        }
                    }

                    //B3: Lưu stock move lại
                    if (stockMoveLineSerials.Count > 0)
                    {
                        //Thực hiện cập nhật stock move line theo mã lot
                        foreach (var item in stockMoveLineSerials)
                        {
                            if (item.ContainsKey("is_created_stock_move_line") && item["is_created_stock_move_line"] == "True")
                            {
                                //Nếu là mới tạo thì bỏ qua không cần cập nhật nữa
                                continue;
                            }
                            var str_move_line_ids = item["move_line_ids"].ToString().Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Split(",");

                            //Tạo danh sách stock move line để cập nhật
                            var move_line_ids = Array.ConvertAll(str_move_line_ids, int.Parse)
                                .Select(id =>
                                {
                                    int move_line_id = int.Parse(item["id"]);
                                    if (id == move_line_id)
                                    {
                                        return new object[] { 1, id, new { lot_id = int.Parse(item["lot_id"]), qty_done = 1 } };
                                    }
                                    else
                                    {
                                        return new object[] { 4, id, false };
                                    }
                                }).ToArray();
                            var stockMoveWriteResult = await odooAPIService.SaveSerialStockMoveAsync(item, move_line_ids, dbConfig.UserID, dbConfig.SessionID);
                            if (stockMoveWriteResult == null || stockMoveWriteResult["result"].ToString() != "True")
                            {
                                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Mã lot id" + item["lot_id"] + " không tiêu hao thành công cho thành phần product id: " + item["product_id"] + " lsx: " + productionOrderInfo["name"]);
                                bODataProcessResult.OK = false;
                                bODataProcessResult.Message = "Mã lot id" + item["lot_id"] + " không tiêu hao thành công cho thành phần product id: " + item["product_id"];
                                return bODataProcessResult;
                            }
                            logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Mã lot id" + item["lot_id"] + " tiêu hao thành công cho thành phần product id: " + item["product_id"] + " lsx: " + productionOrderInfo["name"]);
                        }
                    }
                }


                int lot_id = 0;
                string lot_name = string.Empty;
                if (!string.IsNullOrWhiteSpace(productTracking) && (productTracking == "serial" || productTracking == "lot"))
                {
                    var stockLotInfo = await odooAPIService.LotSearchAsync(dataRequest.LotNumber, product_id, company_id, dbConfig.UserID, dbConfig.SessionID);
                    if (stockLotInfo == null)
                    {
                        stockLotInfo = await odooAPIService.CreateLotAsync(dataRequest.LotNumber, product_id, company_id, dbConfig.UserID, dbConfig.SessionID);
                        stockLotInfo = await odooAPIService.LotSearchAsync(dataRequest.LotNumber, product_id, company_id, dbConfig.UserID, dbConfig.SessionID);
                    }

                    if (stockLotInfo != null)
                    {
                        lot_id = (int)stockLotInfo.Last[0];
                        lot_name = (string)stockLotInfo.Last[1];
                    }
                    else
                    {
                        logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Mã lot " + dataRequest.LotNumber + " không tìm thấy để nhập cho lệnh sản xuất: " + productionOrderInfo["name"]);
                        bODataProcessResult.OK = false;
                        bODataProcessResult.Message = "Không tìm thấy hoặc tạo được mã lô: " + dataRequest.LotNumber;
                        return bODataProcessResult;
                    }
                }

                //Để trành không sử dụng lại mã lot đã dùng rồi
                var checkLotInfo = await odooAPIService.CheckUsedLotIDAsync(lot_id, dbConfig.UserID, dbConfig.SessionID);
                if (checkLotInfo != null)
                {
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Mã lô " + dataRequest.LotNumber + " đã được sử dụng cho lệnh sản xuất " + checkLotInfo["name"]);
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Mã lô " + dataRequest.LotNumber + " đã được sử dụng cho lệnh sản xuất " + checkLotInfo["name"];
                    return bODataProcessResult;
                }
                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Mã lot " + dataRequest.LotNumber + " được chuẩn bị để tiêu hao cho cho lệnh sản xuất " + productionOrderInfo["name"]);

                // Thực hiên tiêu hao nghuyên vật liệu theo BOM
                Dictionary<string, object> moveRawConsumeInfo = new Dictionary<string, object>();
                if (productionOrderInfo["product_tracking"] == "serial" || productionOrderInfo["product_tracking"] == "lot")
                {
                    //Xử lý tiêu hao nvl theo mã serial tiêu hao theo qty_produce hoặc lot_producing_id
                    for (int i = 0; i < 4; i++)
                    {
                        moveRawConsumeInfo = await odooAPIService.ConsumeMaterialsByBOMAsyncv2(productionOrderInfo, dbConfig.UserID, dbConfig.SessionID, dataRequest.Quality, lot_id, i);
                        var move_raw_ids = ((JObject)moveRawConsumeInfo["result"])["value"]["move_raw_ids"] as JArray;
                        if (move_raw_ids != null && move_raw_ids.Count > 0)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    moveRawConsumeInfo = await odooAPIService.ConsumeMaterialsByBOMAsyncv1(productionOrderInfo, dbConfig.UserID, dbConfig.SessionID, dataRequest.Quality, lot_id);
                }

                //Thực hiện tính lại nguyên vật liệu trong trường hợp lỗi
                var result = ((JObject)moveRawConsumeInfo["result"])["value"]["move_raw_ids"] as JArray;
                var workOrderResult = ((JObject)moveRawConsumeInfo["result"])["value"]["workorder_ids"] as JArray;

                var moveRawList = new List<object>();
                var workOrderList = new List<object>();

                if (result != null)
                {
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Tiêu hao NVL thành công cho lệnh sản xuất " + productionOrderInfo["name"] + (!string.IsNullOrWhiteSpace(dataRequest.LotNumber) ? " với số seri: " + dataRequest.LotNumber : ""));
                    foreach (var item in result)
                    {
                        var id = (int)item[1];
                        if (id != 0)
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

                    if (workOrderResult != null)
                    {
                        foreach (var item in workOrderResult)
                        {
                            var id = (int)item[1];
                            if (id != 0)
                            {
                                var detail = item[2] as JObject;

                                decimal qty_producing = 0;
                                try
                                {
                                    qty_producing = decimal.Parse(detail?["qty_producing"]?.ToString());
                                }
                                catch
                                {
                                    qty_producing = 0;
                                }

                                decimal duration_expected = 0;
                                try
                                {
                                    duration_expected = decimal.Parse(detail?["duration_expected"]?.ToString());
                                }
                                catch
                                {
                                    duration_expected = 0;
                                }

                                int finished_lot_id = 0;
                                try
                                {
                                    finished_lot_id = int.Parse(detail?["finished_lot_id"][0]?.ToString());
                                }
                                catch
                                {
                                    finished_lot_id = 0;
                                }

                                if (qty_producing != 0)
                                {
                                    var workOrder = new object[]
                                    {
                                        1,
                                        id,
                                        new {
                                            qty_producing = qty_producing,
                                            duration_expected = duration_expected,
                                            finished_lot_id = finished_lot_id
                                        }
                                    };
                                    workOrderList.Add(workOrder);
                                }
                                else
                                {
                                    var workOrder = new object[]
                                    {
                                        4,
                                        id,
                                        false
                                    };
                                    workOrderList.Add(workOrder);
                                }
                            }
                        }
                    }

                    // Danh sách thành phần tiêu hao
                    object[] move_raw_ids = moveRawList.ToArray();
                    object[] work_order_ids = workOrderList.ToArray();

                    int mrp_production_id = int.Parse(productionOrderInfo["id"]);

                    var saveResult = await odooAPIService.SaveProductionOrderAsyncv1(mrp_production_id, lot_id, dataRequest.Quality, move_raw_ids, work_order_ids, dbConfig.UserID, dbConfig.SessionID);
                    if (saveResult == null || saveResult["result"].ToString() != "True")
                    {
                        logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Lỗi không lưu được lệnh sản xuất " + productionOrderInfo["name"] + (!string.IsNullOrWhiteSpace(dataRequest.LotNumber) ? " với số seri: " + dataRequest.LotNumber : ""));
                        bODataProcessResult.OK = false;
                        bODataProcessResult.Message = "Lỗi không lưu được lệnh sản xuất ";
                        return bODataProcessResult;
                    }
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Lưu lệnh sản xuất thành công " + productionOrderInfo["name"] + (!string.IsNullOrWhiteSpace(dataRequest.LotNumber) ? " với số seri: " + dataRequest.LotNumber : ""));

                    var markDoneResult = await odooAPIService.MarkDoneProductionOrderAsync(mrp_production_id, dbConfig.UserID, dbConfig.SessionID);

                    var backOrderOnchangeResult = await odooAPIService.BackOrderOnchange(mrp_production_id, dbConfig.UserID, dbConfig.SessionID);

                    if (!dataRequest.IsLastOrder)
                    {
                        var backorder_id = await odooAPIService.BackOrderCreate(mrp_production_id, dbConfig.UserID, dbConfig.SessionID, lot_id);

                        var backorderResult = await odooAPIService.BackOrderAction(mrp_production_id, backorder_id, dbConfig.UserID, dbConfig.SessionID);
                    }



                    bODataProcessResult.OK = true;
                    bODataProcessResult.Message = "Hoàn thành lệnh sản xuất";
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Hoàn thành lệnh sản xuất " + productionOrderInfo["name"] + (!string.IsNullOrWhiteSpace(dataRequest.LotNumber) ? " với số seri: " + dataRequest.LotNumber : ""));
                }
                else
                {
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Tiêu hao NVL thất bại cho lệnh sản xuất " + productionOrderInfo["name"]);
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Lỗi không tiêu hao được nguyên vật liệu";
                }


            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

                if (!string.IsNullOrWhiteSpace(bODataProcessResult.Message) && bODataProcessResult.Message.Contains("Odoo Session Expired"))
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }

            }
            return bODataProcessResult;
        }

        /// <summary>
        /// Thực hiện nhập kqsx không sử dụng hàm tiêu hao nvl
        /// </summary>
        /// <param name="dataRequest"></param>
        /// <returns></returns>
        private async Task<BODataProcessResult> InputProductionResultToViindooV1(InputProductDataRequest dataRequest)
        {
            LogService logger = new LogService(svnDBConfig.ConnectionString);
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            try
            {
                if (string.IsNullOrWhiteSpace(dbConfig.SessionID) || dbConfig.UserID == 0)
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }

                //Lấy dữ liệu lệnh sản xuất
                var productionOrderInfo = await odooAPIService.ReadProductionByProductIDAsync(dataRequest.WorkOrderNumber, dbConfig.UserID, dbConfig.SessionID);
                if (productionOrderInfo == null)
                {
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Không tìm thấy lệnh sản xuất cho mã seri: " + dataRequest.WorkOrderNumber);
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Không tìm thấy lệnh sản xuất cho mã seri: " + dataRequest.WorkOrderNumber;
                    return bODataProcessResult;
                }
                int remainingQty = int.Parse(productionOrderInfo["product_qty"]);
                if (dataRequest.Quality >= remainingQty)
                {
                    dataRequest.IsLastOrder = true;
                }

                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Bắt đầu thực hiện lệnh sản xuất: " + productionOrderInfo["name"]);

                var str_move_ids = productionOrderInfo["move_raw_ids"].Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Split(",");
                var move_ids = Array.ConvertAll(str_move_ids, int.Parse);

                var productTracking = productionOrderInfo["product_tracking"]?.ToString();

                //Lấy product_id
                var arrProductID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["product_id"]);
                var product_id = Convert.ToInt32(arrProductID[0]);

                //Lấy company_id
                var arrCompanyID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["company_id"]);
                var company_id = Convert.ToInt32(arrCompanyID[0]);

                //Get stock move
                var stockMoveInfo = await odooAPIService.GetStockMoveByIDAsync(move_ids, company_id, dbConfig.UserID, dbConfig.SessionID);

                //Lấy các thành phần được theo dõi theo mã lot hoặc serial
                List<Dictionary<string, object>> stockMoveSerialInfo = new List<Dictionary<string, object>>();
                if (stockMoveInfo != null)
                {
                    foreach (var item in stockMoveInfo)
                    {
                        var token = (JToken)item["has_tracking"];
                        if (token.Type == JTokenType.String && token?.ToString() == "serial" || token.Type == JTokenType.String && token?.ToString() == "lot")
                        {
                            stockMoveSerialInfo.Add(item);
                        }
                    }
                }

                if (stockMoveSerialInfo.Count > 0)
                {
                    //Dữ liệu dùng để thay đổi stock move line có serial theo lệnh sản xuất
                    List<Dictionary<string, string>> stockMoveLineSerials = new List<Dictionary<string, string>>();

                    foreach (var item in stockMoveSerialInfo)
                    {
                        var str_move_line_ids = item["move_line_ids"].ToString().Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Split(",").ToList();
                        //var move_line_ids = Array.ConvertAll(str_move_line_ids, int.Parse);

                        //Lấy product_id
                        var arrMarterialProductID = JsonConvert.DeserializeObject<object[]>(item["product_id"].ToString());
                        var product_material_id = Convert.ToInt32(arrMarterialProductID[0]);

                        var move_id = int.Parse(item["id"].ToString());

                        if (dataRequest.LotScaneds.Count > 0)
                        {
                            var lotScaned = dataRequest.LotScaneds.FirstOrDefault(x => x.product_id == product_material_id);
                            int lot_id_info = 0;
                            if (lotScaned != null)
                            {
                                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Xử lý thành phần có mã serial: " + lotScaned.lotNumber + " và mã nguyên liệu là: " + arrMarterialProductID[1] + " cho lsx: " + productionOrderInfo["name"]);
                                //Bước cần thay đổi theo cách mới để lấy stock move line theo mã lot
                                //B1: Lấy ra stock move line đầu tiên trong danh sách thuộc stock move - checked
                                Dictionary<string, string> firstStockMoveLine = new Dictionary<string, string>();

                                lot_id_info = await odooAPIService.GetLotInfo(lotScaned.lotNumber, Convert.ToInt32(productionOrderInfo["id"]), item, dbConfig.UserID, dbConfig.SessionID);
                                if (lot_id_info == 0)
                                {
                                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Mã lot " + lotScaned.lotNumber + " không tìm thấy cho lsx: " + productionOrderInfo["name"]);
                                    bODataProcessResult.OK = false;
                                    bODataProcessResult.Message = "Mã lot " + lotScaned.lotNumber + " không tìm thấy ";
                                    return bODataProcessResult;
                                }

                                if (str_move_line_ids != null && str_move_line_ids.Count > 0)
                                {
                                    if (str_move_line_ids.Count == 1 && str_move_line_ids[0] == "[]")
                                    {
                                        var createResult = await odooAPIService.CreateLotComponentForMO(lot_id_info, Convert.ToInt32(productionOrderInfo["id"]), item, dbConfig.UserID, dbConfig.SessionID);
                                        if (createResult == null || createResult["result"].ToString() != "True")
                                        {
                                            logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Lỗi không tiêu hao mã lot " + lotScaned.lotNumber + " cho NVL" + item["product_id"] + " cho lsx: " + productionOrderInfo["name"]);
                                            bODataProcessResult.OK = false;
                                            bODataProcessResult.Message = "Lỗi không tạo được stock move line cho mã lot " + lotScaned.lotNumber;
                                            return bODataProcessResult;
                                        }
                                        firstStockMoveLine["is_created_stock_move_line"] = "True";
                                    }
                                    else
                                    {
                                        firstStockMoveLine = await odooAPIService.GetStockMoveLineByID(Convert.ToInt32(str_move_line_ids[0]), Convert.ToInt32(productionOrderInfo["id"]), item, dbConfig.UserID, dbConfig.SessionID);
                                        firstStockMoveLine["is_created_stock_move_line"] = "False";
                                    }

                                }
                                else
                                {
                                    //Nếu Không được dữ phần thì thực hiện gán sô seri cho stock move
                                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Thành phần " + arrMarterialProductID[1].ToString() + " Chưa có số serial nào co lsx: " + productionOrderInfo["name"]);
                                    bODataProcessResult.OK = false;
                                    bODataProcessResult.Message = "Thành phần " + arrMarterialProductID[1].ToString() + " Chưa có số serial nào. ";
                                    return bODataProcessResult;
                                }
                                //var stockMoveLineSerial = await odooAPIService.GetStockMoveLineByLotNameAsync(lotScaned.lotNumber, product_material_id, str_move_line_ids, dbConfig.UserID, dbConfig.SessionID);
                                ////var stockMoveLineSerial = await odooAPIService.GetLotByNameAndProductIDAsync(move_id, productionOrderInfo["name"], lotScaned.lotNumber, product_material_id, dbConfig.UserID, dbConfig.SessionID);
                                //if (stockMoveLineSerial == null)
                                //{
                                //    bODataProcessResult.OK = false;
                                //    bODataProcessResult.Message = "Mã lot " + lotScaned.lotNumber + " không tìm thấy " ;
                                //    return bODataProcessResult;
                                //}

                                var stockMoveLineSerial = firstStockMoveLine;
                                if (stockMoveLineSerial == null || stockMoveLineSerial.Count == 0)
                                {
                                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Mã lot " + lotScaned.lotNumber + " không tìm thấy cho lệnh sx: " + productionOrderInfo["name"]);
                                    bODataProcessResult.OK = false;
                                    bODataProcessResult.Message = "Mã lot " + lotScaned.lotNumber + " không tìm thấy ";
                                    return bODataProcessResult;
                                }
                                stockMoveLineSerial["lot_id"] = lot_id_info.ToString();
                                stockMoveLineSerial["move_line_ids"] = item["move_line_ids"].ToString();
                                stockMoveLineSerial["location_id"] = item["location_id"].ToString();
                                stockMoveLineSerial["location_dest_id"] = item["location_dest_id"].ToString();
                                stockMoveLineSerial["warehouse_id"] = item["warehouse_id"].ToString();
                                stockMoveLineSerial["picking_type_id"] = item["picking_type_id"].ToString();
                                stockMoveLineSerial["company_id"] = item["company_id"].ToString();
                                stockMoveLineSerial["mo_id"] = productionOrderInfo["id"].ToString();
                                //thêm qty cần tiêu hao để lưu vào stockmoveline
                                stockMoveLineSerial["quantity"] = lotScaned.quantity.ToString();
                                stockMoveLineSerials.Add(stockMoveLineSerial);
                            }
                        }
                    }

                    //B3: Lưu stock move lại
                    if (stockMoveLineSerials.Count > 0)
                    {
                        //Thực hiện cập nhật stock move line theo mã lot
                        foreach (var item in stockMoveLineSerials)
                        {
                            if (item.ContainsKey("is_created_stock_move_line") && item["is_created_stock_move_line"] == "True")
                            {
                                //Nếu là mới tạo thì bỏ qua không cần cập nhật nữa
                                continue;
                            }
                            var str_move_line_ids = item["move_line_ids"].ToString().Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Split(",");

                            //Tạo danh sách stock move line để cập nhật
                            var move_line_ids = Array.ConvertAll(str_move_line_ids, int.Parse)
                                .Select(id =>
                                {
                                    int move_line_id = int.Parse(item["id"]);
                                    if (id == move_line_id)
                                    {
                                        //khi cập nhật stockmoveline thì cập nhât qty_done vào
                                        //return new object[] { 1, id, new { lot_id = int.Parse(item["lot_id"]), qty_done = 1 } };
                                        return new object[] { 1, id, new { lot_id = int.Parse(item["lot_id"]), qty_done = int.Parse(item["quantity"]) } };
                                    }
                                    else
                                    {
                                        return new object[] { 4, id, false };
                                    }
                                }).ToArray();
                            var stockMoveWriteResult = await odooAPIService.SaveSerialStockMoveAsync(item, move_line_ids, dbConfig.UserID, dbConfig.SessionID);
                            if (stockMoveWriteResult == null || stockMoveWriteResult["result"].ToString() != "True")
                            {
                                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Mã lot id" + item["lot_id"] + " không tiêu hao thành công cho thành phần product id: " + item["product_id"] + " lsx: " + productionOrderInfo["name"]);
                                bODataProcessResult.OK = false;
                                bODataProcessResult.Message = "Mã lot id" + item["lot_id"] + " không tiêu hao thành công cho thành phần product id: " + item["product_id"];
                                return bODataProcessResult;
                            }
                            logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Mã lot id" + item["lot_id"] + " tiêu hao thành công cho thành phần product id: " + item["product_id"] + " lsx: " + productionOrderInfo["name"]);
                        }
                    }
                }


                int lot_id = 0;
                string lot_name = string.Empty;
                if (!string.IsNullOrWhiteSpace(productTracking) && (productTracking == "serial" || productTracking == "lot"))
                {
                    var stockLotInfo = await odooAPIService.LotSearchAsync(dataRequest.LotNumber, product_id, company_id, dbConfig.UserID, dbConfig.SessionID);
                    if (stockLotInfo == null)
                    {
                        stockLotInfo = await odooAPIService.CreateLotAsync(dataRequest.LotNumber, product_id, company_id, dbConfig.UserID, dbConfig.SessionID);
                        stockLotInfo = await odooAPIService.LotSearchAsync(dataRequest.LotNumber, product_id, company_id, dbConfig.UserID, dbConfig.SessionID);
                    }

                    if (stockLotInfo != null)
                    {
                        lot_id = (int)stockLotInfo.Last[0];
                        lot_name = (string)stockLotInfo.Last[1];
                    }
                    else
                    {
                        logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Mã lot " + dataRequest.LotNumber + " không tìm thấy để nhập cho lệnh sản xuất: " + productionOrderInfo["name"]);
                        bODataProcessResult.OK = false;
                        bODataProcessResult.Message = "Không tìm thấy hoặc tạo được mã lô: " + dataRequest.LotNumber;
                        return bODataProcessResult;
                    }
                }

                //Để trành không sử dụng lại mã lot đã dùng rồi
                var checkLotInfo = await odooAPIService.CheckUsedLotIDAsync(lot_id, dbConfig.UserID, dbConfig.SessionID);
                if (checkLotInfo != null)
                {
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Mã lô " + dataRequest.LotNumber + " đã được sử dụng cho lệnh sản xuất " + checkLotInfo["name"]);
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Mã lô " + dataRequest.LotNumber + " đã được sử dụng cho lệnh sản xuất " + checkLotInfo["name"];
                    return bODataProcessResult;
                }
                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Mã lot " + dataRequest.LotNumber + " được chuẩn bị để tiêu hao cho cho lệnh sản xuất " + productionOrderInfo["name"]);

                // Thực hiên tiêu hao nghuyên vật liệu theo BOM
                Dictionary<string, object> moveRawConsumeInfo = new Dictionary<string, object>();
                if (productionOrderInfo["product_tracking"] == "serial" || productionOrderInfo["product_tracking"] == "lot")
                {
                    //Xử lý tiêu hao nvl theo mã serial tiêu hao theo qty_produce hoặc lot_producing_id
                    for (int i = 0; i < 4; i++)
                    {
                        moveRawConsumeInfo = await odooAPIService.ConsumeMaterialsByBOMAsyncv2(productionOrderInfo, dbConfig.UserID, dbConfig.SessionID, dataRequest.Quality, lot_id, i);
                        var move_raw_ids = ((JObject)moveRawConsumeInfo["result"])["value"]["move_raw_ids"] as JArray;
                        if (move_raw_ids != null && move_raw_ids.Count > 0)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    moveRawConsumeInfo = await odooAPIService.ConsumeMaterialsByBOMAsyncv1(productionOrderInfo, dbConfig.UserID, dbConfig.SessionID, dataRequest.Quality, lot_id);
                }

                //Thực hiện tính lại nguyên vật liệu trong trường hợp lỗi
                var result = ((JObject)moveRawConsumeInfo["result"])["value"]["move_raw_ids"] as JArray;
                var workOrderResult = ((JObject)moveRawConsumeInfo["result"])["value"]["workorder_ids"] as JArray;

                var moveRawList = new List<object>();
                var workOrderList = new List<object>();

                if (result != null)
                {
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Tiêu hao NVL thành công cho lệnh sản xuất " + productionOrderInfo["name"] + (!string.IsNullOrWhiteSpace(dataRequest.LotNumber) ? " với số seri: " + dataRequest.LotNumber : ""));
                    foreach (var item in result)
                    {
                        var id = (int)item[1];
                        if (id != 0)
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

                    if (workOrderResult != null)
                    {
                        foreach (var item in workOrderResult)
                        {
                            var id = (int)item[1];
                            if (id != 0)
                            {
                                var detail = item[2] as JObject;

                                decimal qty_producing = 0;
                                try
                                {
                                    qty_producing = decimal.Parse(detail?["qty_producing"]?.ToString());
                                }
                                catch
                                {
                                    qty_producing = 0;
                                }

                                decimal duration_expected = 0;
                                try
                                {
                                    duration_expected = decimal.Parse(detail?["duration_expected"]?.ToString());
                                }
                                catch
                                {
                                    duration_expected = 0;
                                }

                                int finished_lot_id = 0;
                                try
                                {
                                    finished_lot_id = int.Parse(detail?["finished_lot_id"][0]?.ToString());
                                }
                                catch
                                {
                                    finished_lot_id = 0;
                                }

                                if (qty_producing != 0)
                                {
                                    var workOrder = new object[]
                                    {
                                        1,
                                        id,
                                        new {
                                            qty_producing = qty_producing,
                                            duration_expected = duration_expected,
                                            finished_lot_id = finished_lot_id
                                        }
                                    };
                                    workOrderList.Add(workOrder);
                                }
                                else
                                {
                                    var workOrder = new object[]
                                    {
                                        4,
                                        id,
                                        false
                                    };
                                    workOrderList.Add(workOrder);
                                }
                            }
                        }
                    }

                    // Danh sách thành phần tiêu hao
                    object[] move_raw_ids = moveRawList.ToArray();
                    object[] work_order_ids = workOrderList.ToArray();

                    int mrp_production_id = int.Parse(productionOrderInfo["id"]);

                    var saveResult = await odooAPIService.SaveProductionOrderAsyncv1(mrp_production_id, lot_id, dataRequest.Quality, move_raw_ids, work_order_ids, dbConfig.UserID, dbConfig.SessionID);
                    if (saveResult == null || saveResult["result"].ToString() != "True")
                    {
                        logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Lỗi không lưu được lệnh sản xuất " + productionOrderInfo["name"] + (!string.IsNullOrWhiteSpace(dataRequest.LotNumber) ? " với số seri: " + dataRequest.LotNumber : ""));
                        bODataProcessResult.OK = false;
                        bODataProcessResult.Message = "Lỗi không lưu được lệnh sản xuất ";
                        return bODataProcessResult;
                    }
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Lưu lệnh sản xuất thành công " + productionOrderInfo["name"] + (!string.IsNullOrWhiteSpace(dataRequest.LotNumber) ? " với số seri: " + dataRequest.LotNumber : ""));

                    var markDoneResult = await odooAPIService.MarkDoneProductionOrderAsync(mrp_production_id, dbConfig.UserID, dbConfig.SessionID);

                    var backOrderOnchangeResult = await odooAPIService.BackOrderOnchange(mrp_production_id, dbConfig.UserID, dbConfig.SessionID);

                    if (!dataRequest.IsLastOrder)
                    {
                        var backorder_id = await odooAPIService.BackOrderCreate(mrp_production_id, dbConfig.UserID, dbConfig.SessionID, lot_id);

                        var backorderResult = await odooAPIService.BackOrderAction(mrp_production_id, backorder_id, dbConfig.UserID, dbConfig.SessionID);
                    }



                    bODataProcessResult.OK = true;
                    bODataProcessResult.Message = "Hoàn thành lệnh sản xuất";
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Hoàn thành lệnh sản xuất " + productionOrderInfo["name"] + (!string.IsNullOrWhiteSpace(dataRequest.LotNumber) ? " với số seri: " + dataRequest.LotNumber : ""));
                }
                else
                {
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Tiêu hao NVL thất bại cho lệnh sản xuất " + productionOrderInfo["name"]);
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Lỗi không tiêu hao được nguyên vật liệu";
                }


            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

                if (!string.IsNullOrWhiteSpace(bODataProcessResult.Message) && bODataProcessResult.Message.Contains("Odoo Session Expired"))
                {
                    bODataProcessResult = await odooAPIService.LoginAsync();
                    if (!bODataProcessResult.OK)
                    {
                        return bODataProcessResult;
                    }
                    dbConfig.SessionID = bODataProcessResult.DataType;
                    dbConfig.UserID = bODataProcessResult.UserID;
                }

            }
            return bODataProcessResult;
        }
        #endregion

        #region private methods
        private List<mrp_productionUI> ConvertDynamicToUI(List<dynamic> dynamicList)
        {
            try
            {
                if (dynamicList == null) return new List<mrp_productionUI>();

                var result = new List<mrp_productionUI>(dynamicList.Count);

                foreach (Newtonsoft.Json.Linq.JObject item in dynamicList)
                {
                    var uiItem = new mrp_productionUI
                    {
                        id = (int)(item["id"] ?? 0),
                        message_main_attachment_id = (int)(item["message_main_attachment_id"] ?? 0),
                        backorder_sequence = (int)(item["backorder_sequence"] ?? 0),

                        // Các trường quan hệ Many2one hoặc false
                        product_id = ParseOdooField(item["product_id"]),
                        product_uom_id = ParseOdooField(item["product_uom_id"]),
                        lot_producing_id = ParseOdooField(item["lot_producing_id"]),
                        bom_id = ParseOdooField(item["bom_id"]),

                        picking_type_id = ParseOdooField(item["picking_type_id"]) ?? 0,
                        location_src_id = ParseOdooField(item["location_src_id"]) ?? 0,
                        location_dest_id = ParseOdooField(item["location_dest_id"]) ?? 0,
                        production_location_id = ParseOdooField(item["production_location_id"]) ?? 0,
                        company_id = ParseOdooField(item["company_id"]) ?? 0,

                        user_id = (int)(item["user_id"] ?? 0),
                        procurement_group_id = (int)(item["procurement_group_id"] ?? 0),
                        orderpoint_id = (int)(item["orderpoint_id"] ?? 0),
                        create_uid = (int)(item["create_uid"] ?? 0),
                        write_uid = (int)(item["write_uid"] ?? 0),

                        origin_message_id = (string)item["origin_message_id"],
                        origin_references = (string)item["origin_references"],
                        name = (string)item["name"],
                        priority = (string)item["priority"],
                        origin = (string)item["origin"],
                        state = (string)item["state"],
                        reservation_state = (string)item["reservation_state"],
                        product_description_variants = (string)item["product_description_variants"],
                        consumption = (string)item["consumption"],

                        product_qty = (decimal)(item["product_qty"] ?? 0m),
                        qty_producing = (decimal)(item["qty_producing"] ?? 0m),
                        product_uom_qty = (decimal)(item["product_uom_qty"] ?? 0m),
                        extra_cost = (decimal)(item["extra_cost"] ?? 0m),

                        propagate_cancel = (bool)(item["propagate_cancel"] ?? false),
                        is_locked = (bool)(item["is_locked"] ?? false),
                        is_planned = (bool)(item["is_planned"] ?? false),
                        allow_workorder_dependencies = (bool)(item["allow_workorder_dependencies"] ?? false),

                        // --- CẬP NHẬT: XỬ LÝ DATE/DATETIME CÓ THỂ BỊ FALSE ---
                        date_planned_start = ParseOdooDateTime(item["date_planned_start"]),
                        date_planned_finished = ParseOdooDateTime(item["date_planned_finished"]),
                        date_deadline = ParseOdooDateTime(item["date_deadline"]),
                        date_start = ParseOdooDateTime(item["date_start"]),
                        date_finished = ParseOdooDateTime(item["date_finished"]),
                        create_date = ParseOdooDateTime(item["create_date"]),
                        write_date = ParseOdooDateTime(item["write_date"]),
                        // ----------------------------------------------------

                        analytic_account_id = (int)(item["analytic_account_id"] ?? 0),
                        x_Svn_customer_SN = (string)item["x_Svn_customer_SN"],
                        finished_move_line_ids = item["finished_move_line_ids"]
                    };

                    result.Add(uiItem);
                }

                return result;
            }
            catch (Exception ex)
            {
                // Log lỗi nếu cần
                return null;
            }
        }

        // Helper xử lý ID (Many2one / Int)
        private static int? ParseOdooField(Newtonsoft.Json.Linq.JToken token)
        {
            if (token == null || token.Type == Newtonsoft.Json.Linq.JTokenType.Boolean)
                return null;

            if (token.Type == Newtonsoft.Json.Linq.JTokenType.Array)
                return (int?)token[0];

            return (int?)token;
        }

        // Helper mới xử lý Date/DateTime: Tránh lỗi khi Odoo trả về false
        private static DateTime? ParseOdooDateTime(Newtonsoft.Json.Linq.JToken token)
        {
            if (token == null || token.Type == Newtonsoft.Json.Linq.JTokenType.Boolean)
                return null; // Nếu rỗng Odoo trả về false -> map thành null trong C#

            return (DateTime?)token;
        }
        #endregion
    }
}
