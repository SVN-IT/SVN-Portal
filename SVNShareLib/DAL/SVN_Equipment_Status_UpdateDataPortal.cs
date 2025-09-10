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
    public class SVN_Equipment_Status_UpdateDataPortal
    {
        string connectionString;
        public SVN_Equipment_Status_UpdateDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public SVN_Equipment_StatusUI GetEquipmentStatusByOeration(string operation, DateTime datetime)
        {
            SVN_Equipment_StatusUI dataUI = new SVN_Equipment_StatusUI();
            try
            {
                string sql = "SELECT * FROM SVN_Equipment_Status_Update where Operation = @operation AND Datetime = @datetime";
                var param = new { operation = operation, datetime = datetime };
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    var data = connection.QueryFirstOrDefault<SVN_Equipment_StatusUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    return data;
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
