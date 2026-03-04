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
    public class SVN_SavedSearch_FormulaDataPortal
    {
        string connectionString;
        public SVN_SavedSearch_FormulaDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public List<SVN_SavedSearch_FormulaUI> GetSavedSearchFormulaData(string spSearchID = "")
        {
            try
            {
                string sql = "SELECT * FROM SVN_SavedSearch_Formula";
                if (!string.IsNullOrWhiteSpace(spSearchID))
                {
                    sql = sql + " WHERE SPSearchID = @SPSearchID";
                }

                var param = new { SPSearchID = spSearchID };
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    var data = connection.Query<SVN_SavedSearch_FormulaUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    return data.ToList();
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
