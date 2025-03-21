using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Newtonsoft.Json.Linq;
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
        ConvertDataService convertDataService;
        private static string serverUrl;
        private static string dbName;
        private static string username;
        private static string password;
        public OdooRpcDBService(ViindooDBConfig dBConfig, SVNDBConfig SVNDBConfig, ConvertDataService convertDataService)
        {
            this.dBConfig = dBConfig;
            serverUrl = dBConfig.OdooServerUrl;
            dbName = dBConfig.DbName;
            username = dBConfig.Username;
            password = dBConfig.Password;
            this.SVNDBConfig = SVNDBConfig;
            this.convertDataService = convertDataService;
        }

        private async Task<BODataProcessResult> ConnectDB()
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
                        string[] order = item.Order.Split(" ");
                        if (item.Order.Contains("desc"))
                        {
                            odooPaginationParam = odooPaginationParam.OrderByDescending(order[0]);
                        }
                        else
                        {
                            odooPaginationParam = odooPaginationParam.OrderBy(item.Order);
                        }
                    }

                    var productions = await client.GetSVNAll<mrp_production[]>(
                        item.TableName,
                        domainFilter,
                        odooFieldParam,
                        odooPaginationParam);

                    //chuẩn bị dữ liệu để insert vào db
                    var dataUI = convertDataService.ConverterToProductionUI(productions.ToList());
                    BODataProcessResult insertResult = new BODataProcessResult();
                    if(dataUI != null && dataUI.Count > 0)
                    {
                        //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                        insertResult = convertDataService.InsertProductionResultToSVNDB(dataUI);
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

        public async Task<BODataProcessResult> GetStockMoveData(string tableName = "stock.move")
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
                    if (item.Limit > 0)
                    {
                        odooPaginationParam.Limit = item.Limit;
                    }
                    if (!string.IsNullOrWhiteSpace(item.Order))
                    {
                        string[] order = item.Order.Split(" ");
                        if (item.Order.Contains("desc"))
                        {
                            odooPaginationParam = odooPaginationParam.OrderByDescending(order[0]);
                        }
                        else
                        {
                            odooPaginationParam = odooPaginationParam.OrderBy(item.Order);
                        }
                    }

                    var productions = await client.GetSVNAll<mrp_production[]>(
                        item.TableName,
                        domainFilter,
                        odooFieldParam,
                        odooPaginationParam);

                    //chuẩn bị dữ liệu để insert vào db
                    var dataUI = convertDataService.ConverterToStockMoveUI(productions.ToList());
                    BODataProcessResult insertResult = new BODataProcessResult();
                    if (dataUI != null && dataUI.Count > 0)
                    {
                        //Thực hiện insert dữ liệu chưa tồn tại trong SVNDB
                        insertResult = convertDataService.InsertStockMoveToSVNDB(dataUI);
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
