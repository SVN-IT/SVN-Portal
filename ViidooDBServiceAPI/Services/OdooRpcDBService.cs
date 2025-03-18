using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OdooRpc.CoreCLR.Client;
using OdooRpc.CoreCLR.Client.Models;
using OdooRpc.CoreCLR.Client.Models.Parameters;
using SVNShareLib;
using SVNShareLib.BaseObject;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
namespace ViidooDBServiceAPI.Services
{
    public class OdooRpcDBService
    {
        ViindooDBConfig dBConfig;
        SVNDBConfig SVNDBConfig;
        private static string serverUrl;
        private static string dbName;
        private static string username;
        private static string password;
        public OdooRpcDBService(ViindooDBConfig dBConfig, SVNDBConfig SVNDBConfig)
        {
            this.dBConfig = dBConfig;
            serverUrl = dBConfig.ServerUrl;
            dbName = dBConfig.DbName;
            username = dBConfig.Username;
            password = dBConfig.Password;
            this.SVNDBConfig = SVNDBConfig;
        }

        public async Task<BODataProcessResult> ConnectDB()
        {
            BODataProcessResult dataProcessResult = new BODataProcessResult();
            try
            {
                OdooConnectionInfo odooConnection = new OdooConnectionInfo()
                {
                    Host = serverUrl,
                    Database = dbName,
                    Username = username,
                    Password = password,
                    Port = 443,
                    IsSSL = true
                };
                var client = new SVNOdooRpcClient(odooConnection);

                await client.Authenticate();

                if (client.SessionInfo.IsLoggedIn)
                {
                    dataProcessResult.OK = true;
                    dataProcessResult.Message = "Login success";
                    dataProcessResult.OdooUserID = client.SessionInfo.UserId.Value;
                    dataProcessResult.Content = client;
                }
                else
                {
                    dataProcessResult.Message = "Login fail";
                }
            }
            catch(Exception ex)
            {
                dataProcessResult.Message = ex.Message;
            }
            return dataProcessResult;
        }

        public async Task<List<BODataProcessResult>> GetData()
        {
            List<BODataProcessResult> processResults = new List<BODataProcessResult>();
            foreach (var item in dBConfig.QueryConfig)
            {
                BODataProcessResult processResult = new BODataProcessResult();
                processResult.DataType = item.TableName;
                try
                {
                    var connectResult = await ConnectDB();
                    if (connectResult.OK)
                    {
                        var domain = item.Domain.Split(",");
                        var fields = item.Fields.Split(",");
                        SVNOdooRpcClient client = (SVNOdooRpcClient)connectResult.Content;

                        OdooDomainFilter domainFilter = new OdooDomainFilter();

                        if(domain.Count() > 0)
                        {
                            domainFilter = domainFilter.Filter(domain[0], domain[1], domain[2]);
                        }

                        OdooFieldParameters odooFieldParam = new OdooFieldParameters(fields);

                        OdooPaginationParameters odooPaginationParam = new OdooPaginationParameters();
                        //{
                        //    Limit = 10
                        //};
                        //odooPaginationParam = odooPaginationParam.OrderByDescending("date_finished");

                        var productions = await client.GetSVNAll<mrp_productionUI[]>(
                            item.TableName,
                            domainFilter,
                            odooFieldParam,
                            odooPaginationParam);
                    }
                    else
                    {
                        processResult.Message = connectResult.Message;
                    }
                }
                catch(Exception ex)
                {
                    processResult.Message = ex.Message;
                }
                processResults.Add(processResult);
            }
            return processResults;
        }

