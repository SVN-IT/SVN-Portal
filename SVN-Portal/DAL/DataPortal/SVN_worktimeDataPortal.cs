using SVNShareLib.DTO;
using System.Data.SqlClient;
using System.Data;
using Dapper;
using SVN_Portal.DAL.DTO;

namespace SVN_Portal.DAL.DataPortal
{
    public class SVN_worktimeDataPortal
    {
        string connectionString;
        public SVN_worktimeDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public SVN_worktimeUI GetWorkingtiem(List<int> product_id, string strDate)
        {
            mrp_productionUI dataUI = new mrp_productionUI();
            try
            {

                var date = DateTime.ParseExact(strDate, "yyyyMMdd", null);
                var start = date;
                var end = date.AddDays(1);

                string sql = @"
                SELECT product_id, first_finish_date_time, last_finish_date_time
                FROM your_table
                WHERE 
                    ((first_finish_date_time >= @start AND first_finish_date_time < @end)
                    OR (last_finish_date_time >= @start AND last_finish_date_time < @end))
                    AND product_id IN @product_id";

                var param = new { product_id = product_id, start = start, end = end };
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    var data = connection.QueryFirstOrDefault<SVN_worktimeUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    return data;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
