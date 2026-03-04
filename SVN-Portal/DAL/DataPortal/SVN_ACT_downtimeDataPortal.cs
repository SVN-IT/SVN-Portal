using Dapper;
using SVN_Portal.DAL.DTO;
using System.Data.SqlClient;
using System.Data;

namespace SVN_Portal.DAL.DataPortal
{
    public class SVN_ACT_downtimeDataPortal
    {
        string connectionString;
        public SVN_ACT_downtimeDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<SVN_ACT_downtimeUI>> ReadList(string storedProceduce = "SVN_ACT_downtime")
        {
            List<SVN_ACT_downtimeUI> dataUI = new List<SVN_ACT_downtimeUI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    //Gọi thủ tục tính toán kết quả theo tarhet
                    string storedProcedure = storedProceduce;
                    DynamicParameters parameters = new DynamicParameters();

                    var datas = await conn.QueryAsync<SVN_ACT_downtimeUI>(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
                    return datas.ToList();
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
