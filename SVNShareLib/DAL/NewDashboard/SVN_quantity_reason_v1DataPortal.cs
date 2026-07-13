using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SVNShareLib.DTO.NewDashboard;
using Dapper;

namespace SVNShareLib.DAL.NewDashboard
{
    public class SVN_quantity_reason_v1DataPortal
    {
        string connectionString;
        public SVN_quantity_reason_v1DataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<SVN_quantity_reason_v1UI>> ReadList()
        {
            List<SVN_quantity_reason_v1UI> dataUI = new List<SVN_quantity_reason_v1UI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from SVN_quality_reason";
                    var data = await conn.QueryAsync<SVN_quantity_reason_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
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
