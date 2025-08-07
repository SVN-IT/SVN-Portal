using SVN_Portal.DAL.DTO;
using System.Data.SqlClient;
using System.Data;
using System.Threading;
using Dapper;

namespace SVN_Portal.DAL.DataPortal
{
    public class SVNQACheckListReportDataPortal
    {
        string connectionString = string.Empty; 
        public SVNQACheckListReportDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<SVN_QACheckList_Report_UI>> GetDataByDateAndOperation(string date, int storeID)
        {
            List<SVN_QACheckList_Report_UI> dataUI = new List<SVN_QACheckList_Report_UI>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    //sql = "select TOP(1) qr.Number, qr.Name, qcr.ConfirmStatus, qrr.PointBSC from QACheckListReport qr\r\n\tLeft join QAConfirmResultReport qcr on qcr.QAReportID = qr.ID\r\n\tLeft join QAReviewResultReport qrr on qrr.QAReportID = qr.ID\r\n\tWhere qcr.ConfirmedDate = @date AND qcr.ConfirmStatus = 'Y'\r\n\tAND qrr.ConfirmedDate = @date AND qrr.PointBSC >= 4\r\n\tand qr.StoreID = @storeID\r\n\torder by qr.ModifiedOn desc";
                    sql = "select TOP(1) qr.Number, qr.Name, qcr.ConfirmStatus, qrr.PointBSC, qr.RestaurantStaffs from QACheckListReport qr\r\nLeft join QAConfirmResultReport qcr on qcr.QAReportID = qr.ID\r\nLeft join QAReviewResultReport qrr on qrr.QAReportID = qr.ID\r\nWhere qcr.ConfirmedDate = @date\r\nAND qrr.ConfirmedDate = @date\r\nand qr.StoreID = @storeID\r\norder by qr.ModifiedOn desc";
                    param = new { date = date, storeID = storeID };
                    var data = await conn.QueryAsync<SVN_QACheckList_Report_UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
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
