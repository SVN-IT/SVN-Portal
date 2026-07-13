using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using SVNShareLib.DTO.NewDashboard;

namespace SVNShareLib.DAL.NewDashboard
{
    public class SVN_production_result_v1DataPortal
    {
        string connectionString;
        public SVN_production_result_v1DataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<SVN_production_result_v1UI>> ReadList(string date, string tableName = "SVN_Production_result_Viindoo")
        {
            List<SVN_production_result_v1UI> dataUI = new List<SVN_production_result_v1UI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from " + tableName + " where Date_time = @date";
                    param = new { date };
                    var data = await conn.QueryAsync<SVN_production_result_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    dataUI = data.ToList();
                }
                return dataUI;
            }
            catch
            {
                return null;
            }
        }

        public async Task<SVN_production_result_v1UI> ReadTypeValueByOperation(string date, string operation, string shift, string type_value, string tableName = "SVN_Production_result_Viindoo")
        {
            SVN_production_result_v1UI dataUI = new SVN_production_result_v1UI();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from " + tableName + " where Date_time = @date AND Operation = @operation AND Type_value = @type_value AND Shift = @shift";
                    param = new { date, operation, shift, type_value };
                    var data = await conn.QueryFirstOrDefaultAsync<SVN_production_result_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
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
        /// THỰC THI CHÈN HÀNG LOẠT (BULK INSERT)
        /// Trả về số lượng dòng chèn thành công (nếu lỗi hoặc không có dòng nào sẽ trả về <= 0)
        /// </summary>
        public int InsertHourlyTargetBulk(List<SVN_production_result_v1UI> insertDataList)
        {
            if (insertDataList == null || insertDataList.Count == 0) return 0;

            string query = @"
            INSERT INTO SVN_Production_result_Viindoo 
            (
                Type_value, Time1, Time2, Time3, Time4, Time5, Time6, 
                Operation, Date_time, WC, Achieve, Forecast, 
                WORunning, Product, Customer, Shift
            ) 
            VALUES 
            (
                @Type_value, @Time1, @Time2, @Time3, @Time4, @Time5, @Time6, 
                @Operation, @Date_time, @WC, @Achieve, @Forecast, 
                @WORunning, @Product, @Customer, @Shift
            );";

            int totalRowsAffected = 0;

            // Sử dụng một kết nối duy nhất và dùng Transaction để tăng tốc độ ghi DB gấp 10 lần
            using (IDbConnection db = new SqlConnection(connectionString))
            {
                db.Open();
                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        // Dapper tự động tối ưu hóa việc truyền mảng/danh sách đối tượng vào truy vấn
                        totalRowsAffected = db.Execute(query, insertDataList, transaction);

                        transaction.Commit(); // Xác nhận lưu dữ liệu nếu toàn bộ mảng hợp lệ
                    }
                    catch (Exception)
                    {
                        transaction.Rollback(); // Hoàn tác (Hủy bỏ) nếu có bất kỳ dòng nào bị crash (Ví dụ: Trùng khóa, sai kiểu số)
                        throw; // Đẩy lỗi ra ngoài để tầng Try/Catch ở Controller hứng và log lỗi
                    }
                }
            }

            return totalRowsAffected;
        }

        /// <summary>
        /// THỰC THI CẬP NHẬT HÀNG LOẠT (BULK UPDATE)
        /// Trả về số lượng dòng cập nhật thành công (nếu lỗi hoặc không có dòng nào sẽ trả về <= 0)
        /// </summary>
        public int UpdateHourlyTargetBulk(List<SVN_production_result_v1UI> updateDataList)
        {
            if (updateDataList == null || updateDataList.Count == 0) return 0;

            string query = @"
            UPDATE SVN_Production_result_Viindoo
            SET 
                Time1 = @Time1,
                Time2 = @Time2,
                Time3 = @Time3,
                Time4 = @Time4,
                Time5 = @Time5,
                Time6 = @Time6,
                WC = @WC,
                Achieve = @Achieve,
                Forecast = @Forecast,
                WORunning = @WORunning,
                Product = @Product,
                Customer = @Customer
            WHERE Date_time = @Date_time 
              AND Operation = @Operation 
              AND Type_value = @Type_value
              AND Shift = @Shift;";

            int totalRowsAffected = 0;

            using (IDbConnection db = new SqlConnection(connectionString))
            {
                db.Open();
                using (var transaction = db.BeginTransaction())
                {
                    try
                    {
                        totalRowsAffected = db.Execute(query, updateDataList, transaction);

                        transaction.Commit();
                    }
                    catch (Exception)
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }

            return totalRowsAffected;
        }

        /// <summary>
        /// XÓA (DELETE) - Xóa bản ghi dựa trên các điều kiện định danh dữ liệu
        /// </summary>
        public async Task<int> DeleteHourlyTarget(string dateTime, string operation, string typeValue, string shift)
        {
            string query = @"
            DELETE FROM SVN_Production_result_Viindoo 
            WHERE Date_time = @DateTime 
              AND Operation = @Operation 
              AND Type_value = @TypeValue
              AND Shift = @Shift;";

            try
            {
                using (IDbConnection db = new SqlConnection(connectionString))
                {
                    int rowsAffected = await db.ExecuteAsync(query, new
                    {
                        DateTime = dateTime,
                        Operation = operation,
                        TypeValue = typeValue,
                        Shift = shift
                    });

                    return rowsAffected; // Trả về số lượng dòng bị xóa
                }
            }
            catch (Exception ex)
            {
                return 0;
            }
        }

        public async Task<List<SVN_production_result_v1UI>> ReadListByOperationsRunning(string date, string tableName = "SVN_Production_result_Viindoo")
        {
            List<SVN_production_result_v1UI> dataUI = new List<SVN_production_result_v1UI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from " + tableName + " where Date_time = @date AND Type_value = @type_value AND (Time1 != 0 OR Time2 != 0 OR Time3 != 0 OR Time4 != 0 OR Time5 != 0)";
                    param = new { date, type_value = "Target" };
                    var data = await conn.QueryAsync<SVN_production_result_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    dataUI = data.ToList();
                    if (dataUI != null)
                    {
                        List<string> operations = dataUI.Select(x => x.Operation).Distinct().ToList();
                        if (operations != null && operations.Count > 0)
                        {
                            sql = "select * from " + tableName + " where Date_time = @date AND Operation IN @operations";
                            param = new { date, operations };
                            var data1 = await conn.QueryAsync<SVN_production_result_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                            dataUI = data1.ToList();
                        }
                    }
                }
                return dataUI;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<SVN_production_result_v1UI>> ReadListByOperAndWC(string date, string oper, string WC = "", string tableName = "SVN_Production_result_Viindoo")
        {
            List<SVN_production_result_v1UI> dataUI = new List<SVN_production_result_v1UI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from " + tableName + " where Date_time = @date AND Operation = @oper";
                    if (!string.IsNullOrWhiteSpace(WC))
                    {
                        sql += " AND WC = @WC";
                        param = new { date, oper, WC };
                    }
                    else
                    {
                        param = new { date, oper };
                    }
                    var data = await conn.QueryAsync<SVN_production_result_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
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
