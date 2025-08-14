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
    public class SeriFGAndWipDataPortal
    {
        string connectionString;
        public SeriFGAndWipDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }
        public int InsertBulk(List<SeriFGAndWipUI> mrp_ProductionUIs)
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
                            var insertResult = connection.Execute("INSERT INTO [dbo].[SVN_SeriFGAndWip]([id],[name],[seriTP],[seriBTP],[date_finished],[productID])\r\n     VALUES(@id,@name,@seriTP,@seriBTP,@date_finished,@productID)", mrp_ProductionUIs, trans, commandTimeout: timeOut);
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
