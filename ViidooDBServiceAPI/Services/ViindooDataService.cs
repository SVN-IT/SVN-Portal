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

        public BODataProcessResult GetPackageBySeri(string serialNumber)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                object[] domain = new object[] { "name", "=", serialNumber };
                processResult = GetViindooDataByCondition("stock.lot", domain);
                if (processResult.OK)
                {
                    var stockLotUI = convertDataService.ConverterToStockLotUI(processResult.Content);
                    if (stockLotUI != null && stockLotUI.Count == 1)
                    {
                        domain = new object[] { "lot_id", "=", stockLotUI[0].id };
                        processResult = GetViindooDataByCondition("stock.move.line", domain);
                        if (processResult.OK) 
                        { 
                            var stockMoveLineUI = convertDataService.ConverterToStockMoveLineUI(processResult.Content);
                            if (stockMoveLineUI != null && stockMoveLineUI.Count > 0)
                            {
                                domain = new object[] { "id", "=", stockMoveLineUI[0].result_package_id };
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
