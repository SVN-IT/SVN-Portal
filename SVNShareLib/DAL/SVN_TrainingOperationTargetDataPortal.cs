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
    public class SVN_TrainingOperationTargetDataPortal
    {
        string connectionString;
        public SVN_TrainingOperationTargetDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public List<SVN_TrainingOperationTargetUI> ReadListTargetByOperation(string operation)
        {
            try
            {
                List<SVN_TrainingOperationTargetUI> dataUI = new List<SVN_TrainingOperationTargetUI>();
                string sql = "SELECT * FROM SVN_TrainingOperationTarget WHERE Operation = @operation";
                var param = new { operation = operation };

                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    dataUI = connection.Query<SVN_TrainingOperationTargetUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text).ToList();
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
