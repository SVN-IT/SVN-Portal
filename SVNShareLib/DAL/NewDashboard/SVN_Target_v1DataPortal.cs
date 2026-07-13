using Dapper;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SVNShareLib.DTO.NewDashboard;

namespace SVNShareLib.DAL.NewDashboard
{
    public class SVN_Target_v1DataPortal
    {
        string connectionString;
        public SVN_Target_v1DataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<SVN_target_v1UI>> ReadList(string date, string shift, string storedProceduce = "SVN_Pro_CalTarget_Viindoo")
        {
            List<SVN_target_v1UI> dataUI = new List<SVN_target_v1UI>();
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

                    var datas = await conn.QueryAsync<SVN_target_v1UI>(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
                    return datas.ToList();
                }
                return dataUI;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<SVN_target_v1UI>> ReadListTargetByDate(string fromDate, string toDate, string storedProceduce = "SVN_Pro_CalTarget_Viindoo_FT")
        {
            List<SVN_target_v1UI> dataUI = new List<SVN_target_v1UI>();
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

                    var datas = await conn.QueryAsync<SVN_target_v1UI>(storedProcedure, parameters, commandType: CommandType.StoredProcedure);
                    return datas.ToList();
                }
                return dataUI;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<SVN_target_v1UI>> ReadListTargetFromToDate(DateTime fromDate, DateTime toDate)
        {
            List<SVN_target_v1UI> dataUI = new List<SVN_target_v1UI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from SVN_daily_target WHERE CONVERT(date, Date_time, 112) BETWEEN @fromDate AND @toDate";
                    param = new { fromDate, toDate };
                    var data = await conn.QueryAsync<SVN_target_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
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
        public async Task<List<SVN_target_v1UI>> ReadListDailyTargetByDate(string date)
        {
            List<SVN_target_v1UI> dataUI = new List<SVN_target_v1UI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from SVN_daily_target WHERE Date_time = @date";
                    param = new { date, };
                    var data = await conn.QueryAsync<SVN_target_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
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
        public int InsertBulk(List<SVN_target_v1UI> sVN_Targets)
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
                            var deleteResult = connection.Execute("DELETE FROM SVN_daily_target WHERE Date_time = @date", new { date }, trans, commandTimeout: timeOut);
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

        public async Task<bool> ExecuteSyncProduction(int add_hour, string storedProceduce = "SVN_Sync_Production_By_Hour_1s")
        {
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    DynamicParameters parameters = new DynamicParameters();
                    parameters.Add("add_hour", add_hour);
                    // Sử dụng ExecuteAsync cho thủ tục không có giá trị trả về
                    // Truyền null cho tham số vì SP không có param
                    await conn.ExecuteAsync(
                        storedProceduce,
                        parameters,
                        commandType: CommandType.StoredProcedure,
                        commandTimeout: 1000
                    );

                    return true;
                }
            }
            catch (Exception ex)
            {
                // Ghi log lỗi nếu cần: Console.WriteLine(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Hàm lấy dữ liệu target theo ngày
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public async Task<List<SVN_target_v1UI>> ReadListTargetByDate(string date)
        {
            List<SVN_target_v1UI> dataUI = new List<SVN_target_v1UI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from SVN_target WHERE Date_time = @date";
                    param = new { date };
                    var data = await conn.QueryAsync<SVN_target_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
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
        /// Hàm lấy dữ liệu target theo ngày và operation
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public async Task<SVN_target_v1UI> ReadTargetByDateAndOperation(string date, string operation, string shift)
        {
            SVN_target_v1UI dataUI = new SVN_target_v1UI();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from SVN_target WHERE Date_time = @date And Operation = @operation And Shift = @shift";
                    param = new { date, operation, shift };
                    var data = await conn.QueryFirstOrDefaultAsync<SVN_target_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    dataUI = data;
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
        public int InsertTargetBulk(List<SVN_target_v1UI> sVN_Targets)
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
                            var insertResult = connection.Execute("INSERT INTO [dbo].[SVN_target]([Operation],[Daily_plan],[UPH],[UPPH],[Labor],[Date_time],[Defect],[Workingtime],[Shift])VALUES(@Operation,@Daily_plan,@UPH,@UPPH,@Labor,@Date_time,@Defect,@Workingtime,@Shift)", sVN_Targets, trans, commandTimeout: timeOut);
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
        /// Hàm cập nhật dữ liệu target hàng loạt dựa trên cặp khóa Operation và Date_time
        /// </summary>
        /// <param name="sVN_Targets"></param>
        /// <returns></returns>
        public int UpdateTargetBulk(List<SVN_target_v1UI> sVN_Targets)
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
                            // Câu lệnh UPDATE sử dụng cặp khóa ở mệnh đề WHERE
                            string updateSql = @"UPDATE [dbo].[SVN_target] 
                                         SET [Daily_plan] = @Daily_plan,
                                             [UPH] = @UPH,
                                             [UPPH] = @UPPH,
                                             [Labor] = @Labor,
                                             [Defect] = @Defect,
                                             [Workingtime] = @Workingtime,
                                             [Shift] = @Shift
                                         WHERE [Operation] = @Operation AND [Date_time] = @Date_time AND [Shift] = @Shift";

                            // Dapper tự động lặp qua toàn bộ danh sách sVN_Targets để thực hiện lệnh
                            var updateResult = connection.Execute(updateSql, sVN_Targets, trans, commandTimeout: timeOut);

                            if (updateResult <= 0)
                            {
                                trans.Rollback();
                                return -1;
                            }
                            else
                            {
                                trans.Commit();
                                return updateResult; // Trả về tổng số dòng đã cập nhật thành công
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
        public int DeleteTarget(string date, string operation, string shift)
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
                            var deleteResult = connection.Execute("DELETE FROM SVN_target WHERE Date_time = @date AND Operation = @operation AND Shift = @shift", new { date, operation, shift }, trans, commandTimeout: timeOut);
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
