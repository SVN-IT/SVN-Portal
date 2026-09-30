using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using SVN_Portal.DAL.DTO;

namespace SVN_Portal.DAL.DataPortal
{
    public class SVN_PrintLogTwistDataPortal
    {
        readonly string _connectionString;

        public SVN_PrintLogTwistDataPortal(string connectionString)
        {
            _connectionString = connectionString;
        }

        private IDbConnection Connection => new SqlConnection(_connectionString);

        public async Task<int> InsertAsync(SVN_PrintLogTwistUI log)
        {
            const string sql = @"
                INSERT INTO SVN_PrintLogTwist (wo_code, product_id, product_code, print_qty, print_time)
                VALUES (@wo_code, @product_id, @product_code, @print_qty, @print_time);
                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            using var db = Connection;
            return await db.ExecuteScalarAsync<int>(sql, log);
        }

        public async Task<List<SVN_PrintLogTwistUI>> GetByDateAsync(DateTime date)
        {
            const string sql = @"
                SELECT * FROM SVN_PrintLogTwist
                WHERE CAST(print_time AS DATE) = CAST(@date AS DATE)
                ORDER BY print_time DESC";

            using var db = Connection;
            var result = await db.QueryAsync<SVN_PrintLogTwistUI>(sql, new { date });
            return result.ToList();
        }

        public async Task<List<SVN_PrintLogTwistUI>> GetAllProductMappingsAsync()
        {
            const string sql = @"
                SELECT product_id, product_code,
                       0 AS print_qty, GETDATE() AS print_time, '' AS wo_code
                FROM SVN_PrintLogTwist
                GROUP BY product_id, product_code";

            using var db = Connection;
            var result = await db.QueryAsync<SVN_PrintLogTwistUI>(sql);
            return result.ToList();
        }

        public async Task<List<SVN_PrintLogTwistUI>> GetSummaryByDateAsync(DateTime date)
        {
            const string sql = @"
                SELECT
                    product_code,
                    product_id,
                    SUM(print_qty) AS print_qty,
                    COUNT(*) AS print_count,
                    MIN(print_time) AS print_time
                FROM SVN_PrintLogTwist
                WHERE CAST(print_time AS DATE) = CAST(@date AS DATE)
                GROUP BY product_code, product_id
                ORDER BY product_code";

            using var db = Connection;
            var result = await db.QueryAsync<SVN_PrintLogTwistUI>(sql, new { date });
            return result.ToList();
        }
    }
}
