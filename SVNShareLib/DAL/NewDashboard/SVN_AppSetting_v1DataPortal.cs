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
    public class SVN_AppSetting_v1DataPortal
    {
        string connectionString = "";
        public SVN_AppSetting_v1DataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<SVN_AppSetting_v1UI> GetSettingByGroupAndKey(string Key, string Group = "Dashboard")
        {
            SVN_AppSetting_v1UI appSetting = new SVN_AppSetting_v1UI();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from SVN_AppSetting where [Group] = @Group and [Key] = @Key";
                    param = new { Group = Group, Key = Key };
                    appSetting = await conn.QueryFirstOrDefaultAsync<SVN_AppSetting_v1UI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                }
                return appSetting;
            }
            catch
            {
                return null;
            }
        }

        public async Task<int> UpdateSettingValue(string Key, string Value, string Group = "Dashboard")
        {
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = "UPDATE SVN_AppSetting SET [Value] = @Value, ModifiedOn = GETDATE() WHERE [Group] = @Group AND [Key] = @Key";
                    return await conn.ExecuteAsync(sql, new { Group, Key, Value }, commandTimeout: timeOut);
                }
            }
            catch
            {
                return 0;
            }
        }

        public async Task<int> BackupSetting(string sourceKey, string backupKey, string Group = "Dashboard")
        {
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = @"
                        UPDATE SVN_AppSetting 
                        SET [Value] = (SELECT TOP 1 [Value] FROM SVN_AppSetting WHERE [Group] = @Group AND [Key] = @sourceKey),
                            ModifiedOn = GETDATE()
                        WHERE [Group] = @Group AND [Key] = @backupKey";
                    return await conn.ExecuteAsync(sql, new { Group, sourceKey, backupKey }, commandTimeout: timeOut);
                }
            }
            catch
            {
                return 0;
            }
        }
    }
}
