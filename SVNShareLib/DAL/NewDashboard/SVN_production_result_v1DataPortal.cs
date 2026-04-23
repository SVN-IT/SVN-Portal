using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using SVNShareLib.DTO.NewDashboard;

namespace SVNShareLib.DAL.NewDashboard
{
    public class SVN_production_result_v1DataPortal
    {
        string connectionString;
        public SVN_production_result_v1DataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<SVN_production_result_v1UI>> ReadList(string date, string tableName = "SVN_Production_result_Viindoo")
        {
            List<SVN_production_result_v1UI> dataUI = new List<SVN_production_result_v1UI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from " + tableName + " where Date_time = @date";
                    param = new { date };
                    var data = await conn.QueryAsync<SVN_production_result_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    dataUI = data.ToList();
                }
                return dataUI;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<SVN_production_result_v1UI>> ReadListByOperationsRunning(string date, string tableName = "SVN_Production_result_Viindoo")
        {
            List<SVN_production_result_v1UI> dataUI = new List<SVN_production_result_v1UI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from " + tableName + " where Date_time = @date AND Type_value = @type_value AND (Time1 != 0 OR Time2 != 0 OR Time3 != 0 OR Time4 != 0 OR Time5 != 0)";
                    param = new { date, type_value = "Target" };
                    var data = await conn.QueryAsync<SVN_production_result_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    dataUI = data.ToList();
                    if (dataUI != null)
                    {
                        List<string> operations = dataUI.Select(x => x.Operation).Distinct().ToList();
                        if (operations != null && operations.Count > 0)
                        {
                            sql = "select * from " + tableName + " where Date_time = @date AND Operation IN @operations";
                            param = new { date, operations };
                            var data1 = await conn.QueryAsync<SVN_production_result_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                            dataUI = data1.ToList();
                        }
                    }
                }
                return dataUI;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<SVN_production_result_v1UI>> ReadListByOperAndWC(string date, string oper, string WC = "", string tableName = "SVN_Production_result_Viindoo")
        {
            List<SVN_production_result_v1UI> dataUI = new List<SVN_production_result_v1UI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from " + tableName + " where Date_time = @date AND Operation = @oper";
                    if (!string.IsNullOrWhiteSpace(WC))
                    {
                        sql += " AND WC = @WC";
                        param = new { date, oper, WC };
                    }
                    else
                    {
                        param = new { date, oper };
                    }
                    var data = await conn.QueryAsync<SVN_production_result_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    dataUI = data.ToList();
                }
                return dataUI;
            }
            catch
            {
                return null;
            }
        }
    }
}
