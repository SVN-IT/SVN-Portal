using SVN_Portal.DAL.DTO;
using System.Data.SqlClient;
using System.Data;
using Dapper;

namespace SVN_Portal.DAL.DataPortal
{
    public class SVN_InputItemsStatusDataPortal
    {
        string connectionString;
        public SVN_InputItemsStatusDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<SVN_InputItemsStatusUI>> ReadList(string date, string operation, string tableName = "SVN_InputItemStatus")
        {
            List<SVN_InputItemsStatusUI> dataUI = new List<SVN_InputItemsStatusUI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from " + tableName + " where Datetime = @date";
                    param = new { date = date };
                    var data = await conn.QueryAsync<SVN_InputItemsStatusUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    dataUI = data.ToList();
                    if (dataUI != null && !string.IsNullOrWhiteSpace(operation))
                    {
                        dataUI = dataUI.Where(x => x.Line.Contains(operation)).ToList();
                    }
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
