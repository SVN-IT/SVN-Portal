using Dapper;
using SVN_Portal.DAL.DTO;
using System.Data.SqlClient;
using System.Data;

namespace SVN_Portal.DAL.DataPortal
{
    public class SVN_Scan_Code_InfoDataPortal
    {
        string connectionString;
        public SVN_Scan_Code_InfoDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<SVN_Scan_Code_InfoUI> ReadByCode(string code)
        {
            SVN_Scan_Code_InfoUI dataUI = new SVN_Scan_Code_InfoUI();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from SVN_Scan_Code_Info where Code = @code";
                    param = new { code = code };
                    var data = await conn.QueryFirstOrDefaultAsync<SVN_Scan_Code_InfoUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
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
