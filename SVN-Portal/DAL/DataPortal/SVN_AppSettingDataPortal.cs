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

        public async Task<double> GetCostInYear()
        {
            double cost = 0;
            try
            {
                var appSetting = await GetSettingByGroupAndKey("YearCost");
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
        public async Task<string> GetStartDate()
        {
            string companyStartDate = string.Empty;
            try
            {
                var appSetting = await GetSettingByGroupAndKey("CompanyStartDate");
                if (appSetting != null && !string.IsNullOrWhiteSpace(appSetting.Value))
                {
                    companyStartDate = appSetting.Value;
                }
                return companyStartDate;
            }
            catch
            {
                return companyStartDate;
            }
        }

        public async Task<string> GetLocalCompanyCode()
        {
            string companyCode = "SVN";
            try
            {
                var appSetting = await GetSettingByGroupAndKey("LocalCompanyCode");
                if (appSetting != null && !string.IsNullOrWhiteSpace(appSetting.Value))
                {
                    companyCode = appSetting.Value;
                }
                return companyCode;
            }
            catch
            {
                return companyCode;
            }
        }

        public async Task<double> GetVNDRate()
        {
            double rate = 26152.137911;
            try
            {
                var appSetting = await GetSettingByGroupAndKey("VNDRate");
                if (appSetting != null && !string.IsNullOrWhiteSpace(appSetting.Value))
                {
                    rate = double.Parse(appSetting.Value);
                }
                return rate;
            }
            catch
            {
                return rate;
            }
        }

        public async Task<string> GetCurrencyInfoByCode(string code)
        {
            List<CurrencyInfo> operCurrencyInfo = new List<CurrencyInfo>();
            try
            {
                var appSetting = await GetSettingByGroupAndKey("CurrencyInfo");
                if (appSetting != null && !string.IsNullOrWhiteSpace(appSetting.Value))
                {
                    operCurrencyInfo = System.Text.Json.JsonSerializer.Deserialize<List<CurrencyInfo>>(appSetting.Value);
                }

                if(operCurrencyInfo != null && operCurrencyInfo.Count > 0)
                {
                    var currency = operCurrencyInfo.Where(c => c.Code.ToLower() == code.ToLower()).FirstOrDefault();
                    if (currency != null)
                    {
                        return currency.Currency;
                    }
                }

                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
