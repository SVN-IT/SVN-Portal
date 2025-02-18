using Dapper;
using SVN_Portal.DAL.DTO;
using SVN_Portal.Models;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
namespace SVN_Portal.DAL.DataPortal
{
    public class SVN_Defect_recordDataPortal
    {
        string connectionString;
        public SVN_Defect_recordDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<SVN_Defect_record>> ReadList(string date)
        {
            List<SVN_Defect_record> dataUI = new List<SVN_Defect_record>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from SVN_Defect_Record where INSDatetime = @date";
                    param = new { date = date };
                    var data = await conn.QueryAsync<SVN_Defect_record>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
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
