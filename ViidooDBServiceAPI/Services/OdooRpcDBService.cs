using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OdooRpc.CoreCLR.Client;
using OdooRpc.CoreCLR.Client.Models;
using OdooRpc.CoreCLR.Client.Models.Parameters;
using SVNShareLib;
using SVNShareLib.DTO;
namespace ViidooDBServiceAPI.Services
{
    public class OdooRpcDBService
    {
        ViindooDBConfig dBConfig;
        private static string serverUrl;
        private static string dbName;
        private static string username;
        private static string password;
        public OdooRpcDBService(ViindooDBConfig dBConfig)
        {
            this.dBConfig = dBConfig;
            serverUrl = dBConfig.ServerUrl;
            dbName = dBConfig.DbName;
            username = dBConfig.Username;
            password = dBConfig.Password;
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
                        domainFilter = domainFilter.Filter(domain[0], domain[1], domain[2]);

                        OdooFieldParameters odooFieldParam = new OdooFieldParameters(fields);

                        OdooPaginationParameters odooPaginationParam = new OdooPaginationParameters()
                        {
                            Limit = 10
                        };
                        odooPaginationParam = odooPaginationParam.OrderByDescending("date_finished");

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
    }
}
