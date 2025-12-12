using Dapper;
using SVN_Portal.DAL.DTO;
using SVN_Portal.Services.Configurations;
using System.Data;
using System.Data.SqlClient;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SVN_Portal.DAL.DataPortal
{
    public class SVN_AppSettingDataPortal
    {
        string connectionString = "";
        public SVN_AppSettingDataPortal(string connectionString)
        {
            this.connectionString = connectionString;
        }

        public async Task<SVN_AppSettingUI> GetSettingByGroupAndKey(string Key, string Group = "Dashboard")
        {
            SVN_AppSettingUI appSetting = new SVN_AppSettingUI();
            int timeOut = 1000;
            try
            {
                using (IDbConnection conn = new SqlConnection(connectionString))
                {
                    string sql = string.Empty;
                    var param = new object();
                    sql = "select * from SVN_AppSetting where [Group] = @Group and [Key] = @Key";
                    param = new { Group = Group, Key = Key };
                    appSetting = await conn.QueryFirstOrDefaultAsync<SVN_AppSettingUI>(sql, param, commandTimeout: timeOut, commandType: CommandType.Text);
                }
                return appSetting;
            }
            catch
            {
                return null;
            }
        }

        public async Task<OperInfoConfig> GetOperInfoConfig()
        {
            OperInfoConfig operInfoConfig = new OperInfoConfig();
            try
            {
                var appSetting = await GetSettingByGroupAndKey("OperInfo");
                if (appSetting != null && !string.IsNullOrWhiteSpace(appSetting.Value))
                {
                    operInfoConfig.OperInfo = System.Text.Json.JsonSerializer.Deserialize<List<OperInfo>>(appSetting.Value);
                }
                return operInfoConfig;
            }
            catch
            {
                return operInfoConfig;
            }
        }

        public async Task<double> GetCostPerDay()
        {
            double cost = 0;
            try
            {
                var appSetting = await GetSettingByGroupAndKey("Cost");
                if (appSetting != null && !string.IsNullOrWhiteSpace(appSetting.Value))
                {
                    cost = Convert.ToDouble(appSetting.Value);
                }
                return cost;
            }
            catch
            {
                return cost;
            }
        }
    }
}
