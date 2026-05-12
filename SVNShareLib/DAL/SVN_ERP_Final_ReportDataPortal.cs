using SVNShareLib.DTO;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace SVNShareLib.DAL
{
    public class SVN_ERP_Final_ReportDataPortal
    {
        string connectionString;
        public SVN_ERP_Final_ReportDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<workOrderInfoUI>> GetDataByCondition(string conditionString)
        {
            try
            {
                string sql = "SELECT * FROM SVN_ERP_Final_Report";
                if (!string.IsNullOrEmpty(conditionString))
                {
                    sql += " WHERE " + conditionString;
                }
                var param = new object();
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    var data = await connection.QueryAsync<workOrderInfoUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    return data.ToList();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
