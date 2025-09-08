using Dapper;
using SVN_Portal.DAL.DTO;
using SVN_Portal.Models;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using static System.Runtime.InteropServices.JavaScript.JSType;
namespace SVN_Portal.DAL.DataPortal
{
    public class SVN_TargetDataPortal
    {
        string connectionString;
        public SVN_TargetDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }


        public async Task<List<SVN_target>> ReadList(string date, string storedProceduce = "SVN_Pro_CalTarget")
        {
            List<SVN_target> dataUI = new List<SVN_target>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                  //Gọi thủ tục tính toán kết quả theo tarhet
                    string storedProcedure = storedProceduce;
                    DynamicParameters parameters = new DynamicParameters();
                    parameters.Add("date_time", date);
                   
                    var datas = await conn.QueryAsync<SVN_target>(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
                    return datas.ToList();
                }
                return dataUI;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<SVN_target>> ReadListTargetFromToDate(DateTime fromDate, DateTime toDate)
        {
            List<SVN_target> dataUI = new List<SVN_target>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from SVN_daily_target WHERE CONVERT(date, Date_time, 112) BETWEEN @fromDate AND @toDate";
                    param = new { fromDate = fromDate, toDate = toDate };
                    var data = await conn.QueryAsync<SVN_target>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
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

