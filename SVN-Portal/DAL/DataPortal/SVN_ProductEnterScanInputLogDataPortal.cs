using Dapper;
using SVN_Portal.DAL.DTO;
using System.Data;
using System.Data.SqlClient;

namespace SVN_Portal.DAL.DataPortal
{
    public class SVN_ProductEnterScanInputLogDataPortal
    {
        string connectionString;
        public SVN_ProductEnterScanInputLogDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task UpsertAsync(string masterWoCode, string woCode, SVN_ProductEnterScanInputLogEntryUI entry)
        {
            int timeOut = 1000;
            using (IDbConnection conn = new SqlConnection(connectionString))
            {
                string sql = @"
                    MERGE SVN_ProductEnterScanInputLog AS target
                    USING (SELECT @master_wo_code AS master_wo_code, @wo_code AS wo_code, @product_id AS product_id, @field_type AS field_type) AS source
                    ON target.master_wo_code = source.master_wo_code
                        AND target.wo_code = source.wo_code
                        AND target.product_id = source.product_id
                        AND target.field_type = source.field_type
                    WHEN MATCHED THEN
                        UPDATE SET
                            product_name = @product_name,
                            has_tracking = @has_tracking,
                            scanned_value = @scanned_value,
                            scan_time = @scan_time
                    WHEN NOT MATCHED THEN
                        INSERT (master_wo_code, wo_code, product_id, product_name, field_type, has_tracking, scanned_value, scan_time)
                        VALUES (@master_wo_code, @wo_code, @product_id, @product_name, @field_type, @has_tracking, @scanned_value, @scan_time);";

                var param = new
                {
                    master_wo_code = masterWoCode,
                    wo_code = woCode,
                    product_id = entry.product_id,
                    product_name = entry.product_name,
                    field_type = entry.field_type,
                    has_tracking = entry.has_tracking,
                    scanned_value = entry.scanned_value,
                    scan_time = entry.scan_time
                };
                await conn.ExecuteAsync(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
            }
        }
    }
}
