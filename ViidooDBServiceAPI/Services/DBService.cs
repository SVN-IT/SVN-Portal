using CookComputing.XmlRpc;
using Newtonsoft.Json;
using SVNShareLib;
using SVNShareLib.BaseObject;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Newtonsoft.Json.Linq;
using SVNShareLib.DTO;
using System.Xml.Linq;
using SVNShareLib.DAL;
using Dapper;

namespace ViidooDBServiceAPI.Services
{
    public interface IOdooCommon : IXmlRpcProxy
    {
        [XmlRpcMethod("authenticate")]
        int Authenticate(string db, string user, string password, XmlRpcStruct context);

        [XmlRpcMethod("version")]
        XmlRpcStruct Version();
    }

    public interface IOdooObject : IXmlRpcProxy
    {
        [XmlRpcMethod("execute_kw")]
        object Execute_Kw(string db, int uid, string password, string model, string method, object[] args);
    }
    public class DBService
    {
        SVNDBConfig SVNDBConfig;
        ViindooDBConfig dBConfig;
        private static string serverUrl;
        private static string dbName;
        private static string username;
        private static string password;
        private static string tableName;
        public DBService(ViindooDBConfig dBConfig, SVNDBConfig sVNDBConfig)
        {
            this.dBConfig = dBConfig;
            serverUrl = dBConfig.ServerUrl;
            dbName = dBConfig.DbName;
            username = dBConfig.Username;
            password = dBConfig.Password;
            SVNDBConfig = sVNDBConfig;
        }

        public BODataProcessResult ConnectDB()
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                // 1. Authentication
                IOdooCommon common = XmlRpcProxyGen.Create<IOdooCommon>();
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

