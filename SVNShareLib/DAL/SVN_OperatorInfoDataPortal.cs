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
    public class SVN_OperatorInfoDataPortal
    {
        string connectionString;
        public SVN_OperatorInfoDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public List<SVN_OperatorInfoUI> ReadList()
        {
            try
            {
                List<SVN_OperatorInfoUI> dataUI = new List<SVN_OperatorInfoUI>();
                string sql = "SELECT * FROM SVN_OperatorInfo";
                var param = new object();

                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    dataUI = connection.Query<SVN_OperatorInfoUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text).ToList();
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
