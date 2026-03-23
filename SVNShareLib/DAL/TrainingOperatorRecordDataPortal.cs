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

        public List<TrainingOperatorRecordUI> ReadListByDate(string date)
        {
            try
            {
                List<TrainingOperatorRecordUI> dataUI = new List<TrainingOperatorRecordUI>();
                string sql = "SELECT * FROM TrainingOperatorRecord WHERE Traning_date = @date";
                var param = new { date = date };

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
                    "WHERE Operator_code = @Operator_code AND Traning_date = @Traning_date AND Training_doc_code = @Training_doc_code";
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
    }
}
