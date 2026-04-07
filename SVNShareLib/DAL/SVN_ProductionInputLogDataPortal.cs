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
    public class SVN_ProductionInputLogDataPortal
    {
        private readonly string _connectionString;

        public SVN_ProductionInputLogDataPortal(string connectionString)
        {
            _connectionString = connectionString;
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        public async Task<int> InsertAsync(SVN_ProductionInputLogUI log)
        {
            const string sql = @"
            INSERT INTO SVN_ProductionInputLogs 
            ([level], product_id, product_qty, date_finished, product_type, serial_code, 
             component_list, [state], API_function, API_parameters, [status], wo_code)
            VALUES 
            (@level, @product_id, @product_qty, @date_finished, @product_type, @serial_code, 
             @component_list, @state, @API_function, @API_parameters, @status, @wo_code);
            SELECT CAST(SCOPE_IDENTITY() as int);";

            using (var db = Connection)
            {
                // Trả về ID vừa tạo tự động
                return await db.ExecuteScalarAsync<int>(sql, log);
            }
        }

        // 2. READ (Get All)
        public async Task<IEnumerable<SVN_ProductionInputLogUI>> GetAllAsync()
        {
            const string sql = "SELECT * FROM SVN_ProductionInputLogs ORDER BY id DESC";
            using (var db = Connection)
            {
                return await db.QueryAsync<SVN_ProductionInputLogUI>(sql);
            }
        }

        // 3. READ (Get by ID)
        public async Task<SVN_ProductionInputLogUI> GetByIdAsync(int id)
        {
            const string sql = "SELECT * FROM SVN_ProductionInputLogs WHERE id = @id";
            using (var db = Connection)
            {
                return await db.QueryFirstOrDefaultAsync<SVN_ProductionInputLogUI>(sql, new { id });
            }
        }

        // 4. UPDATE
        public async Task<bool> UpdateAsync(SVN_ProductionInputLogUI log)
        {
            const string sql = @"
            UPDATE SVN_ProductionInputLogs 
            SET [level] = @level, 
                product_id = @product_id, 
                product_qty = @product_qty, 
                date_finished = @date_finished, 
                product_type = @product_type, 
                serial_code = @serial_code, 
                component_list = @component_list, 
                [state] = @state, 
                API_function = @API_function, 
                API_parameters = @API_parameters, 
                [status] = @status, 
                wo_code = @wo_code
            WHERE id = @id";

            using (var db = Connection)
            {
                var rowsAffected = await db.ExecuteAsync(sql, log);
                return rowsAffected > 0;
            }
        }

        // 5. DELETE
        public async Task<bool> DeleteAsync(int id)
        {
            const string sql = "DELETE FROM SVN_ProductionInputLogs WHERE id = @id";
            using (var db = Connection)
            {
                var rowsAffected = await db.ExecuteAsync(sql, new { id });
                return rowsAffected > 0;
            }
        }
    }
}
