using SVNShareLib.DTO.NewDashboard;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;

namespace SVNShareLib.DAL.NewDashboard
{
    public class SVNQACheckListReport_v1DataPortal
    {
        string connectionString = string.Empty;
        public SVNQACheckListReport_v1DataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<SVN_QACheckList_Report_v1UI>> GetDataByDateAndOperation(string date, int storeID)
        {
            List<SVN_QACheckList_Report_v1UI> dataUI = new List<SVN_QACheckList_Report_v1UI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select TOP(1) qr.Number, qr.Name, qcr.ConfirmStatus, qrr.PointBSC, qr.RestaurantStaffs, qr.CheckDate from QACheckListReport qr\r\nLeft join QAConfirmResultReport qcr on qcr.QAReportID = qr.ID\r\nLeft join QAReviewResultReport qrr on qrr.QAReportID = qr.ID\r\nWhere 1=1\r\nAND qr.CheckDate = @date\r\nand qr.StoreID = @storeID\r\norder by qr.ModifiedOn desc";
                    param = new { date = date, storeID = storeID };
                    var data = await conn.QueryAsync<SVN_QACheckList_Report_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
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
