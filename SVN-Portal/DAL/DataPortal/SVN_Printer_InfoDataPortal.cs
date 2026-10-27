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

        public async Task<List<PrinterConfigData>> ReadListByType(string Type, string tableName = "SVN_Printer_Info")
        {
            List<PrinterConfigData> dataUI = new List<PrinterConfigData>();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from " + tableName + " Where Type = @Type";
                    param = new { Type = Type };
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

        public async Task<int> Update(PrinterConfigData printerConfigData, string tableName = "SVN_Printer_Info")
        {
            int timeOut = 1000;
            string updateQuery = "UPDATE " + tableName +
                    " SET Name_Printer = @Name_Printer, " +
                    "IP_Printer = @IP_Printer, MAC_Printer = @MAC_Printer, " +
                    "Port_Printer = @Port_Printer, Size = @Size, " +
                    "Type = @Type, ZPL_Temp = @ZPL_Temp, DPL_Temp = @DPL_Temp " +
                    "WHERE ID_Printer = @ID_Printer";
            try
            {
                using (IDbConnection dbConnection = new SqlConnection(connectionString))
                {
                    dbConnection.Open();

                    // Execute the update query; Dapper maps the parameters automatically
                    int rowsAffected = await dbConnection.ExecuteAsync(updateQuery, printerConfigData, null, timeOut, CommandType.Text);

                    return rowsAffected;
                }
            }
            catch
            {
                return -1;
            }
        }

        public async Task<int> Insert(PrinterConfigData printerConfigData, string tableName = "SVN_Printer_Info")
        {
            int timeOut = 1000;
            string insertQuery = "INSERT INTO " + tableName + " " +
                "(Name_Printer, IP_Printer, MAC_Printer, Port_Printer, Size, Type, ZPL_Temp, DPL_Temp, ID_Printer) " +
                "VALUES (@Name_Printer, @IP_Printer, @MAC_Printer, @Port_Printer, @Size, @Type, @ZPL_Temp, @DPL_Temp, @ID_Printer)";
            try
            {
                using (IDbConnection dbConnection = new SqlConnection(connectionString))
                {
                    dbConnection.Open();

                    // Execute the update query; Dapper maps the parameters automatically
                    int rowsAffected = await dbConnection.ExecuteAsync(insertQuery, printerConfigData, null, timeOut, CommandType.Text);

                    return rowsAffected;
                }
            }
            catch
            {
                return -1;
            }
        }
    }
}
