using SVN_Portal.DAL.DTO;
using System.Data.SqlClient;
using System.Data;
using Dapper;

namespace SVN_Portal.DAL.DataPortal
{
    public class SVN_quantity_reasonDataPortal
    {
        string connectionString;
        public SVN_quantity_reasonDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<SVN_quantity_reasonUI>> ReadList()
        {
            List<SVN_quantity_reasonUI> dataUI = new List<SVN_quantity_reasonUI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from SVN_quality_reason";
                    var data = await conn.QueryAsync<SVN_quantity_reasonUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
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
