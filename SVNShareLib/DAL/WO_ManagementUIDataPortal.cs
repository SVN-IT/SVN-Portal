using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using SVNShareLib.DTO;

namespace SVNShareLib.DAL
{
    public class WO_ManagementUIDataPortal
    {
        string _connectionString;
        public WO_ManagementUIDataPortal(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<int> CreateAsync(WO_ManagementUI item)
        {
            try
            {
                using (IDbConnection db = new SqlConnection(_connectionString))
                {
                    string sql = @"INSERT INTO WO_Management (WO_Name, WO_Content) 
                           VALUES (@WO_Name, @WO_Content);
                           SELECT CAST(SCOPE_IDENTITY() as int);"; // Lấy ID vừa tạo

                    return await db.ExecuteScalarAsync<int>(sql, item);
                }
            }
            catch
            {
                return -1; // Trả về -1 nếu có lỗi
            }
        }

        public async Task<WO_ManagementUI> GetByWONameAsync(string WO_Name)
        {
            try
            {
                using (IDbConnection db = new SqlConnection(_connectionString))
                {
                    string sql = "SELECT * FROM WO_Management WHERE WO_Name = @WO_Name";
                    return await db.QueryFirstOrDefaultAsync<WO_ManagementUI>(sql, new { WO_Name });
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
