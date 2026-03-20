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
    }
}
