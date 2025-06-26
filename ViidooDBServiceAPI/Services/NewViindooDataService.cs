using CookComputing.XmlRpc;
using SVNShareLib;

namespace ViidooDBServiceAPI.Services
{
    public interface INewOdooCommon : IXmlRpcProxy
    {
        [XmlRpcMethod("authenticate")]
        int Authenticate(string db, string username, string password, object args);
    }

    public interface INewOdooObject : IXmlRpcProxy
    {
        [XmlRpcMethod("execute_kw")]
        object ExecuteKw(string db, int uid, string password, string model, string method, object[] args, object kwargs = null);
    }

    public class NewViindooDataService
    {
        SVNDBConfig SVNDBConfig;
        ViindooDBConfig dBConfig;
        ConvertDataService convertDataService;
        private static string serverUrl;
        private static string dbName;
        private static string username;
        private static string password;
        private static string tableName;
        public NewViindooDataService(ViindooDBConfig dBConfig, SVNDBConfig sVNDBConfig, ConvertDataService convertDataService)
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
                INewOdooCommon common = XmlRpcProxyGen.Create<INewOdooCommon>();
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

        private BODataProcessResult ExecuteViindooDataByConditionV1(string objectName, object[] args, XmlRpcStruct kwargs, string method = "search_read")
        {
            BODataProcessResult processResult = new BODataProcessResult();
            processResult.DataType = objectName;
            try
            {
                var connectResult = ConnectDB();
                if (connectResult.OK)
                {
                    INewOdooObject models = XmlRpcProxyGen.Create<INewOdooObject>();
                    models.Timeout = 60000;
                    models.Url = serverUrl + "/xmlrpc/2/object";
                    string[] fields = new string[] { };

                    object searchResult = models.ExecuteKw(
                        dbName,
                        connectResult.UserID,
                        password,
                        objectName,
                        method,
                        args,
                        kwargs);
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

        public BODataProcessResult InputResult(int product_id, string serialNumber, int qtyProducing)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            IOdooObject models = XmlRpcProxyGen.Create<IOdooObject>();
            models.Timeout = 60000;
            models.Url = serverUrl + "/xmlrpc/2/object";
            try
            {
                string method = string.Empty;
                object[] domain = new object[] { "product_id", "=", product_id };
                object[] search = new object[] { 
                    new object[] { domain } 
                };
                string objectName = "mrp.production";
                string fields = "id,name,origin";
                int limit = 1;
                string order = "write_date desc";

                XmlRpcStruct kwargs = new XmlRpcStruct
                {
                    { "fields", fields.Split(",") },
                    { "limit", limit },
                    { "order", order }
                };

                processResult = ExecuteViindooDataByConditionV1(objectName, search, kwargs);
                if (processResult.OK)
                {
                    var productionData = convertDataService.ConverterToProductionUI(processResult.Content);

                    method = "write";
                    search = new object[] {
                        new int[] { productionData[0].id },  // ID của Manufacturing Order
                        new XmlRpcStruct {
                            { "qty_producing", qtyProducing }  // Trường cần cập nhật
                        }
                    };
                    processResult = ExecuteViindooDataByConditionV1(objectName, search, null, method);
                    if (processResult.OK)
                    {

                        domain = new object[] { "name", "=", serialNumber };
                        search = new object[] {
                            new object[] { domain }
                        };
                        kwargs = new XmlRpcStruct
                        {
                            { "fields", new string[] { "id" } },
                            { "limit", 1 }
                        };
                        objectName = "stock.lot";
                        processResult = ExecuteViindooDataByConditionV1(objectName, search, kwargs);
                        int lotId = 0;

                        if (processResult.OK)
                        {

                            lotId = Convert.ToInt32(((object[])processResult.Content)[0].GetType().GetProperty("id").GetValue(((object[])processResult.Content)[0], null));
                        }
                        else
                        {
                            objectName = "stock.lot";
                            method = "create";
                            search = new object[] {
                            new object[] {
                                new XmlRpcStruct {
                                    { "name", serialNumber },
                                    { "product_id", product_id }
                                }
                            }
                        };
                            processResult = ExecuteViindooDataByConditionV1(objectName, search, null, method);
                            if (processResult.OK)
                            {
                                lotId = Convert.ToInt32(((object[])processResult.Content)[0].GetType().GetProperty("id").GetValue(((object[])processResult.Content)[0], null));
                            }
                            else
                            {
                                processResult.Message = "Create stock.lot failed: " + processResult.Message;
                                return processResult;
                            }
                        }

                        if (lotId > 0)
                        {
                            objectName = "stock.move.line";
                            method = "create";
                            search = new object[] {
                                new object[] {
                                    new XmlRpcStruct {
                                        { "product_id", product_id },
                                        { "lot_id", lotId },
                                        { "production_id", productionData[0].id },
                                        { "qty_done", 1.0 },
                                        { "location_id", productionData[0].production_location_id },
                                        { "location_dest_id", productionData[0].location_dest_id }
                                    }
                                }
                            };
                            processResult = ExecuteViindooDataByConditionV1(objectName, search, null, method);
                            if (processResult.OK)
                            {
                                processResult.Message = "Input result successfully.";
                            }
                            else
                            {
                                processResult.Message = "Create mrp.production.input failed: " + processResult.Message;
                            }
                        }
                        else
                        {
                            processResult.Message = "Lot ID is not valid.";
                        }

                        objectName = "mrp.production";
                        method = "button_mark_done";
                        search = new object[] {
                            new int[] { productionData[0].id }
                        };
                        processResult = ExecuteViindooDataByConditionV1(objectName, search, null, method);
                        if (processResult.OK)
                        {
                            processResult.Message = "Production marked as done successfully.";
                        }
                        else
                        {
                            processResult.Message = "Mark production as done failed: " + processResult.Message;
                        }
                    }
                    else
                    {
                        processResult.Message = "Create mrp.production failed: " + processResult.Message;
                    }
                }
                else
                {
                    processResult.Message = "Get production data failed: " + processResult.Message;
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