        public async Task<BODataProcessResult> GetProductionResultData(string tableName = "mrp.production")
        {
            BODataProcessResult processResult = new BODataProcessResult();
            var item = dBConfig.QueryConfig.FirstOrDefault(x => x.TableName == tableName);
            processResult.DataType = item.TableName;
            try
            {
                var connectResult = await ConnectDB();
                if (connectResult.OK)
                {
                    string[] domain = new string[] { };
                    if (!string.IsNullOrWhiteSpace(item.Domain))
                    {
                        domain = item.Domain.Split(",");
                    }
                    string[] fields = new string[] { };
                    if (!string.IsNullOrWhiteSpace(item.Fields))
                    {
                        fields = item.Fields.Split(",");
                    }
                    SVNOdooRpcClient client = (SVNOdooRpcClient)connectResult.Content;

                    OdooDomainFilter domainFilter = new OdooDomainFilter();

                    if (domain.Count() >= 3)
                    {
                        domainFilter = domainFilter.Filter(domain[0], domain[1], domain[2]);
                    }

                    OdooFieldParameters odooFieldParam = new OdooFieldParameters(fields);

                    OdooPaginationParameters odooPaginationParam = new OdooPaginationParameters();
                    if(item.Limit > 0)
                    {
                        odooPaginationParam.Limit = item.Limit;
                    }
                    if (!string.IsNullOrWhiteSpace(item.Order))
                    {
                        odooPaginationParam = odooPaginationParam.OrderByDescending(item.Order);
                    }

                    var productions = await client.GetSVNAll<mrp_production[]>(
                        item.TableName,
                        domainFilter,
                        odooFieldParam,
                        odooPaginationParam);

                    //chuẩn bị dữ liệu để insert vào db
                    var dataUI = ConverterToUI(productions.ToList());
                    BODataProcessResult insertResult = new BODataProcessResult();
                    if(dataUI != null && dataUI.Count > 0)
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
                    processResult.Message = connectResult.Message;
                }
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        private List<mrp_productionUI> ConverterToUI(List<mrp_production> mrp_Productions)
        {
            List<mrp_productionUI> mrp_ProductionUIs = new List<mrp_productionUI>();
            try
            {
                foreach (var item in mrp_Productions)
                {
                    mrp_productionUI mrp_ProductionUI = new mrp_productionUI();
                    mrp_ProductionUI.id = item.id;
                    mrp_ProductionUI.message_main_attachment_id = item.message_main_attachment_id;
                    mrp_ProductionUI.backorder_sequence = item.backorder_sequence;
                    if(item.product_id != null && item.product_id.Count() > 0)
                    {
                        var intTemp = (Int64)item.product_id[0];
                        mrp_ProductionUI.product_id = (int)intTemp;
                    }
                    if (item.product_uom_id != null && item.product_uom_id.Count() > 0)
                    {
                        var intTemp = (Int64)item.product_uom_id[0];
                        mrp_ProductionUI.product_uom_id = (int)intTemp;
                    }
                    if (item.lot_producing_id != null && item.lot_producing_id.Count() > 0)
                    {
                        var intTemp = (Int64)item.lot_producing_id[0];
                        mrp_ProductionUI.lot_producing_id = (int)intTemp;
                    }
                    mrp_ProductionUI.picking_type_id = item.picking_type_id;
                    mrp_ProductionUI.location_src_id = item.location_src_id;
                    mrp_ProductionUI.location_dest_id = item.location_dest_id;
                    if (item.bom_id != null && item.bom_id.Count() > 0)
                    {
                        var intTemp = (Int64)item.bom_id[0];
                        mrp_ProductionUI.bom_id = (int)intTemp;
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
                    if(item.date_start != null)
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

                    mrp_ProductionUIs.Add(mrp_ProductionUI);

                }
                return mrp_ProductionUIs;
            }
            catch
            {
                return null;
            }
        }

        private BODataProcessResult InsertProductionResultToSVNDB(List<mrp_productionUI> dataUI)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                List<mrp_productionUI> insertData = new List<mrp_productionUI>();
                List<mrp_productionUI> existData = new List<mrp_productionUI>();
                mrp_productionDataPortal dataPortal = new mrp_productionDataPortal(SVNDBConfig.ConnectionString);
                foreach(var item in dataUI)
                {
                    var existUI = dataPortal.GetDataByID(item.id);
                    if(existUI != null)
                    {
                        existData.Add(item);
                    }
                    else
                    {
                        insertData.Add(item);
                    }
                }
                if(insertData.Count > 0)
                {
                    var insertResult = dataPortal.InsertBulk(insertData);
                    if(insertResult > 0)
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
            catch(Exception ex)
            {

            }
            return processResult;
        }

        private BODataProcessResult CallSPToUpdateResult()
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                mrp_productionDataPortal dataPortal = new mrp_productionDataPortal(SVNDBConfig.ConnectionString);
                processResult = dataPortal.CallUpdateResutl();
            }
            catch(Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return processResult;
        }
    }
}
