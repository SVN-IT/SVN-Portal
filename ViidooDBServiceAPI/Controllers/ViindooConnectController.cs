using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SVNShareLib;
using SVNShareLib.Request;
using SVNShareLib.Utils;
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

        public ViindooConnectController(ViindooDBConfig dbConfig, OdooAPIService odooAPIService, SVNDBConfig svnDBConfig)
        {
            this.dbConfig = dbConfig;
            this.odooAPIService = odooAPIService;
            this.svnDBConfig = svnDBConfig;
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

        [Route("GetWorkOrder")]
        [HttpPost]
        public async Task<BODataProcessResult> GetWorkOrder(InputProductDataRequest dataRequest)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                processResult = await odooAPIService.LoginAsync();
                if (!processResult.OK)
                {
                    return processResult;
                }
                var productionOrderInfo = await odooAPIService.ReadProductionByProductIDAsync(dataRequest.WorkOrderNumber, processResult.UserID, processResult.DataType);
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
                var stockMoveInfo = await odooAPIService.GetStockMoveByIDAsync(move_ids, company_id, processResult.UserID, processResult.DataType);
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
                bODataProcessResult = await odooAPIService.LoginAsync();
                if (bODataProcessResult.OK)
                {
                    //Lấy dữ liệu lệnh sản xuất
                    var productionOrderInfo = await odooAPIService.ReadProductionByProductIDAsync(dataRequest.seriNumber, bODataProcessResult.UserID, bODataProcessResult.DataType);

                    // Thực hiên tiêu hao nghuyên vật liệu theo BOM
                    var moveRawConsumeInfo = await odooAPIService.ConsumeMaterialsByBOMAsync(productionOrderInfo, bODataProcessResult.UserID, bODataProcessResult.DataType, dataRequest.count);

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

                        var saveResult = await odooAPIService.SaveProductionOrderAsync(mrp_production_id, dataRequest.count, move_raw_ids, bODataProcessResult.UserID, bODataProcessResult.DataType);

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

        [Route("InputProductionByWorkOrderv1")]
        [HttpPost]
        public async Task<BODataProcessResult> InputProductionByWorkOrderv1(InputProductDataRequest dataRequest)
        {
            LogService logger = new LogService(svnDBConfig.ConnectionString);
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            try
            {
                bODataProcessResult = await odooAPIService.LoginAsync();
                if (bODataProcessResult.OK)
                {
                    //Lấy dữ liệu lệnh sản xuất
                    var productionOrderInfo = await odooAPIService.ReadProductionByProductIDAsync(dataRequest.WorkOrderNumber, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                    var stockMoveInfo = await odooAPIService.GetStockMoveByIDAsync(move_ids, company_id, bODataProcessResult.UserID, bODataProcessResult.DataType);

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
                                        firstStockMoveLine = await odooAPIService.GetStockMoveLineByID(Convert.ToInt32(str_move_line_ids[0]), Convert.ToInt32(productionOrderInfo["id"]), item, bODataProcessResult.UserID, bODataProcessResult.DataType);

                                        //B2: Tìm kiếm lot id theo lot name - checked
                                        lot_id_info = await odooAPIService.GetLotInfo(lotScaned.lotNumber, Convert.ToInt32(productionOrderInfo["id"]), item, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                                    //var stockMoveLineSerial = await odooAPIService.GetStockMoveLineByLotNameAsync(lotScaned.lotNumber, product_material_id, str_move_line_ids, bODataProcessResult.UserID, bODataProcessResult.DataType);
                                    ////var stockMoveLineSerial = await odooAPIService.GetLotByNameAndProductIDAsync(move_id, productionOrderInfo["name"], lotScaned.lotNumber, product_material_id, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                                var stockMoveWriteResult = await odooAPIService.SaveSerialStockMoveAsync(item, move_line_ids, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                        var stockLotInfo = await odooAPIService.LotSearchAsync(dataRequest.LotNumber, product_id, company_id, bODataProcessResult.UserID, bODataProcessResult.DataType);
                        if (stockLotInfo == null)
                        {
                            stockLotInfo = await odooAPIService.CreateLotAsync(dataRequest.LotNumber, product_id, company_id, bODataProcessResult.UserID, bODataProcessResult.DataType);
                            stockLotInfo = await odooAPIService.LotSearchAsync(dataRequest.LotNumber, product_id, company_id, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                    var checkLotInfo = await odooAPIService.CheckUsedLotIDAsync(lot_id, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                        moveRawConsumeInfo = await odooAPIService.ConsumeMaterialsByBOMAsyncv1(productionOrderInfo, bODataProcessResult.UserID, bODataProcessResult.DataType, dataRequest.Quality, lot_id);
                    }
                    else
                    {
                        //Xử lý tiêu hao nvl theo mã serial tiêu hao theo qty_produce hoặc lot_producing_id
                        for (int i = 0; i < 4; i++)
                        {
                            moveRawConsumeInfo = await odooAPIService.ConsumeMaterialsByBOMAsyncv2(productionOrderInfo, bODataProcessResult.UserID, bODataProcessResult.DataType, dataRequest.Quality, lot_id, i);
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

                        var saveResult = await odooAPIService.SaveProductionOrderAsyncv1(mrp_production_id, lot_id, dataRequest.Quality, move_raw_ids, work_order_ids, bODataProcessResult.UserID, bODataProcessResult.DataType);
                        if (saveResult == null || saveResult["result"].ToString() != "True")
                        {
                            logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Lỗi không lưu được lệnh sản xuất " + productionOrderInfo["name"] + (!string.IsNullOrWhiteSpace(dataRequest.LotNumber) ? " với số seri: " + dataRequest.LotNumber : ""));
                            bODataProcessResult.OK = false;
                            bODataProcessResult.Message = "Lỗi không lưu được lệnh sản xuất ";
                            return bODataProcessResult;
                        }
                        logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Lưu lệnh sản xuất thành công " + productionOrderInfo["name"] + (!string.IsNullOrWhiteSpace(dataRequest.LotNumber) ? " với số seri: " + dataRequest.LotNumber : ""));

                        var markDoneResult = await odooAPIService.MarkDoneProductionOrderAsync(mrp_production_id, bODataProcessResult.UserID, bODataProcessResult.DataType);

                        var backOrderOnchangeResult = await odooAPIService.BackOrderOnchange(mrp_production_id, bODataProcessResult.UserID, bODataProcessResult.DataType);

                        if (!dataRequest.IsLastOrder)
                        {
                            var backorder_id = await odooAPIService.BackOrderCreate(mrp_production_id, bODataProcessResult.UserID, bODataProcessResult.DataType, lot_id);

                            var backorderResult = await odooAPIService.BackOrderAction(mrp_production_id, backorder_id, bODataProcessResult.UserID, bODataProcessResult.DataType);
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


            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

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
            LogService logger = new LogService(svnDBConfig.ConnectionString);
            BODataProcessResult bODataProcessResult = new BODataProcessResult();
            try
            {
                bODataProcessResult = await odooAPIService.LoginAsync();
                if (bODataProcessResult.OK)
                {
                    //Lấy dữ liệu lệnh sản xuất
                    var productionOrderInfo = await odooAPIService.ReadProductionByProductIDAsync(dataRequest.WorkOrderNumber, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                    var stockMoveInfo = await odooAPIService.GetStockMoveByIDAsync(move_ids, company_id, bODataProcessResult.UserID, bODataProcessResult.DataType);

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

                                    lot_id_info = await odooAPIService.GetLotInfo(lotScaned.lotNumber, Convert.ToInt32(productionOrderInfo["id"]), item, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                                            var createResult = await odooAPIService.CreateLotComponentForMO(lot_id_info, Convert.ToInt32(productionOrderInfo["id"]), item, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                                            firstStockMoveLine = await odooAPIService.GetStockMoveLineByID(Convert.ToInt32(str_move_line_ids[0]), Convert.ToInt32(productionOrderInfo["id"]), item, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                                    //var stockMoveLineSerial = await odooAPIService.GetStockMoveLineByLotNameAsync(lotScaned.lotNumber, product_material_id, str_move_line_ids, bODataProcessResult.UserID, bODataProcessResult.DataType);
                                    ////var stockMoveLineSerial = await odooAPIService.GetLotByNameAndProductIDAsync(move_id, productionOrderInfo["name"], lotScaned.lotNumber, product_material_id, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                                var stockMoveWriteResult = await odooAPIService.SaveSerialStockMoveAsync(item, move_line_ids, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                        var stockLotInfo = await odooAPIService.LotSearchAsync(dataRequest.LotNumber, product_id, company_id, bODataProcessResult.UserID, bODataProcessResult.DataType);
                        if (stockLotInfo == null)
                        {
                            stockLotInfo = await odooAPIService.CreateLotAsync(dataRequest.LotNumber, product_id, company_id, bODataProcessResult.UserID, bODataProcessResult.DataType);
                            stockLotInfo = await odooAPIService.LotSearchAsync(dataRequest.LotNumber, product_id, company_id, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                    var checkLotInfo = await odooAPIService.CheckUsedLotIDAsync(lot_id, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                            moveRawConsumeInfo = await odooAPIService.ConsumeMaterialsByBOMAsyncv2(productionOrderInfo, bODataProcessResult.UserID, bODataProcessResult.DataType, dataRequest.Quality, lot_id, i);
                            var move_raw_ids = ((JObject)moveRawConsumeInfo["result"])["value"]["move_raw_ids"] as JArray;
                            if (move_raw_ids != null && move_raw_ids.Count > 0)
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        moveRawConsumeInfo = await odooAPIService.ConsumeMaterialsByBOMAsyncv1(productionOrderInfo, bODataProcessResult.UserID, bODataProcessResult.DataType, dataRequest.Quality, lot_id);
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

                        var saveResult = await odooAPIService.SaveProductionOrderAsyncv1(mrp_production_id, lot_id, dataRequest.Quality, move_raw_ids, work_order_ids, bODataProcessResult.UserID, bODataProcessResult.DataType);
                        if (saveResult == null || saveResult["result"].ToString() != "True")
                        {
                            logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, "Lỗi không lưu được lệnh sản xuất " + productionOrderInfo["name"] + (!string.IsNullOrWhiteSpace(dataRequest.LotNumber) ? " với số seri: " + dataRequest.LotNumber : ""));
                            bODataProcessResult.OK = false;
                            bODataProcessResult.Message = "Lỗi không lưu được lệnh sản xuất ";
                            return bODataProcessResult;
                        }
                        logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, "Lưu lệnh sản xuất thành công " + productionOrderInfo["name"] + (!string.IsNullOrWhiteSpace(dataRequest.LotNumber) ? " với số seri: " + dataRequest.LotNumber : ""));

                        var markDoneResult = await odooAPIService.MarkDoneProductionOrderAsync(mrp_production_id, bODataProcessResult.UserID, bODataProcessResult.DataType);

                        var backOrderOnchangeResult = await odooAPIService.BackOrderOnchange(mrp_production_id, bODataProcessResult.UserID, bODataProcessResult.DataType);

                        if (!dataRequest.IsLastOrder)
                        {
                            var backorder_id = await odooAPIService.BackOrderCreate(mrp_production_id, bODataProcessResult.UserID, bODataProcessResult.DataType, lot_id);

                            var backorderResult = await odooAPIService.BackOrderAction(mrp_production_id, backorder_id, bODataProcessResult.UserID, bODataProcessResult.DataType);
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


            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;

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
                bODataProcessResult = await odooAPIService.LoginAsync();
                if (bODataProcessResult.OK)
                {
                    //Kiểm tra mã lot thành phẩm có tồn tại không
                    var stockLotInfo = await odooAPIService.LotSearchAsync(dataRequest.lotNumber, dataRequest.product_id, 1, bODataProcessResult.UserID, bODataProcessResult.DataType);
                    //if (stockLotInfo == null)
                    //{
                    //    bODataProcessResult.OK = false;
                    //    bODataProcessResult.Message = "Không tìm thấy mã lot: " + dataRequest.lotNumber;
                    //    return bODataProcessResult;
                    //}
                    if (stockLotInfo == null)
                    {
                        stockLotInfo = await odooAPIService.CreateLotAsync(dataRequest.lotNumber, dataRequest.product_id, 1, bODataProcessResult.UserID, bODataProcessResult.DataType);
                        stockLotInfo = await odooAPIService.LotSearchAsync(dataRequest.lotNumber, dataRequest.product_id, 1, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                    var checkLotInfo = await odooAPIService.CheckUsedLotIDAsync(lot_id, bODataProcessResult.UserID, bODataProcessResult.DataType);
                    if (checkLotInfo != null)
                    {
                        bODataProcessResult.OK = false;
                        bODataProcessResult.Message = "Mã lô " + dataRequest.lotNumber + " đã được sử dụng cho lệnh sản xuất " + checkLotInfo["name"];
                        return bODataProcessResult;
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
                bODataProcessResult = await odooAPIService.LoginAsync();
                if (bODataProcessResult.OK)
                {
                    var result = await odooAPIService.GetUsedLotForComponemtAsync(dataRequest.seriNumber, dataRequest.lotNumber, dataRequest.product_id, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;
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
                bODataProcessResult = await odooAPIService.LoginAsync();
                if (bODataProcessResult.OK)
                {
                    var result = await odooAPIService.GetProductItemByCode(code, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                else
                {
                    bODataProcessResult.OK = false;
                    bODataProcessResult.Message = "Đăng nhập thất bại";
                }
            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;
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
                bODataProcessResult = await odooAPIService.LoginAsync();
                if (bODataProcessResult.OK)
                {
                    var result = await odooAPIService.SearchProductTemplate(dataRequest.ItemCode, bODataProcessResult.UserID, bODataProcessResult.DataType);
                    if (result != 0)
                    {
                        var updateResult = await odooAPIService.WriteProductTemplate(result, dataRequest.ItemCode, dataRequest.ItemName, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                        var createResult = await odooAPIService.CreateProductTemplate(dataRequest.ItemCode, dataRequest.ItemName, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
            }
            catch (Exception ex)
            {
                bODataProcessResult.OK = false;
                bODataProcessResult.Message = ex.Message;
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
                bODataProcessResult = await odooAPIService.LoginAsync();
                if (bODataProcessResult.OK)
                {
                    var productResult = await odooAPIService.SearhProductItem(dataRequest.ItemCode, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                        var productTemp = await odooAPIService.SearhProductTemp(productTempItemID, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                        foreach(var item in dataRequest.Items)
                        {
                            var productSubItemResult = await odooAPIService.SearhProductItem(item.ItemCode, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
                                var productTemp = await odooAPIService.SearhProductTemp(productTempItemID, bODataProcessResult.UserID, bODataProcessResult.DataType);
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

                    var result = await odooAPIService.InsertBOM(dataRequest, bODataProcessResult.UserID, bODataProcessResult.DataType);
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
