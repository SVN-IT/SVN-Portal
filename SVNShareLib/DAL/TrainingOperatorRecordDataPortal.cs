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
    public class TrainingOperatorRecordDataPortal
    {
        string connectionString;
        public TrainingOperatorRecordDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public List<TrainingOperatorRecordUI> ReadListByOperation(string operation)
        {
            try
            {
                List<TrainingOperatorRecordUI> dataUI = new List<TrainingOperatorRecordUI>();
                string sql = "SELECT * FROM TrainingOperatorRecord WHERE Operation = @operation";
                var param = new { operation = operation };

                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    dataUI = connection.Query<TrainingOperatorRecordUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text).ToList();
                    return dataUI;
                }
            }
            catch
            {
                return null;
            }
        }

        public async Task<int> Update(TrainingOperatorRecordUI dataUI, string tableName = "TrainingOperatorRecord")
        {
            int timeOut = 1000;
            string updateQuery = "UPDATE " + tableName +
                    " SET Status = @Status " +
                    "WHERE Operator_code = @Operator_code AND Operation = @Operation";
            try
            {
                using (IDbConnection dbConnection = new SqlConnection(connectionString))
                {
                    dbConnection.Open();

                    // Execute the update query; Dapper maps the parameters automatically
                    int rowsAffected = await dbConnection.ExecuteAsync(updateQuery, dataUI, null, timeOut, CommandType.Text);

                    return rowsAffected;
                }
            }
            catch
            {
                return -1;
            }
        }

        public int InsertBulk(List<TrainingOperatorRecordUI> dataUI)
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
                            var insertResult = connection.Execute("INSERT INTO [dbo].[TrainingOperatorRecord]([JsonData],[Traning_date],[Traing_hours],[Training_doc_code],[Operation],[Supervisor_code],[Operator_code],[Operator_name],[Status])VALUES(@JsonData,@Traning_date,@Traing_hours,@Training_doc_code,@Operation,@Supervisor_code,@Operator_code,@Operator_name,@Status)", dataUI, trans, commandTimeout: timeOut);
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

        public int DeleteBulk(List<TrainingOperatorRecordUI> dataUI)
        {
            int timeOut = 1000;
            try
            {
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    if (connection.State != ConnectionState.Open) connection.Open();

                    using (var trans = connection.BeginTransaction())
                    {
                        try
                        {
                            // Lưu ý: Câu lệnh xóa nên dựa trên các khóa chính để tránh xóa nhầm
                            // Ở đây mình ví dụ xóa theo Operator_code và Training_doc_code
                            string sql = @"DELETE FROM [dbo].[TrainingOperatorRecord] 
                                   WHERE [Operator_code] = @Operator_code 
                                   AND [Operation] = @Operation";

                            var deleteResult = connection.Execute(sql, dataUI, trans, commandTimeout: timeOut);

                            if (deleteResult <= 0)
                            {
                                trans.Rollback();
                                return -1;
                            }

                            trans.Commit();
                            return deleteResult;
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
