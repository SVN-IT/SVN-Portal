using PrinterServices.Objects;
using System.Data.SqlClient;
using System.Data;
using Dapper;
using SVN_Portal.DAL.DTO;

namespace SVN_Portal.DAL.DataPortal
{
    public class SVN_label_templateDataPortal
    {
        string connectionString;
        public SVN_label_templateDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }
        public async Task<SVN_label_templateUI> ReadByID(string partNumber, string type, string operation, string tableName = "SVN_label_template")
        {
            SVN_label_templateUI dataUI = new SVN_label_templateUI();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from " + tableName + " Where partNumber = @partNumber AND type = @type AND operation = @operation";
                    param = new { partNumber = partNumber, type = type, operation = operation };
                    var data = await conn.QueryFirstOrDefaultAsync<SVN_label_templateUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    dataUI = data;
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
