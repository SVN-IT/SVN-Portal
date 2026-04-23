using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SVNShareLib.DTO.NewDashboard;
using Dapper;

namespace SVNShareLib.DAL.NewDashboard
{
    public class SVN_Defect_record_v1DataPortal
    {
        string connectionString;
        public SVN_Defect_record_v1DataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<SVN_Defect_record_v1UI>> ReadList(string date)
        {
            List<SVN_Defect_record_v1UI> dataUI = new List<SVN_Defect_record_v1UI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from SVN_Defect_Record where INSDatetime = @date";
                    param = new { date = date };
                    var data = await conn.QueryAsync<SVN_Defect_record_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    dataUI = data.ToList();
                }
                return dataUI;
            }
            catch
            {
                return null;
            }
        }

        public async Task<List<SVN_Defect_record_v1UI>> ReadListFromToDate(DateTime fromDate, DateTime toDate)
        {
            List<SVN_Defect_record_v1UI> dataUI = new List<SVN_Defect_record_v1UI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from SVN_Defect_Record WHERE CONVERT(date, INSDatetime, 112) BETWEEN @fromDate AND @toDate";
                    param = new { fromDate = fromDate, toDate = toDate };
                    var data = await conn.QueryAsync<SVN_Defect_record_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    dataUI = data.ToList();
                }
                return dataUI;
            }
            catch
            {
                return null;
            }
        }
    }
}