        public List<BODataProcessResult> GetData()
        {
            List<BODataProcessResult> processResults = new List<BODataProcessResult>();
            foreach (var item in dBConfig.QueryConfig)
            {
                BODataProcessResult processResult = new BODataProcessResult();
                processResult.DataType = item.TableName;
                try
                {
                    var connectResult = ConnectDB();
                    if (connectResult.OK)
                    {
                        IOdooObject models = XmlRpcProxyGen.Create<IOdooObject>();
                        models.Url = serverUrl + "/xmlrpc/2/object";


                        object[] search = new object[] { };
                        string[] domain = new string[] { };
                        string[] fields = new string[] { };
                        if (!string.IsNullOrWhiteSpace(item.Domain))
                        {
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
                            processResult.OK = true;
                            processResult.Message = "Get data success";
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
                processResults.Add(processResult);
            }
            return processResults;
        }

        /// <summary>
        /// GetProductionResultData
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public BODataProcessResult GetProductionResultData(string tableName = "mrp.production")
        {
            BODataProcessResult processResult = new BODataProcessResult();
            var item = dBConfig.QueryConfig.FirstOrDefault(x => x.TableName == tableName);
            if (item != null)
            {
                processResult.DataType = item.TableName;
                try
                {
                    var connectResult = ConnectDB();
                    if (connectResult.OK)
                    {
                        IOdooObject models = XmlRpcProxyGen.Create<IOdooObject>();
                        models.Url = serverUrl + "/xmlrpc/2/object";


                        object[] search = new object[] { };
                        string[] domain = new string[] { };
                        string[] fields = new string[] { };
                        if (!string.IsNullOrWhiteSpace(item.Domain))
                        {
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
                            processResult.OK = true;
                            processResult.Message = "Get data success";
                            processResult.Content = searchResult;
                            var dataUI = ConverterToProductionUI(searchResult);

                            BODataProcessResult insertResult = new BODataProcessResult();
                            if (dataUI != null && dataUI.Count > 0)
                            {
                                //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                                insertResult = InsertProductionResultToSVNDB(dataUI);
                            }
                            //Nếu insert thành công thì gọi stored proceduce để tổng hợ dữ liệu
                            if (insertResult.OK)
                            {
                                var callResult = CallSPToUpdateResult();
                                processResult = callResult;
                            }
                            else
                            {
                                processResult = insertResult;
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
            }
            else
            {
                processResult.Message = "Table name not found";
            }
            return processResult;
        }

        /// <summary>
        /// GetProductTemplateData
        /// </summary>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public BODataProcessResult GetProductTemplateData(string tableName = "product.template")
        {
            BODataProcessResult processResult = new BODataProcessResult();
            var item = dBConfig.QueryConfig.FirstOrDefault(x => x.TableName == tableName);
            if (item != null)
            {
                processResult.DataType = item.TableName;
                try
                {
                    var connectResult = ConnectDB();
                    if (connectResult.OK)
                    {
                        IOdooObject models = XmlRpcProxyGen.Create<IOdooObject>();
                        models.Url = serverUrl + "/xmlrpc/2/object";


                        object[] search = new object[] { };
                        string[] domain = new string[] { };
                        string[] fields = new string[] { };
                        if (!string.IsNullOrWhiteSpace(item.Domain))
                        {
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
                            processResult.OK = true;
                            processResult.Message = "Get data success";
                            processResult.Content = searchResult;
                            var dataUI = ConvertToTemplateUI(searchResult);

                            BODataProcessResult insertResult = new BODataProcessResult();
                            if (dataUI != null && dataUI.Count > 0)
                            {
                                //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                                insertResult = InsertProductionTemplateToSVNDB(dataUI);
                                processResult = insertResult;
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
            }
            else
            {
                processResult.Message = "Table name not found";
            }
            return processResult;
        }

        public BODataProcessResult GetBomData(string tableName = "mrp.bom")
        {
            BODataProcessResult processResult = new BODataProcessResult();
            var item = dBConfig.QueryConfig.FirstOrDefault(x => x.TableName == tableName);
            if (item != null)
            {
                processResult.DataType = item.TableName;
                try
                {
                    var connectResult = ConnectDB();
                    if (connectResult.OK)
                    {
                        IOdooObject models = XmlRpcProxyGen.Create<IOdooObject>();
                        models.Url = serverUrl + "/xmlrpc/2/object";


                        object[] search = new object[] { };
                        string[] domain = new string[] { };
                        string[] fields = new string[] { };
                        if (!string.IsNullOrWhiteSpace(item.Domain))
                        {
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
                            processResult.OK = true;
                            processResult.Message = "Get data success";
                            processResult.Content = searchResult;
                            var dataUI = ConverterToBomUI(searchResult);

                            BODataProcessResult insertResult = new BODataProcessResult();
                            if (dataUI != null && dataUI.Count > 0)
                            {
                                //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                                insertResult = InsertBOMToSVNDB(dataUI);
                                processResult = insertResult;
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
            }
            else
            {
                processResult.Message = "Table name not found";
            }
            return processResult;
        }

        public BODataProcessResult GetBomLineData(string tableName = "mrp.bom.line")
        {
            BODataProcessResult processResult = new BODataProcessResult();
            var item = dBConfig.QueryConfig.FirstOrDefault(x => x.TableName == tableName);
            if (item != null)
            {
                processResult.DataType = item.TableName;
                try
                {
                    var connectResult = ConnectDB();
                    if (connectResult.OK)
                    {
                        IOdooObject models = XmlRpcProxyGen.Create<IOdooObject>();
                        models.Url = serverUrl + "/xmlrpc/2/object";


                        object[] search = new object[] { };
                        string[] domain = new string[] { };
                        string[] fields = new string[] { };
                        if (!string.IsNullOrWhiteSpace(item.Domain))
                        {
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
                            processResult.OK = true;
                            processResult.Message = "Get data success";
                            processResult.Content = searchResult;
                            var dataUI = ConverterToBomLineUI(searchResult);

                            BODataProcessResult insertResult = new BODataProcessResult();
                            if (dataUI != null && dataUI.Count > 0)
                            {
                                //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                                insertResult = InsertBomLineToSVNDB(dataUI);
                                processResult = insertResult;
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
            }
            else
            {
                processResult.Message = "Table name not found";
            }
            return processResult;
        }

        public BODataProcessResult GetStockLotData(string tableName = "stock.lot")
        {
            BODataProcessResult processResult = new BODataProcessResult();
            var item = dBConfig.QueryConfig.FirstOrDefault(x => x.TableName == tableName);
            if (item != null)
            {
                processResult.DataType = item.TableName;
                try
                {
                    var connectResult = ConnectDB();
                    if (connectResult.OK)
                    {
                        IOdooObject models = XmlRpcProxyGen.Create<IOdooObject>();
                        models.Url = serverUrl + "/xmlrpc/2/object";


                        object[] search = new object[] { };
                        string[] domain = new string[] { };
                        string[] fields = new string[] { };
                        if (!string.IsNullOrWhiteSpace(item.Domain))
                        {
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
                            processResult.OK = true;
                            processResult.Message = "Get data success";
                            processResult.Content = searchResult;
                            var dataUI = ConverterToStockLotUI(searchResult);

                            BODataProcessResult insertResult = new BODataProcessResult();
                            if (dataUI != null && dataUI.Count > 0)
                            {
                                //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                                insertResult = InsertStockLotToSVNDB(dataUI);
                                processResult = insertResult;
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
            }
            else
            {
                processResult.Message = "Table name not found";
            }
            return processResult;
        }

        public BODataProcessResult GetProdCatData(string tableName = "product.category")
        {
            BODataProcessResult processResult = new BODataProcessResult();
            var item = dBConfig.QueryConfig.FirstOrDefault(x => x.TableName == tableName);
            if (item != null)
            {
                processResult.DataType = item.TableName;
                try
                {
                    var connectResult = ConnectDB();
                    if (connectResult.OK)
                    {
                        IOdooObject models = XmlRpcProxyGen.Create<IOdooObject>();
                        models.Url = serverUrl + "/xmlrpc/2/object";


                        object[] search = new object[] { };
                        string[] domain = new string[] { };
                        string[] fields = new string[] { };
                        if (!string.IsNullOrWhiteSpace(item.Domain))
                        {
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
                            processResult.OK = true;
                            processResult.Message = "Get data success";
                            processResult.Content = searchResult;
                            var dataUI = ConverterToProductCatUI(searchResult);

                            BODataProcessResult insertResult = new BODataProcessResult();
                            if (dataUI != null && dataUI.Count > 0)
                            {
                                //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                                insertResult = InsertProductCatToSVNDB(dataUI);
                                processResult = insertResult;
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
            }
            else
            {
                processResult.Message = "Table name not found";
            }
            return processResult;
        }

        public BODataProcessResult GetStockMoveData(string tableName = "stock.move")
        {
            BODataProcessResult processResult = new BODataProcessResult();
            var item = dBConfig.QueryConfig.FirstOrDefault(x => x.TableName == tableName);
            if (item != null)
            {
                processResult.DataType = item.TableName;
                try
                {
                    var connectResult = ConnectDB();
                    if (connectResult.OK)
                    {
                        IOdooObject models = XmlRpcProxyGen.Create<IOdooObject>();
                        models.Url = serverUrl + "/xmlrpc/2/object";


                        object[] search = new object[] { };
                        string[] domain = new string[] { };
                        string[] fields = new string[] { };
                        if (!string.IsNullOrWhiteSpace(item.Domain))
                        {
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
                            processResult.OK = true;
                            processResult.Message = "Get data success";
                            processResult.Content = searchResult;
                            var dataUI = ConverterToStockMoveUI(searchResult);

                            BODataProcessResult insertResult = new BODataProcessResult();
                            if (dataUI != null && dataUI.Count > 0)
                            {
                                //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                                insertResult = InsertStockMoveToSVNDB(dataUI);
                                processResult = insertResult;
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
            }
            else
            {
                processResult.Message = "Table name not found";
            }
            return processResult;
        }

        public BODataProcessResult GetStockMoveLineData(string tableName = "stock.move.line")
        {
            BODataProcessResult processResult = new BODataProcessResult();
            var item = dBConfig.QueryConfig.FirstOrDefault(x => x.TableName == tableName);
            if (item != null)
            {
                processResult.DataType = item.TableName;
                try
                {
                    var connectResult = ConnectDB();
                    if (connectResult.OK)
                    {
                        IOdooObject models = XmlRpcProxyGen.Create<IOdooObject>();
                        models.Url = serverUrl + "/xmlrpc/2/object";


                        object[] search = new object[] { };
                        string[] domain = new string[] { };
                        string[] fields = new string[] { };
                        if (!string.IsNullOrWhiteSpace(item.Domain))
                        {
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
                            processResult.OK = true;
                            processResult.Message = "Get data success";
                            processResult.Content = searchResult;
                            var dataUI = ConverterToStockMoveLineUI(searchResult);

                            BODataProcessResult insertResult = new BODataProcessResult();
                            if (dataUI != null && dataUI.Count > 0)
                            {
                                //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                                insertResult = InsertStockMoveLineToSVNDB(dataUI);
                                processResult = insertResult;
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
            }
            else
            {
                processResult.Message = "Table name not found";
            }
            return processResult;
        }

        public BODataProcessResult GetStockMoveLineConsumeRelData(string tableName = "stock.move.line.consume.rel")
        {
            BODataProcessResult processResult = new BODataProcessResult();
            var item = dBConfig.QueryConfig.FirstOrDefault(x => x.TableName == tableName);
            if (item != null)
            {
                processResult.DataType = item.TableName;
                try
                {
                    var connectResult = ConnectDB();
                    if (connectResult.OK)
                    {
                        IOdooObject models = XmlRpcProxyGen.Create<IOdooObject>();
                        models.Url = serverUrl + "/xmlrpc/2/object";


                        object[] search = new object[] { };
                        string[] domain = new string[] { };
                        string[] fields = new string[] { };
                        if (!string.IsNullOrWhiteSpace(item.Domain))
                        {
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
                            processResult.OK = true;
                            processResult.Message = "Get data success";
                            processResult.Content = searchResult;
                            var dataUI = ConverterToStockMoveLineConsumUI(searchResult);

                            BODataProcessResult insertResult = new BODataProcessResult();
                            if (dataUI != null && dataUI.Count > 0)
                            {
                                //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                                insertResult = InsertStockMoveLineConsumToSVNDB(dataUI);
                                processResult = insertResult;
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
            }
            else
            {
                processResult.Message = "Table name not found";
            }
            return processResult;
        }


        private List<product_templateUI> ConvertToTemplateUI(object searchResult)
        {
            List<product_templateUI> dataUIs = new List<product_templateUI>();
            List<product_template> baseData = new List<product_template>();
            try
            {
                JArray jArray = JArray.FromObject(searchResult);
                var json = JsonConvert.SerializeObject(jArray);
                baseData = JsonConvert.DeserializeObject<List<product_template>>(json);
                foreach (var item in baseData)
                {
                    product_templateUI dataUI = new product_templateUI();
                    dataUI.id = item.id;
                    if (item.message_main_attachment_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.message_main_attachment_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.message_main_attachment_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    dataUI.sequence = item.sequence;
                    if (item.categ_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.categ_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.categ_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.uom_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.uom_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.uom_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.uom_po_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.uom_po_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.uom_po_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.company_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.company_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.company_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    dataUI.color = item.color;
                    if (item.create_uid != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.create_uid;
                            var intTemp = (Int64)objects[0];
                            dataUI.create_uid = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.write_uid != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.write_uid;
                            var intTemp = (Int64)objects[0];
                            dataUI.write_uid = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    dataUI.origin_message_id = item.origin_message_id;
                    dataUI.origin_references = item.origin_references;
                    dataUI.detailed_type = item.detailed_type;
                    dataUI.type = item.type;
                    dataUI.default_code = item.detailed_type;
                    dataUI.priority = item.priority;
                    dataUI.name = item.name;
                    dataUI.description = item.description;
                    dataUI.description_purchase = item.description_purchase;
                    dataUI.description_sale = item.description_sale;
                    dataUI.list_price = item.list_price;
                    dataUI.volume = item.volume;
                    dataUI.weight = item.weight;
                    dataUI.sale_ok = item.sale_ok;
                    dataUI.purchase_ok = item.purchase_ok;
                    dataUI.active = item.active;
                    dataUI.can_image_1024_be_zoomed = item.can_image_1024_be_zoomed;
                    dataUI.has_configurable_attributes = item.has_configurable_attributes;
                    dataUI.create_date = item.create_date;
                    dataUI.write_date = item.write_date;
                    dataUI.tracking = item.tracking;
                    dataUI.description_picking = item.description_picking;
                    dataUI.description_pickingout = item.description_pickingout;
                    dataUI.description_pickingin = item.description_pickingin;
                    dataUI.sale_delay = item.sale_delay;
                    dataUI.produce_delay = item.produce_delay;
                    dataUI.days_to_prepare_mo = item.days_to_prepare_mo;
                    dataUI.purchase_method = item.purchase_method;
                    dataUI.purchase_line_warn = item.purchase_line_warn;
                    dataUI.purchase_line_warn_msg = item.purchase_line_warn_msg;
                    dataUI.service_type = item.service_type;
                    dataUI.sale_line_warn = item.sale_line_warn;
                    dataUI.expense_policy = item.expense_policy;
                    dataUI.invoice_policy = item.invoice_policy;
                    dataUI.sale_line_warn_msg = item.sale_line_warn_msg;
                    if (item.technician_user_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.technician_user_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.technician_user_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    dataUI.equipment_assign_to = item.equipment_assign_to;
                    dataUI.period_uom = item.period_uom;
                    dataUI.recurring_sale_price = item.recurring_sale_price;
                    dataUI.service_tracking = item.service_tracking;

                    dataUIs.Add(dataUI);
                }
                return dataUIs;
            }
            catch
            {
                return null;
            }
        }
        private List<mrp_productionUI> ConverterToProductionUI(object searchResult)
        {
            List<mrp_productionUI> dataUIs = new List<mrp_productionUI>();
            List<mrp_production> baseData = new List<mrp_production>();
            try
            {
                JArray jArray = JArray.FromObject(searchResult);
                var json = JsonConvert.SerializeObject(jArray);
                baseData = JsonConvert.DeserializeObject<List<mrp_production>>(json);
                foreach (var item in baseData)
                {
                    mrp_productionUI mrp_ProductionUI = new mrp_productionUI();
                    mrp_ProductionUI.id = item.id;
                    mrp_ProductionUI.message_main_attachment_id = item.message_main_attachment_id;
                    mrp_ProductionUI.backorder_sequence = item.backorder_sequence;
                    if (item.product_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.product_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.product_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.product_uom_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.product_uom_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.product_uom_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.lot_producing_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.lot_producing_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.lot_producing_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    mrp_ProductionUI.picking_type_id = item.picking_type_id;
                    mrp_ProductionUI.location_src_id = item.location_src_id;
                    mrp_ProductionUI.location_dest_id = item.location_dest_id;
                    if (item.bom_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.bom_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.bom_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    mrp_ProductionUI.user_id = item.user_id;
                    mrp_ProductionUI.company_id = item.company_id;
                    mrp_ProductionUI.procurement_group_id = item.procurement_group_id;
                    mrp_ProductionUI.orderpoint_id = item.orderpoint_id;
                    mrp_ProductionUI.production_location_id = item.production_location_id;
                    mrp_ProductionUI.create_uid = item.create_uid;
                    mrp_ProductionUI.write_uid = item.write_uid;
                    mrp_ProductionUI.origin_message_id = item.origin_message_id;
                    mrp_ProductionUI.origin_references = item.origin_references;
                    mrp_ProductionUI.name = item.name;
                    mrp_ProductionUI.priority = item.priority;
                    mrp_ProductionUI.origin = item.origin;
                    mrp_ProductionUI.state = item.state;
                    mrp_ProductionUI.reservation_state = item.reservation_state;
                    mrp_ProductionUI.product_description_variants = item.product_description_variants;
                    mrp_ProductionUI.consumption = item.consumption;
                    mrp_ProductionUI.product_qty = item.product_qty;
                    mrp_ProductionUI.qty_producing = item.qty_producing;
                    mrp_ProductionUI.propagate_cancel = item.propagate_cancel;
                    mrp_ProductionUI.is_locked = item.is_locked;
                    mrp_ProductionUI.is_planned = item.is_planned;
                    mrp_ProductionUI.allow_workorder_dependencies = item.allow_workorder_dependencies;
                    mrp_ProductionUI.date_planned_start = item.date_planned_start;
                    mrp_ProductionUI.date_planned_finished = item.date_planned_finished;
                    mrp_ProductionUI.date_deadline = item.date_deadline;
                    if (item.date_start != null)
                    {
                        try
                        {
                            mrp_ProductionUI.date_start = DateTime.Parse((string)item.date_start);
                        }
                        catch
                        {

                        }

                    }
                    if (item.date_finished != null)
                    {
                        try
                        {
                            mrp_ProductionUI.date_finished = DateTime.Parse((string)item.date_finished);
                        }
                        catch
                        {

                        }

                    }
                    mrp_ProductionUI.create_date = item.create_date;
                    mrp_ProductionUI.write_date = item.write_date;
                    mrp_ProductionUI.product_uom_qty = item.product_uom_qty;
                    mrp_ProductionUI.analytic_account_id = item.analytic_account_id;
                    mrp_ProductionUI.extra_cost = item.extra_cost;
                    mrp_ProductionUI.x_Svn_customer_SN = item.x_Svn_customer_SN;

                    dataUIs.Add(mrp_ProductionUI);

                }
                return dataUIs;
            }
            catch
            {
                return null;
            }
        }
        private List<mrp_bomUI> ConverterToBomUI(object searchResult)
        {
            List<mrp_bomUI> dataUIs = new List<mrp_bomUI>();
            List<mrp_bom> baseData = new List<mrp_bom>();
            try
            {
                JArray jArray = JArray.FromObject(searchResult);
                var json = JsonConvert.SerializeObject(jArray);
                baseData = JsonConvert.DeserializeObject<List<mrp_bom>>(json);
                foreach (var item in baseData)
                {
                    mrp_bomUI dataUI = new mrp_bomUI();
                    dataUI.id = item.id;
                    if (item.message_main_attachment_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.message_main_attachment_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.message_main_attachment_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    dataUI.sequence = item.sequence;
                    if (item.product_tmpl_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.product_tmpl_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.product_tmpl_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.product_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.product_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.product_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.product_uom_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.product_uom_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.product_uom_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.picking_type_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.picking_type_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.picking_type_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.company_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.company_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.company_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.create_uid != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.create_uid;
                            var intTemp = (Int64)objects[0];
                            dataUI.create_uid = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.write_uid != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.write_uid;
                            var intTemp = (Int64)objects[0];
                            dataUI.write_uid = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.origin_message_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.origin_message_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.origin_message_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    dataUI.origin_references = item.origin_references;
                    dataUI.consumption = item.consumption;
                    dataUI.product_qty = item.product_qty;
                    dataUI.create_date = item.create_date;
                    dataUI.write_date = item.write_date;
                    dataUI.code = item.code;
                    dataUI.type = item.type;
                    dataUI.ready_to_produce = item.ready_to_produce;
                    dataUI.active = item.active;
                    dataUI.allow_operation_dependencies = item.allow_operation_dependencies;
                    dataUI.version = item.version;
                    if (item.previous_bom_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.previous_bom_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.previous_bom_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }

                    dataUIs.Add(dataUI);

                }
                return dataUIs;
            }
            catch
            {
                return null;
            }
        }
        private List<mrp_bom_lineUI> ConverterToBomLineUI(object searchResult)
        {
            List<mrp_bom_lineUI> dataUIs = new List<mrp_bom_lineUI>();
            List<mrp_bom_line> baseData = new List<mrp_bom_line>();
            try
            {
                JArray jArray = JArray.FromObject(searchResult);
                var json = JsonConvert.SerializeObject(jArray);
                baseData = JsonConvert.DeserializeObject<List<mrp_bom_line>>(json);
                foreach (var item in baseData)
                {
                    mrp_bom_lineUI dataUI = new mrp_bom_lineUI();
                    dataUI.id = item.id;
                    if (item.product_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.product_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.product_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.product_uom_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.product_uom_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.product_uom_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.bom_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.bom_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.bom_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.company_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.company_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.company_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.create_uid != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.create_uid;
                            var intTemp = (Int64)objects[0];
                            dataUI.create_uid = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.write_uid != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.write_uid;
                            var intTemp = (Int64)objects[0];
                            dataUI.write_uid = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    dataUI.product_qty = item.product_qty;
                    dataUI.create_date = item.create_date;
                    dataUI.write_date = item.write_date;
                    if (item.product_tmpl_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.product_tmpl_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.product_tmpl_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.operation_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.operation_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.operation_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    dataUI.manual_consumption = item.manual_consumption;
                    dataUI.cost_share = item.cost_share;
                    dataUI.standard_qty = item.standard_qty;
                    dataUI.loss_rate = item.loss_rate;
                    dataUIs.Add(dataUI);

                }
                return dataUIs;
            }
            catch
            {
                return null;
            }
        }
        private List<stock_lotUI> ConverterToStockLotUI(object searchResult)
        {
            List<stock_lotUI> dataUIs = new List<stock_lotUI>();
            List<stock_lot> baseData = new List<stock_lot>();
            try
            {
                JArray jArray = JArray.FromObject(searchResult);
                var json = JsonConvert.SerializeObject(jArray);
                baseData = JsonConvert.DeserializeObject<List<stock_lot>>(json);
                foreach (var item in baseData)
                {
                    stock_lotUI dataUI = new stock_lotUI();
                    dataUI.id = item.id;
                    if (item.message_main_attachment_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.message_main_attachment_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.message_main_attachment_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.product_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.product_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.product_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.product_uom_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.product_uom_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.product_uom_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.company_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.company_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.company_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.create_uid != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.create_uid;
                            var intTemp = (Int64)objects[0];
                            dataUI.create_uid = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.write_uid != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.write_uid;
                            var intTemp = (Int64)objects[0];
                            dataUI.write_uid = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    dataUI.create_date = item.create_date;
                    dataUI.write_date = item.write_date;
                    dataUI.origin_message_id = item.origin_message_id;
                    dataUI.origin_references = item.origin_references;
                    dataUI.name = item.name;
                    dataUI.Ref = item.Ref;
                    dataUI.note = item.note;
                    if (item.customer_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.customer_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.customer_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.supplier_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.supplier_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.supplier_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.country_state_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.country_state_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.country_state_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.equipment_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.equipment_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.equipment_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    dataUIs.Add(dataUI);

                }
                return dataUIs;
            }
            catch
            {
                return null;
            }
        }
        private List<product_categoryUI> ConverterToProductCatUI(object searchResult)
        {
            List<product_categoryUI> dataUIs = new List<product_categoryUI>();
            List<product_category> baseData = new List<product_category>();
            try
            {
                JArray jArray = JArray.FromObject(searchResult);
                var json = JsonConvert.SerializeObject(jArray);
                baseData = JsonConvert.DeserializeObject<List<product_category>>(json);
                foreach (var item in baseData)
                {
                    product_categoryUI dataUI = new product_categoryUI();
                    dataUI.id = item.id;
                    if (item.parent_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.parent_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.parent_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    dataUI.complete_name = item.complete_name;
                    dataUI.parent_path = item.parent_path;
                    if (item.message_main_attachment_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.message_main_attachment_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.message_main_attachment_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.create_uid != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.create_uid;
                            var intTemp = (Int64)objects[0];
                            dataUI.create_uid = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.write_uid != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.write_uid;
                            var intTemp = (Int64)objects[0];
                            dataUI.write_uid = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    dataUI.create_date = item.create_date;
                    dataUI.write_date = item.write_date;
                    if (item.origin_message_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.origin_message_id;
                            var intTemp = (String)objects[0];
                            dataUI.origin_message_id = (String)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.origin_references != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.origin_references;
                            var intTemp = (String)objects[0];
                            dataUI.origin_references = (String)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    dataUI.name = item.name;
                    if (item.removal_strategy_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.removal_strategy_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.removal_strategy_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    dataUI.packaging_reserve_method = item.packaging_reserve_method;
                    if (item.technician_user_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.technician_user_id;
                            var intTemp = (Int64)objects[0];
                            dataUI.technician_user_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    dataUI.equipment_assign_to = item.equipment_assign_to;
                    dataUIs.Add(dataUI);

                }
                return dataUIs;
            }
            catch
            {
                return null;
            }
        }
        private List<stock_moveUI> ConverterToStockMoveUI(object searchResult)
        {
            List<stock_moveUI> dataUIs = new List<stock_moveUI>();
            List<stock_move> baseData = new List<stock_move>();
            try
            {
                JArray jArray = JArray.FromObject(searchResult);
                var json = JsonConvert.SerializeObject(jArray);
                baseData = JsonConvert.DeserializeObject<List<stock_move>>(json);
                foreach (var item in baseData)
                {
                    stock_moveUI mrp_ProductionUI = new stock_moveUI();
                    mrp_ProductionUI.id = item.id;
                    if (item.product_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.product_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.product_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.product_uom != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.product_uom;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.product_uom = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.location_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.location_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.location_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.partner_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.partner_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.partner_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.picking_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.picking_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.picking_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.group_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.group_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.group_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.rule_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.rule_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.rule_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.picking_type_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.picking_type_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.picking_type_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.origin_returned_move_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.origin_returned_move_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.origin_returned_move_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.location_dest_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.location_dest_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.location_dest_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.restrict_partner_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.restrict_partner_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.restrict_partner_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.company_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.company_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.company_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.warehouse_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.warehouse_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.warehouse_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.orderpoint_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.orderpoint_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.orderpoint_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.package_level_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.package_level_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.package_level_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.create_uid != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.create_uid;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.create_uid = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.write_uid != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.write_uid;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.write_uid = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    mrp_ProductionUI.next_serial_count = item.next_serial_count;
                    if (item.product_packaging_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.product_packaging_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.product_packaging_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    mrp_ProductionUI.name = item.name;
                    mrp_ProductionUI.priority = item.priority;
                    mrp_ProductionUI.origin = item.origin;
                    mrp_ProductionUI.state = item.state;
                    mrp_ProductionUI.procure_method = item.procure_method;
                    mrp_ProductionUI.reference = item.reference;
                    if (item.next_serial != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.next_serial;
                            var intTemp = (String)objects[0];
                            mrp_ProductionUI.next_serial = (String)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    mrp_ProductionUI.product_qty = item.product_qty;
                    mrp_ProductionUI.reservation_date = item.reservation_date;
                    mrp_ProductionUI.propagate_cancel = item.propagate_cancel;
                    if (item.description_picking != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.description_picking;
                            var intTemp = (String)objects[0];
                            mrp_ProductionUI.description_picking = (String)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    mrp_ProductionUI.quantity_done = item.quantity_done;
                    mrp_ProductionUI.scrapped = item.scrapped;
                    mrp_ProductionUI.is_inventory = item.is_inventory;
                    mrp_ProductionUI.additional = item.additional;
                    mrp_ProductionUI.date_deadline = item.date_deadline;
                    
                    mrp_ProductionUI.create_date = item.create_date;
                    mrp_ProductionUI.write_date = item.write_date;
                    mrp_ProductionUI.product_uom_qty = item.product_uom_qty;
                    mrp_ProductionUI.date = item.date;
                    if (item.delay_alert_date != null)
                    {
                        try
                        {
                            mrp_ProductionUI.delay_alert_date = DateTime.Parse((string)item.delay_alert_date);
                        }
                        catch
                        {

                        }
                    }
                    mrp_ProductionUI.price_unit = item.price_unit;
                    mrp_ProductionUI.is_done = item.is_done;
                    mrp_ProductionUI.unit_factor = item.unit_factor;
                    if (item.created_production_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.created_production_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.created_production_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.raw_material_production_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.raw_material_production_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.raw_material_production_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.unbuild_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.unbuild_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.unbuild_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.consume_unbuild_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.consume_unbuild_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.consume_unbuild_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.operation_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.operation_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.operation_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.workorder_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.workorder_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.workorder_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.bom_line_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.bom_line_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.bom_line_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.byproduct_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.byproduct_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.byproduct_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    mrp_ProductionUI.order_finished_lot_id = item.order_finished_lot_id;
                    mrp_ProductionUI.cost_share = item.cost_share;
                    mrp_ProductionUI.manual_consumption = item.manual_consumption;
                    if (item.analytic_account_line_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.analytic_account_line_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.analytic_account_line_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.to_refund != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.to_refund;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.to_refund = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.purchase_line_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.purchase_line_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.purchase_line_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.created_purchase_line_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.created_purchase_line_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.created_purchase_line_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.component_standard_consumption_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.component_standard_consumption_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.component_standard_consumption_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.byproduct_standard_consumption_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.byproduct_standard_consumption_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.byproduct_standard_consumption_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    if (item.sale_line_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.sale_line_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.sale_line_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }
                    mrp_ProductionUI.sequence = item.sequence;
                    dataUIs.Add(mrp_ProductionUI);

                }
                return dataUIs;
            }
            catch
            {
                return null;
            }
        }
        private List<stock_move_lineUI> ConverterToStockMoveLineUI(object searchResult)
        {
            List<stock_move_lineUI> dataUIs = new List<stock_move_lineUI>();
            List<stock_move_line> baseData = new List<stock_move_line>();
            try
            {
                JArray jArray = JArray.FromObject(searchResult);
                var json = JsonConvert.SerializeObject(jArray);
                baseData = JsonConvert.DeserializeObject<List<stock_move_line>>(json);
                foreach (var item in baseData)
                {
                    stock_move_lineUI mrp_ProductionUI = new stock_move_lineUI();
                    mrp_ProductionUI.id = item.id;
                    mrp_ProductionUI.move_id = item.move_id;
                    if (item.product_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.product_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.product_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.product_uom_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.product_uom_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.product_uom_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.lot_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.lot_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.lot_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.workorder_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.workorder_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.workorder_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    mrp_ProductionUI.location_id = item.location_id;
                    mrp_ProductionUI.package_id = item.package_id;
                    mrp_ProductionUI.picking_id = item.picking_id;
                    mrp_ProductionUI.result_package_id = item.result_package_id;
                    mrp_ProductionUI.owner_id = item.owner_id;
                    mrp_ProductionUI.product_category_name = item.product_category_name;
                    mrp_ProductionUI.lot_name = item.lot_name;
                    mrp_ProductionUI.location_dest_id = item.location_dest_id;
                    mrp_ProductionUI.reserved_qty = item.reserved_qty;
                    mrp_ProductionUI.company_id = item.company_id;
                    mrp_ProductionUI.reserved_uom_qty = item.reserved_uom_qty;
                    mrp_ProductionUI.qty_done = item.qty_done;
                    mrp_ProductionUI.package_level_id = item.package_level_id;
                    mrp_ProductionUI.create_uid = item.create_uid;
                    mrp_ProductionUI.write_uid = item.write_uid;
                    mrp_ProductionUI.equipment_id = item.equipment_id;
                    mrp_ProductionUI.can_create_equipment = item.can_create_equipment;
                    mrp_ProductionUI.location_processed = item.location_processed;
                    mrp_ProductionUI.state = item.state;
                    mrp_ProductionUI.reference = item.reference;
                    mrp_ProductionUI.description_picking = item.description_picking;

                    mrp_ProductionUI.create_date = item.create_date;
                    mrp_ProductionUI.write_date = item.write_date;
                    mrp_ProductionUI.date = item.date;
                    mrp_ProductionUI.production_id = item.production_id;

                    dataUIs.Add(mrp_ProductionUI);

                }
                return dataUIs;
            }
            catch
            {
                return null;
            }
        }
        private List<stock_move_line_consume_relUI> ConverterToStockMoveLineConsumUI(object searchResult)
        {
            List<stock_move_line_consume_relUI> dataUIs = new List<stock_move_line_consume_relUI>();
            List<stock_move_line_consume_rel> baseData = new List<stock_move_line_consume_rel>();
            try
            {
                JArray jArray = JArray.FromObject(searchResult);
                var json = JsonConvert.SerializeObject(jArray);
                baseData = JsonConvert.DeserializeObject<List<stock_move_line_consume_rel>>(json);
                foreach (var item in baseData)
                {
                    stock_move_line_consume_relUI mrp_ProductionUI = new stock_move_line_consume_relUI();
                    if (item.consume_line_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.consume_line_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.consume_line_id = (int)intTemp;
                        }
                        catch
                        {

                        }

                    }
                    if (item.produce_line_id != null)
                    {
                        try
                        {
                            JArray objects = (JArray)item.produce_line_id;
                            var intTemp = (Int64)objects[0];
                            mrp_ProductionUI.produce_line_id = (int)intTemp;
                        }
                        catch
                        {

                        }
                    }

                    dataUIs.Add(mrp_ProductionUI);

                }
                return dataUIs;
            }
            catch
            {
                return null;
            }
        }

        private BODataProcessResult InsertProductionTemplateToSVNDB(List<product_templateUI> dataUI)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                List<product_templateUI> insertData = new List<product_templateUI>();
                List<product_templateUI> existData = new List<product_templateUI>();
                GrandDataPortal<product_templateUI> dataPortal = new GrandDataPortal<product_templateUI>("SVN_product_template_1", SVNDBConfig.ConnectionString);
                foreach (var item in dataUI)
                {
                    var existUI = dataPortal.GetDataByID(item.id);
                    if (existUI != null)
                    {
                        existData.Add(item);
                    }
                    else
                    {
                        insertData.Add(item);
                    }
                }
                if (insertData.Count > 0)
                {
                    string sqlQuery = "INSERT INTO [dbo].[SVN_product_template_1]([id],[message_main_attachment_id],[sequence],[categ_id],[uom_id],[uom_po_id],[company_id],[color],[create_uid],[write_uid],[origin_message_id],[origin_references],[detailed_type],[type],[default_code],[priority],[name],[description],[description_purchase],[description_sale],[list_price],[volume],[weight],[sale_ok],[purchase_ok],[active],[can_image_1024_be_zoomed],[has_configurable_attributes],[create_date],[write_date],[tracking],[description_picking],[description_pickingout],[description_pickingin],[sale_delay],[produce_delay],[days_to_prepare_mo],[purchase_method],[purchase_line_warn],[purchase_line_warn_msg],[service_type],[sale_line_warn],[expense_policy],[invoice_policy],[sale_line_warn_msg],[technician_user_id],[equipment_assign_to],[period_uom],[recurring_sale_price],[service_tracking])\r\n     VALUES(@id,@message_main_attachment_id,@sequence,@categ_id,@uom_id,@uom_po_id,@company_id,@color,@create_uid,@write_uid,@origin_message_id,@origin_references,@detailed_type,@type,@default_code,@priority,@name,@description,@description_purchase,@description_sale,@list_price,@volume,@weight,@sale_ok,@purchase_ok,@active,@can_image_1024_be_zoomed,@has_configurable_attributes,@create_date,@write_date,@tracking,@description_picking,@description_pickingout,@description_pickingin,@sale_delay,@produce_delay,@days_to_prepare_mo,@purchase_method,@purchase_line_warn,@purchase_line_warn_msg,@service_type,@sale_line_warn,@expense_policy,@invoice_policy,@sale_line_warn_msg,@technician_user_id,@equipment_assign_to,@period_uom,@recurring_sale_price,@service_tracking)";
                    var insertResult = dataPortal.InsertBulk(insertData, sqlQuery);
                    if (insertResult > 0)
                    {
                        processResult.OK = true;
                        processResult.Message = "Insert success";
                    }
                    else
                    {
                        processResult.Message = "Insert fail";
                    }
                }
                else
                {
                    processResult.Message = "All data existed";
                }

            }
            catch (Exception ex)
            {

            }
            return processResult;
        }
        private BODataProcessResult InsertProductionResultToSVNDB(List<mrp_productionUI> dataUI)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                List<mrp_productionUI> insertData = new List<mrp_productionUI>();
                List<mrp_productionUI> existData = new List<mrp_productionUI>();
                GrandDataPortal<mrp_productionUI> dataPortal = new GrandDataPortal<mrp_productionUI>("SVN_mrp_production_1", SVNDBConfig.ConnectionString);
                foreach (var item in dataUI)
                {
                    var existUI = dataPortal.GetDataByID(item.id);
                    if (existUI != null)
                    {
                        existData.Add(item);
                    }
                    else
                    {
                        insertData.Add(item);
                    }
                }
                if (insertData.Count > 0)
                {
                    string sqlQuery = "INSERT INTO [dbo].[SVN_mrp_production_1]([id],[message_main_attachment_id],[backorder_sequence],[product_id],[product_uom_id],[lot_producing_id],[picking_type_id],[location_src_id],[location_dest_id],[bom_id],[user_id],[company_id],[procurement_group_id],[orderpoint_id],[production_location_id],[create_uid],[write_uid],[origin_message_id],[origin_references],[name],[priority],[origin],[state],[reservation_state],[product_description_variants],[consumption],[product_qty],[qty_producing],[propagate_cancel],[is_locked],[is_planned],[allow_workorder_dependencies],[date_planned_start],[date_planned_finished],[date_deadline],[date_start],[date_finished],[create_date],[write_date],[product_uom_qty],[analytic_account_id],[extra_cost],[x_Svn_customer_SN])VALUES(@id,@message_main_attachment_id,@backorder_sequence,@product_id,@product_uom_id,@lot_producing_id,@picking_type_id,@location_src_id,@location_dest_id,@bom_id,@user_id,@company_id,@procurement_group_id,@orderpoint_id,@production_location_id,@create_uid,@write_uid,@origin_message_id,@origin_references,@name,@priority,@origin,@state,@reservation_state,@product_description_variants,@consumption,@product_qty,@qty_producing,@propagate_cancel,@is_locked,@is_planned,@allow_workorder_dependencies,@date_planned_start,@date_planned_finished,@date_deadline,@date_start,@date_finished,@create_date,@write_date,@product_uom_qty,@analytic_account_id,@extra_cost,@x_Svn_customer_SN)";
                    var insertResult = dataPortal.InsertBulk(insertData, sqlQuery);
                    if (insertResult > 0)
                    {
                        processResult.OK = true;
                        processResult.Message = "Insert success";
                    }
                    else
                    {
                        processResult.Message = "Insert fail";
                    }
                }
                else
                {
                    processResult.Message = "All data existed";
                }

            }
            catch (Exception ex)
            {

            }
            return processResult;
        }
        private BODataProcessResult InsertBOMToSVNDB(List<mrp_bomUI> dataUI)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                List<mrp_bomUI> insertData = new List<mrp_bomUI>();
                List<mrp_bomUI> existData = new List<mrp_bomUI>();
                GrandDataPortal<mrp_bomUI> dataPortal = new GrandDataPortal<mrp_bomUI>("SVN_mrp_bom_1", SVNDBConfig.ConnectionString);
                foreach (var item in dataUI)
                {
                    var existUI = dataPortal.GetDataByID(item.id);
                    if (existUI != null)
                    {
                        existData.Add(item);
                    }
                    else
                    {
                        insertData.Add(item);
                    }
                }
                if (insertData.Count > 0)
                {
                    string sqlQuery = "INSERT INTO [dbo].[SVN_mrp_bom_1]([id],[message_main_attachment_id],[product_tmpl_id],[product_id],[product_uom_id],[sequence],[picking_type_id],[company_id],[create_uid],[write_uid],[origin_message_id],[origin_references],[code],[type],[ready_to_produce],[consumption],[product_qty],[active],[allow_operation_dependencies],[create_date],[write_date],[version],[previous_bom_id])VALUES(@id,@message_main_attachment_id,@product_tmpl_id,@product_id,@product_uom_id,@sequence,@picking_type_id,@company_id,@create_uid,@write_uid,@origin_message_id,@origin_references,@code,@type,@ready_to_produce,@consumption,@product_qty,@active,@allow_operation_dependencies,@create_date,@write_date,@version,@previous_bom_id)";
                    var insertResult = dataPortal.InsertBulk(insertData, sqlQuery);
                    if (insertResult > 0)
                    {
                        processResult.OK = true;
                        processResult.Message = "Insert success";
                    }
                    else
                    {
                        processResult.Message = "Insert fail";
                    }
                }
                else
                {
                    processResult.Message = "All data existed";
                }

            }
            catch (Exception ex)
            {

            }
            return processResult;
        }
        private BODataProcessResult InsertBomLineToSVNDB(List<mrp_bom_lineUI> dataUI)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                List<mrp_bom_lineUI> insertData = new List<mrp_bom_lineUI>();
                List<mrp_bom_lineUI> existData = new List<mrp_bom_lineUI>();
                GrandDataPortal<mrp_bom_lineUI> dataPortal = new GrandDataPortal<mrp_bom_lineUI>("SVN_mrp_bom_line", SVNDBConfig.ConnectionString);
                foreach (var item in dataUI)
                {
                    var existUI = dataPortal.GetDataByID(item.id);
                    if (existUI != null)
                    {
                        existData.Add(item);
                    }
                    else
                    {
                        insertData.Add(item);
                    }
                }
                if (insertData.Count > 0)
                {
                    string sqlQuery = "INSERT INTO [dbo].[SVN_mrp_bom_line]([id],[product_id],[product_tmpl_id],[company_id],[product_uom_id],[sequence],[bom_id],[operation_id],[create_uid],[write_uid],[product_qty],[manual_consumption],[create_date],[write_date],[cost_share],[standard_qty],[loss_rate])VALUES(@id,@product_id,@product_tmpl_id,@company_id,@product_uom_id,@sequence,@bom_id,@operation_id,@create_uid,@write_uid,@product_qty,@manual_consumption,@create_date,@write_date,@cost_share,@standard_qty,@loss_rate)";
                    var insertResult = dataPortal.InsertBulk(insertData, sqlQuery);
                    if (insertResult > 0)
                    {
                        processResult.OK = true;
                        processResult.Message = "Insert success";
                    }
                    else
                    {
                        processResult.Message = "Insert fail";
                    }
                }
                else
                {
                    processResult.Message = "All data existed";
                }

            }
            catch (Exception ex)
            {

            }
            return processResult;
        }
        private BODataProcessResult InsertStockLotToSVNDB(List<stock_lotUI> dataUI)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                List<stock_lotUI> insertData = new List<stock_lotUI>();
                List<stock_lotUI> existData = new List<stock_lotUI>();
                GrandDataPortal<stock_lotUI> dataPortal = new GrandDataPortal<stock_lotUI>("SVN_stock_lot", SVNDBConfig.ConnectionString);
                foreach (var item in dataUI)
                {
                    var existUI = dataPortal.GetDataByID(item.id);
                    if (existUI != null)
                    {
                        existData.Add(item);
                    }
                    else
                    {
                        insertData.Add(item);
                    }
                }
                if (insertData.Count > 0)
                {
                    string sqlQuery = "INSERT INTO [dbo].[SVN_stock_lot]([id],[message_main_attachment_id],[product_id],[product_uom_id],[company_id],[create_uid],[write_uid],[origin_message_id],[origin_references],[name],[ref],[note],[create_date],[write_date],[customer_id],[supplier_id],[country_state_id],[equipment_id])VALUES(@id,@message_main_attachment_id,@product_id,@product_uom_id,@company_id,@create_uid,@write_uid,@origin_message_id,@origin_references,@name,@ref,@note,@create_date,@write_date,@customer_id,@supplier_id,@country_state_id,@equipment_id)";
                    var insertResult = dataPortal.InsertBulk(insertData, sqlQuery);
                    if (insertResult > 0)
                    {
                        processResult.OK = true;
                        processResult.Message = "Insert success";
                    }
                    else
                    {
                        processResult.Message = "Insert fail";
                    }
                }
                else
                {
                    processResult.Message = "All data existed";
                }

            }
            catch (Exception ex)
            {

            }
            return processResult;
        }
        private BODataProcessResult InsertProductCatToSVNDB(List<product_categoryUI> dataUI)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                List<product_categoryUI> insertData = new List<product_categoryUI>();
                List<product_categoryUI> existData = new List<product_categoryUI>();
                GrandDataPortal<product_categoryUI> dataPortal = new GrandDataPortal<product_categoryUI>("SVN_product_category", SVNDBConfig.ConnectionString);
                foreach (var item in dataUI)
                {
                    var existUI = dataPortal.GetDataByID(item.id);
                    if (existUI != null)
                    {
                        existData.Add(item);
                    }
                    else
                    {
                        insertData.Add(item);
                    }
                }
                if (insertData.Count > 0)
                {
                    string sqlQuery = "INSERT INTO [dbo].[SVN_product_category]([id],[parent_id],[create_uid],[write_uid],[name],[complete_name],[parent_path],[create_date],[write_date],[message_main_attachment_id],[origin_message_id],[origin_references],[removal_strategy_id],[packaging_reserve_method],[technician_user_id],[equipment_assign_to])VALUES(@id,@parent_id,@create_uid,@write_uid,@name,@complete_name,@parent_path,@create_date,@write_date,@message_main_attachment_id,@origin_message_id,@origin_references,@removal_strategy_id,@packaging_reserve_method,@technician_user_id,@equipment_assign_to)";
                    var insertResult = dataPortal.InsertBulk(insertData, sqlQuery);
                    if (insertResult > 0)
                    {
                        processResult.OK = true;
                        processResult.Message = "Insert success";
                    }
                    else
                    {
                        processResult.Message = "Insert fail";
                    }
                }
                else
                {
                    processResult.Message = "All data existed";
                }

            }
            catch (Exception ex)
            {

            }
            return processResult;
        }
        private BODataProcessResult InsertStockMoveToSVNDB(List<stock_moveUI> dataUI)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                List<stock_moveUI> insertData = new List<stock_moveUI>();
                List<stock_moveUI> existData = new List<stock_moveUI>();
                GrandDataPortal<stock_moveUI> dataPortal = new GrandDataPortal<stock_moveUI>("SVN_stock_move", SVNDBConfig.ConnectionString);
                foreach (var item in dataUI)
                {
                    var existUI = dataPortal.GetDataByID(item.id);
                    if (existUI != null)
                    {
                        existData.Add(item);
                    }
                    else
                    {
                        insertData.Add(item);
                    }
                }
                if (insertData.Count > 0)
                {
                    string sqlQuery = "INSERT INTO [dbo].[SVN_stock_move]([id],[sequence],[company_id],[product_id],[product_uom],[location_id],[location_dest_id],[partner_id],[picking_id],[group_id],[rule_id],[picking_type_id],[origin_returned_move_id],[restrict_partner_id],[warehouse_id],[package_level_id],[next_serial_count],[orderpoint_id],[product_packaging_id],[create_uid],[write_uid],[name],[priority],[state],[origin],[procure_method],[reference],[next_serial],[reservation_date],[description_picking],[product_qty],[product_uom_qty],[quantity_done],[scrapped],[propagate_cancel],[is_inventory],[additional],[date],[date_deadline],[delay_alert_date],[create_date],[write_date],[price_unit],[is_done],[unit_factor],[created_production_id],[production_id],[raw_material_production_id],[unbuild_id],[consume_unbuild_id],[operation_id],[workorder_id],[bom_line_id],[byproduct_id],[order_finished_lot_id],[cost_share],[manual_consumption],[analytic_account_line_id],[to_refund],[purchase_line_id],[created_purchase_line_id],[component_standard_consumption_id],[byproduct_standard_consumption_id],[sale_line_id])VALUES(@id,@sequence,@company_id,@product_id,@product_uom,@location_id,@location_dest_id,@partner_id,@picking_id,@group_id,@rule_id,@picking_type_id,@origin_returned_move_id,@restrict_partner_id,@warehouse_id,@package_level_id,@next_serial_count,@orderpoint_id,@product_packaging_id,@create_uid,@write_uid,@name,@priority,@state,@origin,@procure_method,@reference,@next_serial,@reservation_date,@description_picking,@product_qty,@product_uom_qty,@quantity_done,@scrapped,@propagate_cancel,@is_inventory,@additional,@date,@date_deadline,@delay_alert_date,@create_date,@write_date,@price_unit,@is_done,@unit_factor,@created_production_id,@production_id,@raw_material_production_id,@unbuild_id,@consume_unbuild_id,@operation_id,@workorder_id,@bom_line_id,@byproduct_id,@order_finished_lot_id,@cost_share,@manual_consumption,@analytic_account_line_id,@to_refund,@purchase_line_id,@created_purchase_line_id,@component_standard_consumption_id,@byproduct_standard_consumption_id,@sale_line_id)";
                    var insertResult = dataPortal.InsertBulk(insertData, sqlQuery);
                    if (insertResult > 0)
                    {
                        processResult.OK = true;
                        processResult.Message = "Insert success";
                    }
                    else
                    {
                        processResult.Message = "Insert fail";
                    }
                }
                else
                {
                    processResult.Message = "All data existed";
                }

            }
            catch (Exception ex)
            {

            }
            return processResult;
        }
        private BODataProcessResult InsertStockMoveLineToSVNDB(List<stock_move_lineUI> dataUI)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                List<stock_move_lineUI> insertData = new List<stock_move_lineUI>();
                List<stock_move_lineUI> existData = new List<stock_move_lineUI>();
                GrandDataPortal<stock_move_lineUI> dataPortal = new GrandDataPortal<stock_move_lineUI>("SVN_stock_move_line", SVNDBConfig.ConnectionString);
                foreach (var item in dataUI)
                {
                    var existUI = dataPortal.GetDataByID(item.id);
                    if (existUI != null)
                    {
                        existData.Add(item);
                    }
                    else
                    {
                        insertData.Add(item);
                    }
                }
                if (insertData.Count > 0)
                {
                    string sqlQuery = "INSERT INTO [dbo].[SVN_stock_move_line]([id],[picking_id],[move_id],[company_id],[product_id],[product_uom_id],[package_id],[package_level_id],[lot_id],[result_package_id],[owner_id],[location_id],[location_dest_id],[create_uid],[write_uid],[product_category_name],[lot_name],[state],[reference],[description_picking],[reserved_qty],[reserved_uom_qty],[qty_done],[date],[create_date],[write_date],[workorder_id],[production_id],[equipment_id],[can_create_equipment],[location_processed])VALUES(@id,@picking_id,@move_id,@company_id,@product_id,@product_uom_id,@package_id,@package_level_id,@lot_id,@result_package_id,@owner_id,@location_id,@location_dest_id,@create_uid,@write_uid,@product_category_name,@lot_name,@state,@reference,@description_picking,@reserved_qty,@reserved_uom_qty,@qty_done,@date,@create_date,@write_date,@workorder_id,@production_id,@equipment_id,@can_create_equipment,@location_processed)";
                    var insertResult = dataPortal.InsertBulk(insertData, sqlQuery);
                    if (insertResult > 0)
                    {
                        processResult.OK = true;
                        processResult.Message = "Insert success";
                    }
                    else
                    {
                        processResult.Message = "Insert fail";
                    }
                }
                else
                {
                    processResult.Message = "All data existed";
                }

            }
            catch (Exception ex)
            {

            }
            return processResult;
        }
        private BODataProcessResult InsertStockMoveLineConsumToSVNDB(List<stock_move_line_consume_relUI> dataUI)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                List<stock_move_line_consume_relUI> insertData = new List<stock_move_line_consume_relUI>();
                List<stock_move_line_consume_relUI> existData = new List<stock_move_line_consume_relUI>();
                stock_move_line_consume_relĐataPortal dataPortal = new stock_move_line_consume_relĐataPortal(SVNDBConfig.ConnectionString);
                foreach (var item in dataUI)
                {
                    var existUI = dataPortal.GetDataByConsumeLineIDAndProduceLineID(item.consume_line_id, item.produce_line_id);
                    if (existUI != null)
                    {
                        existData.Add(item);
                    }
                    else
                    {
                        insertData.Add(item);
                    }
                }
                if (insertData.Count > 0)
                {
                    GrandDataPortal<stock_move_line_consume_relUI> grandDataPortal = new GrandDataPortal<stock_move_line_consume_relUI>("SVN_stock_move_line_consume_rel", SVNDBConfig.ConnectionString);
                    string sqlQuery = "INSERT INTO [dbo].[SVN_stock_move_line_consume_rel]([consume_line_id],[produce_line_id])VALUES(@consume_line_id,@produce_line_id)";
                    var insertResult = grandDataPortal.InsertBulk(insertData, sqlQuery);
                    if (insertResult > 0)
                    {
                        processResult.OK = true;
                        processResult.Message = "Insert success";
                    }
                    else
                    {
                        processResult.Message = "Insert fail";
                    }
                }
                else
                {
                    processResult.Message = "All data existed";
                }

            }
            catch (Exception ex)
            {

            }
            return processResult;
        }

        private BODataProcessResult CallSPToUpdateResult()
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                GrandDataPortal<mrp_productionUI> dataPortal = new GrandDataPortal<mrp_productionUI>("SVN_mrp_production_1", SVNDBConfig.ConnectionString);
                string storedProcedure = "SVN_Update_result_Viindoo";
                DynamicParameters parameters = new DynamicParameters();
                processResult = dataPortal.CallStoredProcedure(storedProcedure, parameters);
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        private BODataProcessResult UploadDataToSVNServer(string tableName, object data)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            string className = "SVNShareLib.BaseObject." + tableName.Replace(".", "_");
            var type = System.Type.GetType(className);
            if (type == null)
            {
                Assembly assembly = Assembly.GetExecutingAssembly(); // Or load a specific assembly
                type = assembly.GetType(className);
            }
            object instance = Activator.CreateInstance(type);
            return processResult;
        }
    }
}
