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
    public class SVN_Label_InfoDataPortal
    {
        string connectionString;
        public SVN_Label_InfoDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public List<SVN_Label_InfoUI> ReadList()
        {
            try
            {
                List<SVN_Label_InfoUI> data = new List<SVN_Label_InfoUI>();
                string sql = "SELECT * FROM SVN_Label_Info";
                var param = new object();
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    data = connection.Query<SVN_Label_InfoUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text).ToList();
                    return data;
                }
            }
            catch
            {
                return null;
            }
        }

        public List<SVN_Label_InfoUI> ReadListByLotID(string LotID, string Date = "")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Date))
                {
                    Date = DateTime.Now.ToString("yyyyMMdd");
                }
                List<SVN_Label_InfoUI> data = new List<SVN_Label_InfoUI>();
                string sql = "SELECT * FROM SVN_Label_Info Where LotID = @LotID AND Date = @date";
                var param = new { LotID = LotID, Date = Date };
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    data = connection.Query<SVN_Label_InfoUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text).ToList();
                    return data;
                }
            }
            catch
            {
                return null;
            }
        }

        public List<SVN_Label_InfoUI> ReadListByPalletID(string PalletID, string Date = "")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Date))
                {
                    Date = DateTime.Now.ToString("yyyyMMdd");
                }
                List<SVN_Label_InfoUI> data = new List<SVN_Label_InfoUI>();
                string sql = "SELECT * FROM SVN_Label_Info Where PalletID = @PalletID AND Date = @date";
                var param = new { PalletID = PalletID, Date = Date };
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    data = connection.Query<SVN_Label_InfoUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text).ToList();
                    return data;
                }
            }
            catch
            {
                return null;
            }
        }

        public SVN_Label_InfoUI ReadListBySerialNumbers(string SerialNumber, string Date = "")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Date))
                {
                    Date = DateTime.Now.ToString("yyyyMMdd");
                }

                if (!string.IsNullOrWhiteSpace(SerialNumber))
                {
                    SerialNumber = "%" + SerialNumber + "%"; // Ensure that SerialNumber is treated as a wildcard search
                }

                SVN_Label_InfoUI data = new SVN_Label_InfoUI();
                string sql = "SELECT * FROM SVN_Label_Info Where SerialNumbers LIKE @SerialNumber AND Date = @date";
                var param = new { SerialNumber = SerialNumber, Date = Date };
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    data = connection.QueryFirstOrDefault<SVN_Label_InfoUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    return data;
                }
            }
            catch
            {
                return null;
            }
        }

        public int InsertBulk(List<SVN_Label_InfoUI> mrp_ProductionUIs)
        {
            int timeOut = 1000;
            try
            {
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                    }
                    using (var trans = connection.BeginTransaction())
                    {
                        try
                        {
                            var insertResult = connection.Execute("INSERT INTO [dbo].[SVN_Label_Info]\r\n           ([Date]\r\n           ,[LotID]\r\n           ,[SerialNumbers]\r\n           ,[ScanDateTime]\r\n           ,[Status]\r\n           ,[Operation]\r\n           ,[EmployerID], [PalletID], [SerialCount], [IsDelete])\r\n     VALUES\r\n           (@Date\r\n           ,@LotID\r\n           ,@SerialNumbers\r\n           ,@ScanDateTime\r\n           ,@Status\r\n           ,@Operation\r\n           ,@EmployerID, @PalletID, @SerialCount, @IsDelete)", mrp_ProductionUIs, trans, commandTimeout: timeOut);
                            if (insertResult <= 0)
                            {
                                trans.Rollback();
                                return -1;
                            }
                            else
                            {
                                trans.Commit();
                                return insertResult;
                            }
                        }
                        catch
                        {
                            trans.Rollback();
                            return -1;
                        }
                    }
                }
            }
            catch
            {
                return -1;
            }
        }

        public int UpdateBulk(List<SVN_Label_InfoUI> mrp_ProductionUIs)
        {
            int timeOut = 1000;
            try
            {
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    if (connection.State != ConnectionState.Open)
                    {
                        connection.Open();
                    }
                    using (var trans = connection.BeginTransaction())
                    {
                        try
                        {
                            var insertResult = connection.Execute("UPDATE [dbo].[SVN_Label_Info]\r\nSET\r\n    [Date] = @Date,\r\n    [LotID] = @LotID,\r\n    [SerialNumbers] = @SerialNumbers,\r\n    [ScanDateTime] = @ScanDateTime,\r\n    [Status] = @Status,\r\n    [Operation] = @Operation,\r\n    [EmployerID] = @EmployerID,\r\n    [PalletID] = @PalletID,\r\n    [SerialCount] = @SerialCount,\r\n    [IsDelete] = @IsDelete\r\nWHERE\r\n    [SerialNumbers] like @SerialNumbers", mrp_ProductionUIs, trans, commandTimeout: timeOut);
                            if (insertResult <= 0)
                            {
                                trans.Rollback();
                                return -1;
                            }
                            else
                            {
                                trans.Commit();
                                return insertResult;
                            }
                        }
                        catch
                        {
                            trans.Rollback();
                            return -1;
                        }
                    }
                }
            }
            catch
            {
                return -1;
            }
        }
    }
}
