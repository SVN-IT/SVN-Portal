using Dapper;
using SVNShareLib.DTO.NewDashboard;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SVNShareLib.DAL.NewDashboard
{
    public class SVN_Sync_result_DataPortal
    {
        string connectionString;
        public SVN_Sync_result_DataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task SyncResult(int number_date = 10)
        {
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    //Gọi thủ tục tính toán kết quả theo tarhet
                    string storedProcedure = "SVN_Sync_result data_date";
                    DynamicParameters parameters = new DynamicParameters();
                    parameters.Add("number_date", number_date);

                    var datas = await conn.QueryAsync(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
                }
            }
            catch
            {
                
            }
        }
    }
}
