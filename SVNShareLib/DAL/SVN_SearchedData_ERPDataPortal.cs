using Dapper;
using SVNShareLib.DTO;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DAL
{
    public class SVN_SearchedData_ERPDataPortal
    {
        string connectionString;
        public SVN_SearchedData_ERPDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<SVN_SearchedData_ERPUI>> GetERPSearchedData(string savedSearchID = "", string fromDate = "", string toDate = "")
        {
            try
            {
                string sql = "SELECT * FROM SVN_SearchedData_ERP";
                if(!string.IsNullOrWhiteSpace(savedSearchID))
                {
                    sql = sql + " WHERE SavedSearchID = @savedSearchID";
                }
                if (!string.IsNullOrWhiteSpace(fromDate) && !string.IsNullOrWhiteSpace(toDate))
                {
                    sql = sql + "  and Date_time Between @fromDate And @toDate";
                }

                var param = new { savedSearchID = savedSearchID, fromDate = fromDate, toDate = toDate };
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    var data = await connection.QueryAsync<SVN_SearchedData_ERPUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
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
