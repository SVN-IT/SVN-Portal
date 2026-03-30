using Dapper;
using DocumentFormat.OpenXml.Office2010.Excel;
using SVN_Portal.DAL.DTO;
using SVN_Portal.Models;
using SVNShareLib.DTO;
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


        public async Task<List<SVN_target>> ReadList(string date, string shift, string storedProceduce = "SVN_Pro_CalTarget_Viindoo")
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
                    parameters.Add("shift", shift);

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

        public async Task<List<SVN_target>> ReadListTargetByDate(string fromDate, string toDate, string storedProceduce = "SVN_Pro_CalTarget_Viindoo_FT")
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
                    parameters.Add("date_time", fromDate);
                    parameters.Add("to_date", toDate);

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

        /// <summary>
        /// Hàm lấy dữ liệu target theo ngày
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public async Task<List<SVN_target>> ReadListTargetByDate(string date)
        {
            List<SVN_target> dataUI = new List<SVN_target>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from SVN_daily_target WHERE Date_time = @date";
                    param = new { date = date, };
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

        /// <summary>
        /// Hàm chèn dữ liệu target hàng loạt
        /// </summary>
        /// <param name="sVN_Targets"></param>
        /// <returns></returns>
        public int InsertBulk(List<SVN_target> sVN_Targets)
        {
            int timeOut = 1000;
            try
            {
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                    }
                    using (var trans = connection.BeginTransaction())
                    {
                        try
                        {
                            var insertResult = connection.Execute("INSERT INTO [dbo].[SVN_daily_target]([Operation],[Daily_plan],[UPH],[UPPH],[Labor],[Date_time],[Total_Qty],[MaxLabor],[Current_UPH],[Current_UPPH],[Defect],[Total_NG_Qty],[WC],[Workingtime])VALUES(@Operation,@Daily_plan,@UPH,@UPPH,@Labor,@Date_time,@Total_Qty,@MaxLabor,@Current_UPH,@Current_UPPH,@Defect,@Total_NG_Qty,@WC,@Workingtime)", sVN_Targets, trans, commandTimeout: timeOut);
                            if (insertResult <= 0)
                            {
                                trans.Rollback();
                                return -1;
                            }
                            else
                            {
                                trans.Commit();
                                return insertResult;
                            }
                        }
                        catch
                        {
                            trans.Rollback();
                            return -1;
                        }
                    }
                }
            }
            catch
            {
                return -1;
            }
        }

        /// <summary>
        /// Xóa dữ liệu
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public int Delete(string date)
        {
            int timeOut = 1000;
            try
            {
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                    }
                    using (var trans = connection.BeginTransaction())
                    {
                        try
                        {
                            var deleteResult = connection.Execute("DELETE FROM SVN_daily_target WHERE Date_time = @date", new { date = date }, trans, commandTimeout: timeOut);
                            if (deleteResult <= 0)
                            {
                                trans.Rollback();
                                return -1;
                            }
                            else
                            {
                                trans.Commit();
                                return deleteResult;
                            }
                        }
                        catch
                        {
                            trans.Rollback();
                            return -1;
                        }
                    }
                }
            }
            catch
            {
                return -1;
            }
        }
    }


}

