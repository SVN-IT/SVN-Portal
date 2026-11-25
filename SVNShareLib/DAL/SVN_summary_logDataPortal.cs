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
    public class SVN_summary_logDataPortal
    {
        string connectionString;
        public SVN_summary_logDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public SVN_summary_logUI GetById(Guid id)
        {
            try
            {
                string sql = "SELECT * FROM SVN_summary_log WHERE Id = @Id";
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    return connection.QueryFirstOrDefault<SVN_summary_logUI>(sql, new { Id = id }, commandTimeout: timeOut);
                }
            }
            catch
            {
                return null;
            }
        }

        public int Upsert(SVN_summary_logUI log)
        {
            int timeOut = 1000;
            try
            {
                string sql = @"
                    IF EXISTS (SELECT 1 FROM [dbo].[SVN_summary_log] WHERE [Id] = @Id)
                    BEGIN
                        UPDATE [dbo].[SVN_summary_log]
                        SET [Type] = @Type, [Content] = @Content, [ModifiedBy] = @ModifiedBy, [ModifiedOn] = @ModifiedOn
                        WHERE [Id] = @Id;
                    END
                    ELSE
                    BEGIN
                        INSERT INTO [dbo].[SVN_summary_log]([Id],[App],[Action],[Type],[Content],[CreatedBy],[CreatedOn],[ModifiedBy],[ModifiedOn])
                        VALUES(@Id,@App,@Action,@Type,@Content,@CreatedBy,@CreatedOn,@ModifiedBy,@ModifiedOn);
                    END";
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    return connection.Execute(sql, log, commandTimeout: timeOut);
                }
            }
            catch
            {
                return -1;
            }
        }

        public int DeleteTransactionState(Guid id)
        {
            int timeOut = 1000;
            try
            {
                string sql = "DELETE FROM [dbo].[SVN_summary_log] WHERE [Id] = @Id AND [Action] = 'POTransactionState'";
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    return connection.Execute(sql, new { Id = id }, commandTimeout: timeOut);
                }
            }
            catch
            {
                return -1;
            }
        }

        public List<SVN_summary_logUI> ReadListFromDateToData(string Action, DateTime fromDate, DateTime toDate)
        {
            try
            {
                List<SVN_summary_logUI> dataUI = new List<SVN_summary_logUI>();
                string sql = "SELECT * FROM SVN_summary_log WHERE [Action] = @Action AND CreatedOn BETWEEN @FromDate AND @ToDate";
                var param = new { Action = Action, FromDate = fromDate, ToDate = toDate };

                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    dataUI = connection.Query<SVN_summary_logUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text).ToList();
                    return dataUI;
                }
            }
            catch
            {
                return null;
            }
        }

        private static string EscapeSqlLikePattern(string input)
        {
            return (input ?? "")
                .Replace("[", "[[]")
                .Replace("%", "[%]")
                .Replace("_", "[_]");
        }

        /// <summary>
        /// Tìm các bản ghi log lịch sử cho thấy Customer PO Number đã từng được tạo thành công trên NetSuite trước đây.
        /// </summary>
        public List<SVN_summary_logUI> FindCreatedPurchaseOrderLogs(string customerPoNumber)
        {
            if (string.IsNullOrWhiteSpace(customerPoNumber))
            {
                return new List<SVN_summary_logUI>();
            }

            try
            {
                string escapedPo = EscapeSqlLikePattern(customerPoNumber.Trim());
                string poPatternJson = $"%{escapedPo}%";
                string poPatternLog = $"%PONumber: {escapedPo},%";

                string sql = @"
                    SELECT Id, App, Action, Type, Content, CreatedBy, CreatedOn, ModifiedBy, ModifiedOn
                    FROM SVN_summary_log
                    WHERE (
                        ([Action] = 'POTransactionState' AND [Type] IN ('PO_CREATED', 'SO_COMPLETED') AND [Content] LIKE @PoPatternJson)
                        OR
                        ([Action] = 'CreateNetsuitePO' AND [Type] = 'Info' AND [Content] LIKE @PoPatternLog AND [Content] LIKE '%Netsuite PO%')
                    )
                    ORDER BY CreatedOn DESC";

                var param = new { PoPatternJson = poPatternJson, PoPatternLog = poPatternLog };
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    return connection.Query<SVN_summary_logUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text).ToList();
                }
            }
            catch
            {
                return new List<SVN_summary_logUI>();
            }
        }

        /// <summary>
        /// Tra cứu các bản ghi NetsuitePOLogInfo chứa URL theo danh sách mã SigmaPO để bổ sung thông tin hiển thị.
        /// </summary>
        public List<SVN_summary_logUI> FindTrackingLogsBySigmaPOs(IEnumerable<string> sigmaPos)
        {
            var distinctPos = sigmaPos?
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(10)
                .ToList();

            if (distinctPos == null || distinctPos.Count == 0)
            {
                return new List<SVN_summary_logUI>();
            }

            try
            {
                var conditions = new List<string>();
                var parameters = new DynamicParameters();
                for (int i = 0; i < distinctPos.Count; i++)
                {
                    string paramName = $"@Po{i}";
                    conditions.Add($"[Content] LIKE {paramName}");
                    parameters.Add(paramName, $"%{EscapeSqlLikePattern(distinctPos[i])}%");
                }

                string sql = $@"
                    SELECT Id, App, Action, Type, Content, CreatedBy, CreatedOn, ModifiedBy, ModifiedOn
                    FROM SVN_summary_log
                    WHERE [Action] = 'NetsuitePOLogInfo' AND ({string.Join(" OR ", conditions)})
                    ORDER BY CreatedOn DESC";

                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    return connection.Query<SVN_summary_logUI>(sql, parameters, commandTimeout: timeOut, commandType: CommandType.Text).ToList();
                }
            }
            catch
            {
                return new List<SVN_summary_logUI>();
            }
        }

        public int InsertBulk(List<SVN_summary_logUI> dataUI)
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
                            var insertResult = connection.Execute("INSERT INTO [dbo].[SVN_summary_log]([Id],[App],[Action],[Type],[Content],[CreatedBy],[CreatedOn],[ModifiedBy],[ModifiedOn])VALUES(@Id,@App,@Action,@Type,@Content,@CreatedBy,@CreatedOn,@ModifiedBy,@ModifiedOn)", dataUI, trans, commandTimeout: timeOut);
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
    }
}
