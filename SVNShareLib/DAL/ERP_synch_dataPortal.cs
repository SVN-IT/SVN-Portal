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
    public class ERP_synch_dataPortal
    {
        string connectionString;
        public ERP_synch_dataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public ERP_synch_dataUI GetDataByID(string savedsearchID)
        {
            try
            {
                ERP_synch_dataUI data = new ERP_synch_dataUI();
                string sql = "SELECT * FROM ERP_synch_data WHERE savedsearchID = @savedsearchID";
                var param = new { savedsearchID = savedsearchID };
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    data = connection.QueryFirstOrDefault<ERP_synch_dataUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    return data;
                }
            }
            catch
            {
                return null;
            }
        }

        public ERP_synch_dataUI GetDataByIDAndSyncDate(string savedsearchID, string Synch_datetime)
        {
            try
            {
                ERP_synch_dataUI data = new ERP_synch_dataUI();
                string sql = "SELECT * FROM ERP_synch_data WHERE savedsearchID = @savedsearchID AND Synch_datetime = @Synch_datetime";
                var param = new { savedsearchID = savedsearchID, Synch_datetime = Synch_datetime };
                int timeOut = 1000;
                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    data = connection.QueryFirstOrDefault<ERP_synch_dataUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                    return data;
                }
            }
            catch
            {
                return null;
            }
        }

        public bool Insert(ERP_synch_dataUI entity)
        {
            try
            {
                string sql = @"INSERT INTO ERP_synch_data (savedsearchID, data, Synch_datetime) 
                       VALUES (@savedsearchID, @data, @Synch_datetime)";

                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    // Execute trả về số dòng được chèn vào (1 nếu thành công)
                    int result = connection.Execute(sql, entity, commandTimeout: 1000);
                    return result > 0;
                }
            }
            catch (Exception ex)
            {
                // Log ex nếu cần thiết
                return false;
            }
        }

        public bool Update(ERP_synch_dataUI entity)
        {
            try
            {
                string sql = @"UPDATE ERP_synch_data 
                       SET data = @data
                       WHERE Synch_datetime = @Synch_datetime";

                using (IDbConnection connection = new SqlConnection(connectionString))
                {
                    int result = connection.Execute(sql, entity, commandTimeout: 1000);
                    return result > 0;
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }
    }
}
