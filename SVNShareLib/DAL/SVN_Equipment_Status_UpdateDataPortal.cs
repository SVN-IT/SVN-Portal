using SVNShareLib.DTO;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace SVNShareLib.DAL
{
    public class SVN_Equipment_Status_UpdateDataPortal
    {
        string connectionString;
        public SVN_Equipment_Status_UpdateDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public SVN_Equipment_StatusUI GetEquipmentStatusByOeration(string operation, DateTime datetime)
        {
            SVN_Equipment_StatusUI dataUI = new SVN_Equipment_StatusUI();
            try
            {
                string sql = "SELECT * FROM SVN_Equipment_Status_Update where Operation = @operation AND Datetime = @datetime";
                var param = new { operation = operation, datetime = datetime };
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    var data = connection.QueryFirstOrDefault<SVN_Equipment_StatusUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    return data;
                }
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<SVN_Calc_Run_Duration_UI>> GetCalDuration(string date, string operation, string storedProceduce = "Calc_Run_Duration")
        {
            List<SVN_Calc_Run_Duration_UI> dataUI = new List<SVN_Calc_Run_Duration_UI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    //Gọi thủ tục tính toán kết quả theo tarhet
                    string storedProcedure = storedProceduce;
                    DynamicParameters parameters = new DynamicParameters();
                    parameters.Add("WorkDate", date);
                    parameters.Add("Operation", operation);

                    var datas = await conn.QueryAsync<SVN_Calc_Run_Duration_UI>(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
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
