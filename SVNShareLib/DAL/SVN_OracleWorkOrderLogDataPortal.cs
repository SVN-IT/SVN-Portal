using Dapper;
using SVNShareLib.DTO;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DAL
{
    public class SVN_OracleWorkOrderLogDataPortal
    {
        string connectionString;
        public SVN_OracleWorkOrderLogDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<int> SynchWOLogs(List<SVN_OracleWorkOrderLogUI> dataUI)
        {
            using var connection = new SqlConnection(connectionString);

            var sql = @"
                MERGE INTO SVN_OracleWorkOrderLog AS target
                USING (SELECT @InternalId AS NetSuiteInternalId) AS source
                ON (target.NetSuiteInternalId = source.NetSuiteInternalId)
                WHEN MATCHED THEN
                    UPDATE SET TranId = @TranId, Quantity = @Quantity, StartDate = @StartDate, EndDate = @EndDate
                WHEN NOT MATCHED THEN
                    INSERT (NetSuiteInternalId, TranId, ItemId, Quantity, StartDate, EndDate, CreatedDate)
                    VALUES (@InternalId, @TranId, @ItemId, @Quantity, @StartDate, @EndDate, GETDATE());";

            // Dapper tự động lặp bulk execute mảng dtoList chỉ trong 1 transaction
            var result = await connection.ExecuteAsync(sql, dataUI);

            return result;
        }
    }
}
