using CookComputing.XmlRpc;
using Dapper;
using Microsoft.OpenApi.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SVNShareLib;
using SVNShareLib.BaseObject;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
using SVNShareLib.Request;
using System.Data.SqlTypes;
using System.Security.AccessControl;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace ViidooDBServiceAPI.Services
{
    public interface IViindooCommon : IXmlRpcProxy
    {
        [XmlRpcMethod("authenticate")]
        int Authenticate(string db, string user, string password, XmlRpcStruct context);

        [XmlRpcMethod("version")]
        XmlRpcStruct Version();
    }

    public interface IViindooObject : IXmlRpcProxy
    {
        [XmlRpcMethod("execute_kw")]
        object Execute_Kw(string db, int uid, string password, string model, string method, object[] args);
    }

    public class ViindooDataService
    {
        SVNDBConfig SVNDBConfig;
        ViindooDBConfig dBConfig;
        ConvertDataService convertDataService;
        private static string serverUrl;
        private static string dbName;
        private static string username;
        private static string password;
        private static string tableName;
        public ViindooDataService(ViindooDBConfig dBConfig, SVNDBConfig sVNDBConfig, ConvertDataService convertDataService)
        {
            this.dBConfig = dBConfig;
            serverUrl = dBConfig.ServerUrl;
            dbName = dBConfig.DbName;
            username = dBConfig.Username;
            password = dBConfig.Password;
            SVNDBConfig = sVNDBConfig;
            this.convertDataService = convertDataService;
        }

        private BODataProcessResult ConnectDB()
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                // 1. Authentication
                IOdooCommon common = XmlRpcProxyGen.Create<IOdooCommon>();
                common.Timeout = 60000;
                common.Url = serverUrl + "/xmlrpc/2/common";

                XmlRpcStruct context = new XmlRpcStruct(); // You might need to add values here in some cases
                int userId = common.Authenticate(dbName, username, password, context);

                if (userId == 0)
                {
                    processResult.Message = "Authentication failed.";
                }
                else
                {
                    processResult.OK = true;
                    processResult.UserID = userId;
                    processResult.Message = "Authentication success.";
                }
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        public BODataProcessResult InputResult(int product_id)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            IOdooObject models = XmlRpcProxyGen.Create<IOdooObject>();
            models.Timeout = 60000;
            models.Url = serverUrl + "/xmlrpc/2/object";
            try
            {
                object[] domain = new object[] { "product_id", "=", product_id };
                object[] search = new object[] { domain };
                string objectName = "mrp.production";
                string fields = "id,name,origin";
                int limit = 1;
                string order = "write_date desc";
                processResult = GetViindooDataByConditionV1(objectName, search, fields, limit, order);
                if(processResult.OK)
                {
                    var productionData = convertDataService.ConverterToProductionUI(processResult.Content);
                }    

            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        private BODataProcessResult SwitchFunctionToInsert(object searchResult, string objectName)
        {
            //Switch function to insert data
            BODataProcessResult processResult = new BODataProcessResult();
            BODataProcessResult insertResult = new BODataProcessResult();
            try
            {

                switch (objectName)
                {
                    //case "stock.move.line.consume.rel":
                    //    var dataUI = convertDataService.ConverterToStockMoveLineConsumUI(searchResult);
                    //    if (dataUI != null && dataUI.Count > 0)
                    //    {
                    //        //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                    //        insertResult = convertDataService.InsertStockMoveLineConsumToSVNDB(dataUI);
                    //        processResult = insertResult;
                    //    }
                    //    break;
                    //case "stock.move.line":
                    //    var dataUI1 = convertDataService.ConverterToStockMoveLineUI(searchResult);
                    //    if (dataUI1 != null && dataUI1.Count > 0)
                    //    {
                    //        //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                    //        insertResult = convertDataService.InsertStockMoveLineToSVNDB(dataUI1);
                    //        processResult = insertResult;
                    //    }
                    //    break;
                    case "stock.move":
                        var dataUI2 = convertDataService.ConverterToStockMoveUI(searchResult);
                        if (dataUI2 != null && dataUI2.Count > 0)
                        {
                            //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                            insertResult = convertDataService.InsertStockMoveToSVNDB(dataUI2);
                            processResult = insertResult;
                        }
                        break;
                    case "mrp.production": //done
                        //var dataUI3 = convertDataService.ConverterToProductionUI(searchResult);
                        //if (dataUI3 != null && dataUI3.Count > 0)
                        //{
                        //    //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                        //    insertResult = convertDataService.InsertProductionResultToSVNDB(dataUI3);
                        //    processResult = insertResult;
                        //}
                        processResult = GetDataAndUpdateProductionMoveLine(searchResult);
                        break;
                    case "product.template": //done
                        var dataUI4 = convertDataService.ConvertToTemplateUI(searchResult);
                        if (dataUI4 != null && dataUI4.Count > 0)
                        {
                            //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                            insertResult = convertDataService.InsertProductionTemplateToSVNDB(dataUI4);
                            processResult = insertResult;
                        }
                        break;
                    case "mrp.bom": //done
                        var dataUI5 = convertDataService.ConverterToBomUI(searchResult);
                        if (dataUI5 != null && dataUI5.Count > 0)
                        {
                            //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                            insertResult = convertDataService.InsertBOMToSVNDB(dataUI5);
                            processResult = insertResult;
                        }
                        break;
                    case "mrp.bom.line": //done
                        var dataUI6 = convertDataService.ConverterToBomLineUI(searchResult);
                        if (dataUI6 != null && dataUI6.Count > 0)
                        {
                            //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                            insertResult = convertDataService.InsertBomLineToSVNDB(dataUI6);
                            processResult = insertResult;
                        }
                        break;
                    case "stock.lot": //done
                        var dataUI7 = convertDataService.ConverterToStockLotUI(searchResult);
                        if (dataUI7 != null && dataUI7.Count > 0)
                        {
                            //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                            insertResult = convertDataService.InsertStockLotToSVNDB(dataUI7);
                            processResult = insertResult;
                        }
                        break;
                    case "product.category":
                        var dataUI8 = convertDataService.ConverterToProductCatUI(searchResult);
                        if (dataUI8 != null && dataUI8.Count > 0)
                        {
                            //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                            insertResult = convertDataService.InsertProductCatToSVNDB(dataUI8);
                            processResult = insertResult;
                        }
                        break;
                    case "product.product":
                        var dataUI9 = convertDataService.ConverterToProductProductUI(searchResult);
                        if (dataUI9 != null && dataUI9.Count > 0)
                        {
                            //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                            insertResult = convertDataService.InsertProductProductToSVNDB(dataUI9);
                            processResult = insertResult;
                        }
                        break;
                    case "viin.quality.check":
                        var dataUI10 = convertDataService.ConverterToQuantityCheckUI(searchResult);
                        if (dataUI10 != null && dataUI10.Count > 0)
                        {
                            //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                            insertResult = convertDataService.InsertQuantityCheckToSVNDB(dataUI10);
                            processResult = insertResult;
                        }
                        break;
                    case "viin.quality.alert.team":
                        var dataUI11 = convertDataService.ConverterToQuantityCheckUI(searchResult);
                        if (dataUI11 != null && dataUI11.Count > 0)
                        {
                            //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                            insertResult = convertDataService.InsertQuantityCheckToSVNDB(dataUI11);
                            processResult = insertResult;
                        }
                        break;
                    case "quality.reason":
                        var dataUI12 = convertDataService.ConverterToQuantityCheckUI(searchResult);
                        if (dataUI12 != null && dataUI12.Count > 0)
                        {
                            //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                            insertResult = convertDataService.InsertQuantityCheckToSVNDB(dataUI12);
                            processResult = insertResult;
                        }
                        break;
                    case "stock.quant.package":
                        var dataUI13 = convertDataService.ConverterQuantPackageUI(searchResult);
                        if (dataUI13 != null && dataUI13.Count > 0)
                        {
                            //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                            insertResult = convertDataService.InsertStockQuantPackageToSVNDB(dataUI13);
                            processResult = insertResult;
                        }
                        break;
                    case "mrp.unbuild":
                        var dataUI14 = convertDataService.ConverterToMrpUnbuildUI(searchResult);
                        if (dataUI14 != null && dataUI14.Count > 0)
                        {
                            //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                            insertResult = convertDataService.InsertUnbuildToSVNDB(dataUI14);
                            processResult = insertResult;
                        }
                        break;
                }
            }
            catch(Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        #region Get dữ liệu các bảng mrp.production, stock.move.line, stock.move.line.consume.rel
        private BODataProcessResult GetViindooDataByCondition(string objectName, object[] domain)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            var item = dBConfig.QueryConfig.FirstOrDefault(x => x.TableName == objectName);
            if (item != null)
            {
                processResult.DataType = item.TableName;
                try
                {
                    var connectResult = ConnectDB();
                    if (connectResult.OK)
                    {
                        IOdooObject models = XmlRpcProxyGen.Create<IOdooObject>();
                        models.Timeout = 60000;
                        models.Url = serverUrl + "/xmlrpc/2/object";


                        object[] search = new object[] { };
                        string[] fields = new string[] { };
                        
                        search = new object[] { domain };

                        if (!string.IsNullOrWhiteSpace(item.Fields))
                        {
                            fields = item.Fields.Split(",");
                        }

                        var querydata = new object[]
                        {
                            search,
                            fields,
                            0,
                            item.Limit
                        };
                        if (!string.IsNullOrWhiteSpace(item.Order))
                        {
                            querydata = new object[]
                            {
                                search,
                                fields,
                                0,
                                item.Limit,
                                item.Order
                            };
                        }
                        //new object[] { new object[] { "state", "=", "done" } }
                        //new string[] { "name", "product_id", "state" }

                        object searchResult = models.Execute_Kw(
                            dbName,
                            connectResult.UserID,
                            password,
                            item.TableName,
                            "search_read",
                            querydata);
                        if (searchResult != null)
                        {
                            processResult.OK = true;
                            processResult.Content = searchResult;
                        }
                        else
                        {
                            processResult.Message = "Get data fail";
                        }
                    }
                    else
                    {
                        processResult.Message = connectResult.Message;
                    }

                }
                catch (Exception ex)
                {
                    processResult.Message = ex.Message;
                }
            }
            else
            {
                processResult.Message = "Table name not found";
            }
            return processResult;
        }

        private BODataProcessResult GetViindooDataByConditionV1(string objectName, object[] search, string strfields, int limit, string order, string method = "search_read")
        {
            BODataProcessResult processResult = new BODataProcessResult();
            processResult.DataType = objectName;
            try
            {
                var connectResult = ConnectDB();
                if (connectResult.OK)
                {
                    IOdooObject models = XmlRpcProxyGen.Create<IOdooObject>();
                    models.Timeout = 60000;
                    models.Url = serverUrl + "/xmlrpc/2/object";


                    //object[] search = new object[] { };
                    string[] fields = new string[] { };

                    //search = new object[] { domain };

                    if (!string.IsNullOrWhiteSpace(strfields))
                    {
                        fields = strfields.Split(",");
                    }

                    var querydata = new object[]
                    {
                            search,
                            fields,
                            0,
                            limit
                    };
                    if (!string.IsNullOrWhiteSpace(order))
                    {
                        querydata = new object[]
                        {
                                search,
                                fields,
                                0,
                                limit,
                                order
                        };
                    }
                    //new object[] { new object[] { "state", "=", "done" } }
                    //new string[] { "name", "product_id", "state" }

                    object searchResult = models.Execute_Kw(
                        dbName,
                        connectResult.UserID,
                        password,
                        objectName,
                        method,
                        querydata);
                    if (searchResult != null)
                    {
                        processResult.OK = true;
                        processResult.Content = searchResult;
                    }
                    else
                    {
                        processResult.Message = "Get data fail";
                    }
                }
                else
                {
                    processResult.Message = connectResult.Message;
                }

            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }


        /// <summary>
        /// Get dữ liệu từ viindoo và cập nhật vào bảng stock_move_line, stock_move_line_consume_rel trong SVNDB
        /// Lấy theo finished_move_line_ids của bảng mrp.production
        /// </summary>
        /// <param name="searchResult"></param>
        /// <returns></returns>
        private BODataProcessResult GetDataAndUpdateProductionMoveLine(object searchResult)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                BODataProcessResult insertResult = new BODataProcessResult();
                List<stock_move_lineUI> stock_Move_LineUIs = new List<stock_move_lineUI>();
                List<stock_move_line_consume_relUI> consume_RelUIs = new List<stock_move_line_consume_relUI>();
                object[] domain = new object[] {  };
                if (searchResult != null) 
                {
                    var productiongDataUI = convertDataService.ConverterToProductionUI(searchResult);
                    if (productiongDataUI != null) 
                    {
                        insertResult = convertDataService.InsertProductionResultToSVNDB(productiongDataUI);
                        foreach (var item in productiongDataUI)
                        {
                            if (item.finished_move_line_ids != null)
                            {
                                JArray jArray = JArray.FromObject(item.finished_move_line_ids);
                                var json = JsonConvert.SerializeObject(jArray);
                                int[] finished_move_line_ids = JsonConvert.DeserializeObject<int[]>(json);
                                domain = new object[] { "id", "in", finished_move_line_ids };
                                BODataProcessResult processResultStockMoveLine = new BODataProcessResult();
                                processResultStockMoveLine = GetViindooDataByCondition("stock.move.line", domain);
                                if (processResultStockMoveLine.OK)
                                {
                                    var stockMoveLineUI = convertDataService.ConverterToStockMoveLineUI(processResultStockMoveLine.Content);
                                    if (stockMoveLineUI != null)
                                    {
                                        insertResult = convertDataService.InsertStockMoveLineToSVNDB(stockMoveLineUI);
                                        stock_Move_LineUIs.AddRange(stockMoveLineUI);
                                        processResult = insertResult;
                                    }
                                }
                            }

                        }

                        if (stock_Move_LineUIs.Count > 0)
                        {
                            foreach (var item in stock_Move_LineUIs)
                            {

                                if (item.consume_line_ids != null)
                                {
                                    JArray jArray = JArray.FromObject(item.consume_line_ids);
                                    var json = JsonConvert.SerializeObject(jArray);
                                    var consume_line_ids = JsonConvert.DeserializeObject<List<int>>(json);
                                    foreach (var subitem in consume_line_ids)
                                    {
                                        stock_move_line_consume_relUI dataUI = new stock_move_line_consume_relUI();
                                        dataUI.produce_line_id = item.id;
                                        dataUI.consume_line_id = subitem;
                                        consume_RelUIs.Add(dataUI);
                                    }
                                }

                            }

                            if (consume_RelUIs.Count > 0)
                            {
                                insertResult = convertDataService.InsertStockMoveLineConsumToSVNDB(consume_RelUIs);
                            }
                        }
                        processResult = insertResult;
                    }

                }
            }
            catch(Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        /// <summary>
        /// Lấy thông tin package theo serial number và product ID khi scan
        /// </summary>
        /// <param name="serialNumber"></param>
        /// <param name="productID"></param>
        /// <returns></returns>
        public BODataProcessResult GetPackageBySeri(string serialNumber, int productID)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                object[] domain = new object[] { "name", "=", serialNumber };
                processResult = GetViindooDataByCondition("stock.lot", domain);
                if (processResult.OK)
                {
                    var stockLotUI = convertDataService.ConverterToStockLotUI(processResult.Content);
                    stockLotUI = stockLotUI.Where(x => x.product_id == productID).ToList();
                    if (stockLotUI != null && stockLotUI.Count == 1)
                    {
                        domain = new object[] { "lot_id", "=", stockLotUI[0].id };
                        processResult = GetViindooDataByCondition("stock.move.line", domain);
                        if (processResult.OK) 
                        { 
                            var stockMoveLineUIs = convertDataService.ConverterToStockMoveLineUI(processResult.Content);
                            if (stockMoveLineUIs != null && stockMoveLineUIs.Count > 0)
                            {
                                //test package_id
                                var stockMoveLineUI = stockMoveLineUIs.FirstOrDefault(x => x.package_id == 0);
                                if(stockMoveLineUI == null)
                                {
                                    processResult.OK = false;
                                    processResult.Message = "Get stock move line fail";
                                    return processResult;
                                }
                                domain = new object[] { "id", "=", stockMoveLineUI.result_package_id };
                                processResult = GetViindooDataByCondition("stock.quant.package", domain);
                                if(processResult.OK)
                                {
                                    var stockQuantPackageUI = convertDataService.ConverterQuantPackageUI(processResult.Content);
                                    if (stockQuantPackageUI != null && stockQuantPackageUI.Count == 1)
                                    {
                                        domain = new object[] { "result_package_id", "=", stockQuantPackageUI[0].id };
                                        processResult = GetViindooDataByCondition("stock.move.line", domain);
                                        if (processResult.OK)
                                        {
                                            var stockMoveLineUI2 = convertDataService.ConverterToStockMoveLineUI(processResult.Content);
                                            if (stockMoveLineUI2 != null && stockMoveLineUI2.Count > 0)
                                            {
                                                List<stock_lotUI> stock_LotUIs = new List<stock_lotUI>();
                                                stockMoveLineUI2 = stockMoveLineUI2.Select(x =>
                                                {
                                                    domain = new object[] { "id", "=", x.lot_id };
                                                    processResult = GetViindooDataByCondition("stock.lot", domain);
                                                    if (processResult.OK)
                                                    {
                                                        var stockLotUI2 = convertDataService.ConverterToStockLotUI(processResult.Content);
                                                        if (stockLotUI2 != null && stockLotUI2.Count > 0)
                                                        {
                                                            stock_LotUIs.AddRange(stockLotUI2);
                                                        }
                                                    }
                                                    return x;
                                                }).ToList();

                                                if (stock_LotUIs.Count > 0)
                                                {
                                                    processResult.OK = true;
                                                    processResult.Content = stock_LotUIs;
                                                    processResult.Message = stockQuantPackageUI[0].name;
                                                }
                                                else
                                                {
                                                    processResult.Message = "Get stock lot fail";
                                                }
                                            }
                                            else
                                            {
                                                processResult.Message = "Get stock move line fail";
                                            }
                                        }
                                        else
                                        {
                                            processResult.Message = "Get stock move line fail";
                                        }
                                    }
                                    else
                                    {
                                        processResult.Message = "Get stock quant package fail";
                                    }
                                }
                                else
                                {
                                    processResult.Message = "Get stock quant package fail";
                                }
                            }
                            else
                            {
                                processResult.Message = "Get stock move line fail";
                            }
                        }
                        else
                        {
                            processResult.Message = "Get stock move line fail";
                        }
                    }
                    else
                    {
                        processResult.Message = "Get stock lot fail";
                    }
                }
                else
                {
                    processResult.Message = "Get stock lot fail";
                }
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        /// <summary>
        /// Lấy seri FG và WIP theo tên MO
        /// </summary>
        /// <param name="MOName"></param>
        /// <returns></returns>
        public BODataProcessResult GetSeriFGAndWipByMO(string MOName)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            List<stock_move_lineUI> stock_Move_LineUIs = new List<stock_move_lineUI>();
            List<stock_move_line_consume_relUI> consume_RelUIs = new List<stock_move_line_consume_relUI>();

            List<SeriFGAndWipUI> seriFGAndWipUIs = new List<SeriFGAndWipUI>();

            List<stock_lotUI> stockLotConsumeUI = new List<stock_lotUI>();
            stock_lotUI stockLotProducingUI = new stock_lotUI();
            try
            {
                object[] domain = new object[] { "name", "=", MOName };
                processResult = GetViindooDataByCondition("mrp.production", domain);
                if (processResult.OK)
                {
                    var productiongDataUI = convertDataService.ConverterToProductionUI(processResult.Content);
                    if (productiongDataUI != null && productiongDataUI.Count == 1) 
                    {
                        JArray jArray = JArray.FromObject(productiongDataUI[0].finished_move_line_ids);
                        var json = JsonConvert.SerializeObject(jArray);
                        int[] finished_move_line_ids = JsonConvert.DeserializeObject<int[]>(json);
                        domain = new object[] { "id", "in", finished_move_line_ids };
                        BODataProcessResult processResultStockMoveLine = new BODataProcessResult();
                        processResultStockMoveLine = GetViindooDataByCondition("stock.move.line", domain);
                        if (processResultStockMoveLine.OK)
                        {
                            var stockMoveLineUI = convertDataService.ConverterToStockMoveLineUI(processResultStockMoveLine.Content);
                            if (stockMoveLineUI != null)
                            {
                                stock_Move_LineUIs.AddRange(stockMoveLineUI);
                            }

                            if (stock_Move_LineUIs.Count > 0)
                            {
                                foreach (var item in stock_Move_LineUIs)
                                {

                                    if (item.consume_line_ids != null)
                                    {
                                        JArray jArray1 = JArray.FromObject(item.consume_line_ids);
                                        var json1 = JsonConvert.SerializeObject(jArray1);
                                        var consume_line_ids = JsonConvert.DeserializeObject<List<int>>(json1);
                                        foreach (var subitem in consume_line_ids)
                                        {
                                            stock_move_line_consume_relUI dataUI = new stock_move_line_consume_relUI();
                                            dataUI.produce_line_id = item.id;
                                            dataUI.consume_line_id = subitem;
                                            consume_RelUIs.Add(dataUI);
                                        }
                                    }

                                }

                                // Lây các lot thuộc consume
                                if (consume_RelUIs.Count > 0)
                                {
                                    int[] move_line_ids = consume_RelUIs.Select(x => x.consume_line_id).ToArray();
                                    domain = new object[] { "id", "in", move_line_ids };
                                    BODataProcessResult processResultStockMoveLine2 = new BODataProcessResult();
                                    processResultStockMoveLine2 = GetViindooDataByCondition("stock.move.line", domain);
                                    if (processResultStockMoveLine2.OK)
                                    {
                                        var stockMoveLineConsumeUI2 = convertDataService.ConverterToStockMoveLineUI(processResultStockMoveLine2.Content);
                                        if (stockMoveLineConsumeUI2 != null && stockMoveLineConsumeUI2.Count > 0)
                                        {
                                            var lot_ids = stockMoveLineConsumeUI2.Select(x => x.lot_id).ToArray();
                                            domain = new object[] { "id", "in", lot_ids };
                                            BODataProcessResult processsStockLotConsume = new BODataProcessResult();
                                            processsStockLotConsume = GetViindooDataByCondition("stock.lot", domain);
                                            if (processsStockLotConsume.OK)
                                            {
                                                stockLotConsumeUI = convertDataService.ConverterToStockLotUI(processsStockLotConsume.Content);
                                            }

                                        }
                                        else
                                        {
                                            processResult.OK = false;
                                            processResult.Message = "Get stock move line fail";
                                        }
                                    }
                                    else
                                    {
                                        processResult.OK = false;
                                        processResult.Message = "Get stock move line fail";
                                    }
                                }

                                // Lấy các lot thuộc lot_producing_id
                                domain = new object[] { "id", "=", productiongDataUI[0].lot_producing_id };
                                BODataProcessResult processsStockLotProducing = new BODataProcessResult();
                                processsStockLotProducing = GetViindooDataByCondition("stock.lot", domain);
                                if (processsStockLotProducing.OK)
                                {
                                    var stockLotUI = convertDataService.ConverterToStockLotUI(processsStockLotProducing.Content);
                                    if (stockLotUI != null && stockLotUI.Count == 1) 
                                    {
                                        stockLotProducingUI = stockLotUI.FirstOrDefault();
                                    }
                                }

                                if(stockLotConsumeUI.Count > 0 && stockLotProducingUI != null && stockLotProducingUI.id != 0)
                                {
                                    foreach(var item in stockLotConsumeUI)
                                    {
                                        SeriFGAndWipUI seriFGAndWipUI = new SeriFGAndWipUI();
                                        seriFGAndWipUI.id = productiongDataUI[0].id;
                                        seriFGAndWipUI.name = productiongDataUI[0].name;
                                        seriFGAndWipUI.seriTP = stockLotProducingUI.name;
                                        seriFGAndWipUI.seriBTP = item.name;
                                        seriFGAndWipUI.date_finished = productiongDataUI[0].date_finished;
                                        seriFGAndWipUI.productID = productiongDataUI[0].product_id;
                                        seriFGAndWipUIs.Add(seriFGAndWipUI);
                                    }
                                }

                                if(seriFGAndWipUIs.Count > 0)
                                {
                                    // Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                                    SeriFGAndWipDataPortal dataPortal = new SeriFGAndWipDataPortal(SVNDBConfig.ConnectionString);
                                    var insertResult = dataPortal.InsertBulk(seriFGAndWipUIs);

                                    processResult.OK = true;
                                    processResult.Content = seriFGAndWipUIs;
                                    processResult.Message = "Get and insert seri FG and WIP success";
                                }
                                else
                                {
                                    processResult.OK = false;
                                    processResult.Message = "Get and insert seri FG and WIP fail";
                                }
                            }
                        }
                        else
                        {
                            processResult.OK = false;
                            processResult.Message = "Get stock move line fail";
                        }
                    }
                    else
                    {
                        processResult.OK = false;
                        processResult.Message = "Get mrp production fail";
                        return processResult;
                    }
                }
                else
                {
                    processResult.OK = false;
                    processResult.Message = "Get mrp production fail";
                }
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        public BODataProcessResult GetSeriFGAndWipByDate(string date)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                object[] domain = new object[] { "date_finished", ">=", date };
                processResult = GetViindooDataByCondition("mrp.production", domain);
                if (processResult.OK)
                {
                    var productionDataUI = convertDataService.ConverterToProductionUI(processResult.Content);
                    if (productionDataUI != null)
                    {
                        foreach (var item in productionDataUI)
                        {
                            List<SeriFGAndWipUI> seriFGAndWipUIs = new List<SeriFGAndWipUI>();
                            List<stock_move_lineUI> stock_Move_LineUIs = new List<stock_move_lineUI>();
                            List<stock_move_line_consume_relUI> consume_RelUIs = new List<stock_move_line_consume_relUI>();
                            List<stock_lotUI> stockLotConsumeUI = new List<stock_lotUI>();
                            stock_lotUI stockLotProducingUI = new stock_lotUI();

                            JArray jArray = JArray.FromObject(item.finished_move_line_ids);
                            var json = JsonConvert.SerializeObject(jArray);
                            int[] finished_move_line_ids = JsonConvert.DeserializeObject<int[]>(json);
                            domain = new object[] { "id", "in", finished_move_line_ids };
                            BODataProcessResult processResultStockMoveLine = new BODataProcessResult();
                            processResultStockMoveLine = GetViindooDataByCondition("stock.move.line", domain);
                            if (processResultStockMoveLine.OK)
                            {
                                var stockMoveLineUI = convertDataService.ConverterToStockMoveLineUI(processResultStockMoveLine.Content);
                                if (stockMoveLineUI != null)
                                {
                                    stock_Move_LineUIs.AddRange(stockMoveLineUI);
                                }

                                if (stock_Move_LineUIs.Count > 0)
                                {
                                    foreach (var subitem in stock_Move_LineUIs)
                                    {

                                        if (subitem.consume_line_ids != null)
                                        {
                                            JArray jArray1 = JArray.FromObject(subitem.consume_line_ids);
                                            var json1 = JsonConvert.SerializeObject(jArray1);
                                            var consume_line_ids = JsonConvert.DeserializeObject<List<int>>(json1);
                                            foreach (var subitem2 in consume_line_ids)
                                            {
                                                stock_move_line_consume_relUI dataUI = new stock_move_line_consume_relUI();
                                                dataUI.produce_line_id = item.id;
                                                dataUI.consume_line_id = subitem2;
                                                consume_RelUIs.Add(dataUI);
                                            }
                                        }

                                    }

                                    // Lây các lot thuộc consume
                                    if (consume_RelUIs.Count > 0)
                                    {
                                        int[] move_line_ids = consume_RelUIs.Select(x => x.consume_line_id).ToArray();
                                        domain = new object[] { "id", "in", move_line_ids };
                                        BODataProcessResult processResultStockMoveLine2 = new BODataProcessResult();
                                        processResultStockMoveLine2 = GetViindooDataByCondition("stock.move.line", domain);
                                        if (processResultStockMoveLine2.OK)
                                        {
                                            var stockMoveLineConsumeUI2 = convertDataService.ConverterToStockMoveLineUI(processResultStockMoveLine2.Content);
                                            if (stockMoveLineConsumeUI2 != null && stockMoveLineConsumeUI2.Count > 0)
                                            {
                                                var lot_ids = stockMoveLineConsumeUI2.Select(x => x.lot_id).ToArray();
                                                domain = new object[] { "id", "in", lot_ids };
                                                BODataProcessResult processsStockLotConsume = new BODataProcessResult();
                                                processsStockLotConsume = GetViindooDataByCondition("stock.lot", domain);
                                                if (processsStockLotConsume.OK)
                                                {
                                                    stockLotConsumeUI = convertDataService.ConverterToStockLotUI(processsStockLotConsume.Content);
                                                }

                                            }
                                            else
                                            {
                                                processResult.OK = false;
                                                processResult.Message = "Get stock move line fail";
                                            }
                                        }
                                        else
                                        {
                                            processResult.OK = false;
                                            processResult.Message = "Get stock move line fail";
                                        }
                                    }

                                    // Lấy các lot thuộc lot_producing_id
                                    domain = new object[] { "id", "=", item.lot_producing_id };
                                    BODataProcessResult processsStockLotProducing = new BODataProcessResult();
                                    processsStockLotProducing = GetViindooDataByCondition("stock.lot", domain);
                                    if (processsStockLotProducing.OK)
                                    {
                                        var stockLotUI = convertDataService.ConverterToStockLotUI(processsStockLotProducing.Content);
                                        if (stockLotUI != null && stockLotUI.Count == 1)
                                        {
                                            stockLotProducingUI = stockLotUI.FirstOrDefault();
                                        }
                                    }

                                    if (stockLotConsumeUI.Count > 0 && stockLotProducingUI != null && stockLotProducingUI.id != 0)
                                    {
                                        foreach (var subitem in stockLotConsumeUI)
                                        {
                                            SeriFGAndWipUI seriFGAndWipUI = new SeriFGAndWipUI();
                                            seriFGAndWipUI.id = item.id;
                                            seriFGAndWipUI.name = item.name;
                                            seriFGAndWipUI.seriTP = stockLotProducingUI.name;
                                            seriFGAndWipUI.seriBTP = subitem.name;
                                            seriFGAndWipUI.date_finished = item.date_finished;
                                            seriFGAndWipUI.productID = item.product_id;
                                            seriFGAndWipUIs.Add(seriFGAndWipUI);
                                        }
                                    }

                                    if (seriFGAndWipUIs.Count > 0)
                                    {
                                        // Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                                        SeriFGAndWipDataPortal dataPortal = new SeriFGAndWipDataPortal(SVNDBConfig.ConnectionString);
                                        var insertResult = dataPortal.InsertBulk(seriFGAndWipUIs);

                                        processResult.OK = true;
                                        processResult.Content = seriFGAndWipUIs;
                                        processResult.NumOfRow = seriFGAndWipUIs.Count;
                                        processResult.Message = "Get and insert seri FG and WIP success";
                                    }
                                    else
                                    {
                                        processResult.OK = false;
                                        processResult.Message = "Get and insert seri FG and WIP fail";
                                    }
                                }
                            }
                        }
                        
                    }
                    else
                    {
                        processResult.OK = false;
                        processResult.Message = "Get mrp production fail";
                        return processResult;
                    }
                }
                else
                {
                    processResult.OK = false;
                    processResult.Message = "Get mrp production fail";
                }
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        public BODataProcessResult GetLotByMODone(int product_id, int rows)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            List<svn_lot_infoUI> svn_Lot_InfoUIs = new List<svn_lot_infoUI>();
            try
            {
                object[] domain = new object[] { "product_id", "=", product_id };
                object[] search = new object[] { domain };
                string objectName = "stock.lot";
                string fields = "id,message_main_attachment_id,product_id,product_uom_id,company_id,create_uid,write_uid,origin_message_id,origin_references,name,ref,note,create_date,write_date,customer_id,supplier_id,country_state_id,equipment_id";
                int limit = rows;
                string order = "write_date desc";
                processResult = GetViindooDataByConditionV1(objectName, search, fields, limit, order);
                if (processResult.OK)
                {
                    var stockLotUIs = convertDataService.ConverterToStockLotUI(processResult.Content);
                    if (stockLotUIs != null && stockLotUIs.Count > 0 )
                    {
                        domain = new object[] { "id", "=", product_id };
                        search = new object[] { domain };
                        objectName = "product.product";
                        fields = "id,message_main_attachment_id,product_tmpl_id,create_uid,write_uid,origin_message_id,origin_references,default_code,barcode,combination_indices,volume,weight,active,can_image_variant_1024_be_zoomed,create_date,write_date";
                        limit = 0;
                        order = "write_date desc";
                        processResult = GetViindooDataByConditionV1(objectName, search, fields, limit, order);
                        if (processResult.OK) 
                        {

                            var productProductUIs = convertDataService.ConverterToProductProductUI(processResult.Content);
                            if (productProductUIs != null && productProductUIs.Count > 0)
                            {
                                var productProductUI = productProductUIs.FirstOrDefault();
                                if (productProductUI != null)
                                {
                                    domain = new object[] { "id", "=", productProductUI.product_tmpl_id };
                                    search = new object[] { domain };
                                    objectName = "product.template";
                                    fields = "id,message_main_attachment_id,sequence,categ_id,uom_id,uom_po_id,company_id,color,create_uid,write_uid,origin_message_id,origin_references,detailed_type,type,default_code,priority,name,description,description_purchase,description_sale,list_price,volume,weight,sale_ok,purchase_ok,active,can_image_1024_be_zoomed,has_configurable_attributes,create_date,write_date,tracking,description_picking,description_pickingout,description_pickingin,sale_delay,produce_delay,days_to_prepare_mo,purchase_method,purchase_line_warn,purchase_line_warn_msg,service_type,sale_line_warn,expense_policy,invoice_policy,sale_line_warn_msg,technician_user_id,equipment_assign_to,period_uom,recurring_sale_price,service_tracking";
                                    limit = 0;
                                    order = "write_date desc";
                                    processResult = GetViindooDataByConditionV1(objectName, search, fields, limit, order);
                                    if (processResult.OK) 
                                    {

                                        var productTemplateUIs = convertDataService.ConvertToTemplateUI(processResult.Content);
                                        if (productTemplateUIs != null && productTemplateUIs.Count > 0)
                                        {
                                            var productTemplateUI = productTemplateUIs.FirstOrDefault();
                                            if (productTemplateUI != null) 
                                            {
                                                string strListLotProducingID = string.Join(";", stockLotUIs.Select(x => x.id));
                                                int[] lot_producing_id = stockLotUIs.Select(x => x.id).ToArray();
                                                domain = new object[] { "lot_producing_id", "in", lot_producing_id };
                                                var domain2 = new object[] { "state", "=", "done" };
                                                search = new object[] { domain, domain2 };
                                                objectName = "mrp.production";
                                                fields = "id,product_id,product_uom_id,lot_producing_id,bom_id,name,priority,origin,state,reservation_state,consumption,product_qty,qty_producing,date_planned_start,date_planned_finished,date_deadline,date_start,date_finished,product_uom_qty,x_Svn_customer_SN,finished_move_line_ids";
                                                limit = 0;
                                                order = "write_date desc";
                                                processResult = GetViindooDataByConditionV1(objectName, search, fields, limit, order);
                                                if (processResult.OK)
                                                {
                                                    var mrpProductionUIs = convertDataService.ConverterToProductionUI(processResult.Content);
                                                    if (mrpProductionUIs != null && mrpProductionUIs.Count > 0)
                                                    {
                                                        stockLotUIs = stockLotUIs.Select(x =>
                                                        {
                                                            svn_lot_infoUI svn_Lot_InfoUI = new svn_lot_infoUI();
                                                            svn_Lot_InfoUI.lot_code = x.name;
                                                            svn_Lot_InfoUI.item_name = productTemplateUI.name;
                                                            var productionResult = mrpProductionUIs.FirstOrDefault(y => y.lot_producing_id == x.id);
                                                            if (productionResult != null)
                                                            {
                                                                svn_Lot_InfoUI.product_qty = productionResult.product_qty;
                                                            }
                                                            svn_Lot_InfoUIs.Add(svn_Lot_InfoUI);

                                                            return x;

                                                        }).ToList();
                                                        processResult.OK = true;
                                                        processResult.Content = svn_Lot_InfoUIs;
                                                        processResult.Message = "Get lot info success";
                                                    }
                                                    else
                                                    {
                                                        processResult.Message = "Get mrp production fail";
                                                    }
                                                }
                                                else
                                                {
                                                    processResult.Message = "Get mrp production fail";
                                                }
                                            }
                                            else
                                            {
                                                processResult.Message = "Get product template fail";
                                            }
                                        }
                                        else
                                        {
                                            processResult.Message = "Get product template fail";
                                        }
                                    }
                                    else
                                    {
                                        processResult.Message = "Get product template fail";
                                    }
                                }
                                else
                                {
                                    processResult.Message = "Get product fail";
                                }
                            }
                            else
                            {
                                processResult.Message = "Get product fail";
                            }
                        }
                        else
                        {
                            processResult.Message = "Get product fail";
                        }
                    }
                    else
                    {
                        processResult.Message = "Get stock lot fail";
                    }
                }
                else
                {
                    processResult.Message = "Get stock lot fail";
                }
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        public BODataProcessResult GetViindooData(string objectName)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            var item = dBConfig.QueryConfig.FirstOrDefault(x => x.TableName == objectName);
            if (item != null)
            {
                processResult.DataType = item.TableName;
                try
                {
                    var connectResult = ConnectDB();
                    if (connectResult.OK)
                    {
                        IOdooObject models = XmlRpcProxyGen.Create<IOdooObject>();
                        models.Timeout = 60000;
                        models.Url = serverUrl + "/xmlrpc/2/object";


                        object[] search = new object[] { };
                        string[] domain = new string[] { };
                        string[] fields = new string[] { };
                        if (!string.IsNullOrWhiteSpace(item.Domain))
                        {
                            if (item.Domain.Contains("@write_date"))
                            {
                                //Lấy thời gian hiện tại
                                DateTime curTime = DateTime.Now;
                                //Trừ đi 7h vì dữ liệu trả về cũng bị trừ đi 7 giờ
                                curTime = curTime.AddHours(-7);
                                //Trừ đi 10p để lấy dữ liệu từ 10p trước đến hiện tại
                                curTime = curTime.AddHours(-10);

                                item.Domain = item.Domain.Replace("@write_date", curTime.ToString("yyyy-MM-dd HH:mm:ss")); //"2025-03-20 00:00:00"
                                //item.Domain = item.Domain.Replace("@write_date", "2025-03-20 00:00:00");
                                //curTime.ToString("yyyy-MM-dd HH:mm:ss")
                            }
                            domain = item.Domain.Split(",");
                            search = new object[] { domain };
                        }
                        if (!string.IsNullOrWhiteSpace(item.Fields))
                        {
                            fields = item.Fields.Split(",");
                        }

                        var querydata = new object[]
                        {
                            search,
                            fields,
                            0,
                            item.Limit
                        };
                        if (!string.IsNullOrWhiteSpace(item.Order))
                        {
                            querydata = new object[]
                            {
                                search,
                                fields,
                                0,
                                item.Limit,
                                item.Order
                            };
                        }
                        //new object[] { new object[] { "state", "=", "done" } }
                        //new string[] { "name", "product_id", "state" }

                        object searchResult = models.Execute_Kw(
                            dbName,
                            connectResult.UserID,
                            password,
                            item.TableName,
                            "search_read",
                            querydata);
                        if (searchResult != null)
                        {
                            processResult = SwitchFunctionToInsert(searchResult, objectName);
                        }
                        else
                        {
                            processResult.Message = "Get data fail";
                        }
                    }
                    else
                    {
                        processResult.Message = connectResult.Message;
                    }

                }
                catch (Exception ex)
                {
                    processResult.Message = ex.Message;
                }
            }
            else
            {
                processResult.Message = "Table name not found";
            }
            return processResult;
        }

        /// <summary>
        /// Hàm search data từ viindoo
        /// HÀM CHÍNH ĐANG ĐƯỢC SỬ DỤNG
        /// </summary>
        /// <param name="objectName"></param>
        /// <param name="listDomain"></param>
        /// <param name="strFields"></param>
        /// <param name="strOrder"></param>
        /// <param name="strLimit"></param>
        /// <returns></returns>
        public BODataProcessResult GetViindooDataV1(QueryConfig dataRequest)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            processResult.DataType = dataRequest.TableName;
            string strDatetime = string.Empty;
            try
            {
                var connectResult = ConnectDB();
                if (connectResult.OK)
                {
                    IOdooObject models = XmlRpcProxyGen.Create<IOdooObject>();
                    models.Timeout = 60000;
                    models.Url = serverUrl + "/xmlrpc/2/object";

                    object[] search = new object[] { };
                    object[] domain = new object[] { };
                    string[] fields = new string[] { };
                    if (dataRequest.ListDomain != null && dataRequest.ListDomain.Count > 0)
                    {
                        var domainItems = new List<object>();

                        for (var i = 0; i < dataRequest.ListDomain.Count; i++)
                        {
                            if (dataRequest.ListDomain[i].Contains("@write_date"))
                            {
                                DateTime curTime = DateTime.Now;
                                curTime = curTime.AddHours(-7);
                                curTime = curTime.AddMinutes(-10);

                                strDatetime = curTime.ToString("yyyy-MM-dd HH:mm:ss");

                                dataRequest.ListDomain[i] = dataRequest.ListDomain[i].Replace("@write_date", curTime.ToString("yyyy-MM-dd HH:mm:ss"));
                            }
                            if (dataRequest.ListDomain[i].Contains(","))
                            {
                                object[] domainItem = dataRequest.ListDomain[i].Split(",");
                                for (int j = 0; j < domainItem.Length; j++)
                                {
                                    if (domainItem[j] is string && domainItem[j].ToString().Contains(";"))
                                    {
                                        string[] parts = domainItem[j].ToString().Split(';'); // Split "2,IPQC"

                                        object[] convertedParts = Array.ConvertAll(parts, part =>
                                        {
                                            if (int.TryParse(part, out int result))
                                            {
                                                return (object)result; // Convert "2" to int
                                            }
                                            if (bool.TryParse(part, out bool resultBool))
                                            {
                                                return (object)resultBool; // Convert "true" to bool
                                            }
                                            return (object)part; // Keep "IPQC" as string
                                        });

                                        //object[] convertedItem = new object[] { convertedParts }; // Convert to new object[]

                                        // Create a new array with the updated value
                                        domainItem = ReplaceItem(domainItem, j, convertedParts);
                                        //break;
                                    }
                                }
                                domainItems.Add(domainItem);
                            }
                            if (dataRequest.ListDomain[i] == "|")
                            {
                                domainItems.Add(dataRequest.ListDomain[i]);
                            }
                        }

                        domain = domainItems.ToArray();
                        search = domain;
                        //search = new object[] { domain };
                    }
                    if (!string.IsNullOrWhiteSpace(dataRequest.Fields))
                    {
                        fields = dataRequest.Fields.Split(",");
                    }
                    var querydata = new object[]
                    {
                            search,
                            fields,
                            0,
                            dataRequest.Limit
                    };
                    if (!string.IsNullOrWhiteSpace(dataRequest.Order))
                    {
                        querydata = new object[]
                        {
                                search,
                                fields,
                                0,
                                dataRequest.Limit,
                                dataRequest.Order
                        };
                    }
                    object searchResult = models.Execute_Kw(
                        dbName,
                        connectResult.UserID,
                        password,
                        dataRequest.TableName,
                        "search_read",
                        querydata);

                    if (searchResult != null)
                    {
                        processResult = SwitchFunctionToInsert(searchResult, dataRequest.TableName);
                        if(dataRequest.TableName == "mrp.production")
                        {
                            processResult.NumOfRow = ((object[])searchResult).Length;
                            processResult.Message = processResult.Message + " at Date_finished more than " + strDatetime;
                        }
                    }
                    else
                    {
                        processResult.Message = "Get data fail";
                    }
                }
                else
                {
                    processResult.Message = connectResult.Message;
                }
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }

            //Cập nhật lại domain nếu có chứa @write_date
            if (dataRequest.ListDomain != null && dataRequest.ListDomain.Count > 0)
            {
                for (var i = 0; i < dataRequest.ListDomain.Count; i++)
                {
                    if (dataRequest.ListDomain[i].Contains(strDatetime) && !string.IsNullOrWhiteSpace(strDatetime))
                    {
                        dataRequest.ListDomain[i] = dataRequest.ListDomain[i].Replace(strDatetime, "@write_date");
                    }
                }
            }

            return processResult;
        }

        /// <summary>
        /// Hàm search data từ viindoo
        /// </summary>
        /// <param name="objectName"></param>
        /// <param name="listDomain"></param>
        /// <param name="strFields"></param>
        /// <param name="strOrder"></param>
        /// <param name="strLimit"></param>
        /// <returns></returns>
        public BODataProcessResult GetViindooDataV2(ViindooDataRequest dataRequest)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            processResult.DataType = dataRequest.TableName;
            try
            {
                var connectResult = ConnectDB();
                if (connectResult.OK)
                {
                    IOdooObject models = XmlRpcProxyGen.Create<IOdooObject>();
                    models.Timeout = 60000;
                    models.Url = serverUrl + "/xmlrpc/2/object";


                    object[] search = new object[] { };
                    object[] domain = new object[] { };
                    string[] fields = new string[] { };
                    if (dataRequest.listDomain != null && dataRequest.listDomain.Count > 0)
                    {
                        var domainItems = new List<object>();

                        for (var i = 0; i < dataRequest.listDomain.Count; i++)
                        {
                            if (dataRequest.listDomain[i].Contains("@write_date"))
                            {
                                DateTime curTime = DateTime.Now;
                                curTime = curTime.AddHours(-7);
                                curTime = curTime.AddHours(-10);
                                dataRequest.listDomain[i] = dataRequest.listDomain[i].Replace("@write_date", curTime.ToString("yyyy-MM-dd HH:mm:ss"));
                            }
                            if(dataRequest.listDomain[i].Contains(","))
                            {
                                object[] domainItem = dataRequest.listDomain[i].Split(",");
                                for (int j = 0; j < domainItem.Length; j++)
                                {
                                    if (domainItem[j] is string && domainItem[j].ToString().Contains(";"))
                                    {
                                        string[] parts = domainItem[j].ToString().Split(';'); // Split "2,IPQC"

                                        object[] convertedParts = Array.ConvertAll(parts, part =>
                                        {
                                            if (int.TryParse(part, out int result))
                                            {
                                                return (object)result; // Convert "2" to int
                                            }
                                            if(bool.TryParse(part, out bool resultBool))
                                            {
                                                return (object)resultBool; // Convert "true" to bool
                                            }
                                            return (object)part; // Keep "IPQC" as string
                                        });

                                        //object[] convertedItem = new object[] { convertedParts }; // Convert to new object[]

                                        // Create a new array with the updated value
                                        domainItem = ReplaceItem(domainItem, j, convertedParts);
                                        //break;
                                    }
                                }
                                domainItems.Add(domainItem);
                            }
                            if(dataRequest.listDomain[i] == "|")
                            {
                                domainItems.Add(dataRequest.listDomain[i]);
                            }
                        }

                        domain = domainItems.ToArray();
                        search = domain;
                        //search = new object[] { domain };
                    }
                    if (!string.IsNullOrWhiteSpace(dataRequest.Fields))
                    {
                        fields = dataRequest.Fields.Split(",");
                    }
                    var querydata = new object[]
                    {
                            search,
                            fields,
                            0,
                            dataRequest.Limit
                    };
                    if (!string.IsNullOrWhiteSpace(dataRequest.Order))
                    {
                        querydata = new object[]
                        {
                                search,
                                fields,
                                0,
                                dataRequest.Limit,
                                dataRequest.Order
                        };
                    }
                    object searchResult = models.Execute_Kw(
                        dbName,
                        connectResult.UserID,
                        password,
                        dataRequest.TableName,
                        "search_read",
                        querydata);
                    if (searchResult != null)
                    {
                        var dynamicParameters = convertDataService.ConvertObjectToData(searchResult);

                        processResult.OK = true;
                        processResult.Message = "Get data success";

                        JArray jArray = JArray.FromObject(searchResult);
                        object data = JsonConvert.SerializeObject(jArray);

                        //var dataUI = convertDataService.ConverterToQuantityReasonUI(searchResult);
                        //var result = convertDataService.InsertQuantityReasonToSVNDB(dataUI);

                        var dataUI = convertDataService.ConverterToQuantityAlertUI(searchResult);
                        var result = convertDataService.InsertQuantityAlertToSVNDB(dataUI);

                        //List<Dictionary<string, object>> dataDict = new List<Dictionary<string, object>>();

                        //// Duyệt qua từng phần tử trong JArray và thêm vào danh sách
                        //foreach (var item in jArray)
                        //{
                        //    var obj = new Dictionary<string, object>();

                        //    // Duyệt qua các cặp key-value trong mỗi đối tượng JSON
                        //    foreach (var property in item.Children<JProperty>())
                        //    {
                        //        obj[property.Name] = property.Value;
                        //    }

                        //    // Thêm đối tượng vào danh sách
                        //    dataDict.Add(obj);
                        //}

                        //if (dataRequest.IsUpdate == true && dataDict != null)
                        //{
                        //    DatabaseDataPortal dataPortal = new DatabaseDataPortal(SVNDBConfig.ConnectionString);

                        //    var result = dataPortal.ExecuteData(dataRequest.InsertQuery, dataDict);

                        //}

                        processResult.Content = data;
                    }
                    else
                    {
                        processResult.Message = "Get data fail";
                    }
                }
                else
                {
                    processResult.Message = connectResult.Message;
                }
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        private static object[] ReplaceItem(object[] originalArray, int index, object newItem)
        {
            object[] newArray = new object[originalArray.Length]; // Create a new array
            Array.Copy(originalArray, newArray, originalArray.Length); // Copy original values
            newArray[index] = newItem; // Replace the specified index
            return newArray;
        }

        public BODataProcessResult CallSPToUpdateResult()
        {
            BODataProcessResult processResult = new BODataProcessResult();
            processResult.DataType = "CallSPToUpdateResult";
            try
            {
                GrandDataPortal<mrp_productionUI> dataPortal = new GrandDataPortal<mrp_productionUI>("SVN_mrp_production_1", SVNDBConfig.ConnectionString);
                string storedProcedure = "SVN_Update_result_Viindoo_WC";
                DynamicParameters parameters = new DynamicParameters();
                processResult = dataPortal.CallStoredProcedure(storedProcedure, parameters);
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }
        #endregion
    }
}
