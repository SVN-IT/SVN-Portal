using SVN_Portal.DAL.DTO;
using System.Data.SqlClient;
using System.Data;
using Dapper;
using PrinterServices.Objects;

namespace SVN_Portal.DAL.DataPortal
{
    public class SVN_Printer_InfoDataPortal
    {
        string connectionString;
        public SVN_Printer_InfoDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<List<PrinterConfigData>> ReadList(string tableName = "SVN_Printer_Info")
        {
            List<PrinterConfigData> dataUI = new List<PrinterConfigData>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from " + tableName;
                    param = new object();
                    var data = await conn.QueryAsync<PrinterConfigData>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    dataUI = data.ToList();
                }
                return dataUI;
            }
            catch
            {
                return null;
            }
        }

        public async Task<PrinterConfigData> ReadByID(string ID_Printer, string tableName = "SVN_Printer_Info")
        {
            PrinterConfigData dataUI = new PrinterConfigData();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from " + tableName + " Where ID_Printer = @ID_Printer";
                    param = new { ID_Printer = ID_Printer };
                    var data = await conn.QueryFirstOrDefaultAsync<PrinterConfigData>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    dataUI = data;
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
