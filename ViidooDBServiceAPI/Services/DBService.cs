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
                    dataUI.message_main_attachment_id = item.message_main_attachment_id;
                    dataUI.product_tmpl_id = item.product_tmpl_id;
                    dataUI.sequence = item.sequence;
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
                    dataUI.picking_type_id = item.picking_type_id;
                    dataUI.company_id = item.company_id;
                    dataUI.create_uid = item.create_uid;
                    dataUI.write_uid = item.write_uid;
                    dataUI.origin_message_id = item.origin_message_id;
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
                    dataUI.previous_bom_id = item.previous_bom_id;

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
                    dataUI.company_id = item.company_id;
                    dataUI.create_uid = item.create_uid;
                    dataUI.write_uid = item.write_uid;
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
                    dataUI.operation_id = item.operation_id;
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
                    dataUI.message_main_attachment_id = item.message_main_attachment_id;
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
                    dataUI.company_id = item.company_id;
                    dataUI.create_uid = item.create_uid;
                    dataUI.write_uid = item.write_uid;
                    dataUI.create_date = item.create_date;
                    dataUI.write_date = item.write_date;
                    dataUI.origin_message_id = item.origin_message_id;
                    dataUI.origin_references = item.origin_references;
                    dataUI.name = item.name;
                    dataUI.Ref = item.Ref;
                    dataUI.note = item.note;
                    dataUI.customer_id = item.customer_id;
                    dataUI.supplier_id = item.supplier_id;
                    dataUI.country_state_id = item.country_state_id;
                    dataUI.equipment_id = item.equipment_id;
                    dataUIs.Add(dataUI);

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
