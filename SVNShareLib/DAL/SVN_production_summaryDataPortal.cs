using SVNShareLib.DTO;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;
using Dapper;

namespace SVNShareLib.DAL
{
    public class SVN_production_summaryDataPortal
    {
        string connectionString;
        public SVN_production_summaryDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public List<SVN_production_summaryUI> ReadListByYearMonth(int year, int month)
        {
            try
            {
                List<SVN_production_summaryUI> dataUI = new List<SVN_production_summaryUI>();
                string sql = "SELECT * FROM SVN_production_summary WHERE year = @year AND month = @month";
                var param = new { year = year, month = month };

                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    dataUI = connection.Query<SVN_production_summaryUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text).ToList();
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
