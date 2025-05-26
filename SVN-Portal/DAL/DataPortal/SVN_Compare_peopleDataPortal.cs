using Dapper;
using SVN_Portal.DAL.DTO;
using System.Data.SqlClient;
using System.Data;

namespace SVN_Portal.DAL.DataPortal
{
    public class SVN_Compare_peopleDataPortal
    {
        string connectionString;
        public SVN_Compare_peopleDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<SVN_Compare_peopleUI>> ReadList(string date, string storedProceduce = "SVN_Compare_people")
        {
            List<SVN_Compare_peopleUI> dataUI = new List<SVN_Compare_peopleUI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    //Gọi thủ tục tính toán kết quả theo tarhet
                    string storedProcedure = storedProceduce;
                    DynamicParameters parameters = new DynamicParameters();
                    parameters.Add("date_time", date);

                    var datas = await conn.QueryAsync<SVN_Compare_peopleUI>(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
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
