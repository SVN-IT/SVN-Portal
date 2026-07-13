using Sigma_Dashboard.Services.Configurations;
using SVNShareLib.DAL.NewDashboard;

namespace Sigma_Dashboard.Services
{
    public class AppSettingServices
    {
        string connectionString;
        DBConfiguration dBConfiguration;
        public AppSettingServices(DBConfiguration dBConfiguration)
        {
            this.dBConfiguration = dBConfiguration;
            this.connectionString = dBConfiguration.GetConnectionString();
        }

        public async Task<OperInfoConfig> GetOperInfoConfig()
        {
            OperInfoConfig operInfoConfig = new OperInfoConfig();
            var dataPortal = new SVN_AppSetting_v1DataPortal(connectionString);
            try
            {
                var appSetting = await dataPortal.GetSettingByGroupAndKey("OperInfo");
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

        public async Task<List<SectionConfig>> GetSectionConfigs()
        {
            List<SectionConfig> sectionConfigs = new List<SectionConfig>();
            var dataPortal = new SVN_AppSetting_v1DataPortal(connectionString);
            try
            {
                var appSetting = await dataPortal.GetSettingByGroupAndKey("SectionConfig");
                if (appSetting != null && !string.IsNullOrWhiteSpace(appSetting.Value))
                {
                    sectionConfigs = System.Text.Json.JsonSerializer.Deserialize<List<SectionConfig>>(appSetting.Value);
                }
                return sectionConfigs;
            }
            catch
            {
                return sectionConfigs;
            }
        }

        public async Task<double> GetCostPerDay()
        {
            double cost = 0;
            var dataPortal = new SVN_AppSetting_v1DataPortal(connectionString);
            try
            {
                var appSetting = await dataPortal.GetSettingByGroupAndKey("Cost");
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
            var dataPortal = new SVN_AppSetting_v1DataPortal(connectionString);
            try
            {
                var appSetting = await dataPortal.GetSettingByGroupAndKey("YearCost");
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
            var dataPortal = new SVN_AppSetting_v1DataPortal(connectionString);
            try
            {
                var appSetting = await dataPortal.GetSettingByGroupAndKey("CompanyStartDate");
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
            var dataPortal = new SVN_AppSetting_v1DataPortal(connectionString);
            try
            {
                var appSetting = await dataPortal.GetSettingByGroupAndKey("LocalCompanyCode");
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
            var dataPortal = new SVN_AppSetting_v1DataPortal(connectionString);
            try
            {
                var appSetting = await dataPortal.GetSettingByGroupAndKey("VNDRate");
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
            var dataPortal = new SVN_AppSetting_v1DataPortal(connectionString);
            try
            {
                var appSetting = await dataPortal.GetSettingByGroupAndKey("CurrencyInfo");
                if (appSetting != null && !string.IsNullOrWhiteSpace(appSetting.Value))
                {
                    operCurrencyInfo = System.Text.Json.JsonSerializer.Deserialize<List<CurrencyInfo>>(appSetting.Value);
                }

                if (operCurrencyInfo != null && operCurrencyInfo.Count > 0)
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

        public async Task<string> GetOperationSetupInProcess()
        {
            string strValue = string.Empty;
            var dataPortal = new SVN_AppSetting_v1DataPortal(connectionString);
            try
            {
                var appSetting = await dataPortal.GetSettingByGroupAndKey("OperationSetupInProcess");
                if (appSetting != null && !string.IsNullOrWhiteSpace(appSetting.Value))
                {
                    strValue = appSetting.Value;
                }
                return strValue;
            }
            catch
            {
                return strValue;
            }
        }

        public async Task<string> GetOperationInSetupStatus()
        {
            string strValue = string.Empty;
            var dataPortal = new SVN_AppSetting_v1DataPortal(connectionString);
            try
            {
                var appSetting = await dataPortal.GetSettingByGroupAndKey("OperationInSetupStatus");
                if (appSetting != null && !string.IsNullOrWhiteSpace(appSetting.Value))
                {
                    strValue = appSetting.Value;
                }
                return strValue;
            }
            catch
            {
                return strValue;
            }
        }

        public async Task<string> GetMasterOperList()
        {
            string strValue = string.Empty;
            var dataPortal = new SVN_AppSetting_v1DataPortal(connectionString);
            try
            {
                var appSetting = await dataPortal.GetSettingByGroupAndKey("MasterOperList");
                if (appSetting != null && !string.IsNullOrWhiteSpace(appSetting.Value))
                {
                    strValue = appSetting.Value;
                }
                return strValue;
            }
            catch
            {
                return strValue;
            }
        }

        public async Task<string> GetInSetupStatusFontSize()
        {
            string strValue = string.Empty;
            var dataPortal = new SVN_AppSetting_v1DataPortal(connectionString);
            try
            {
                var appSetting = await dataPortal.GetSettingByGroupAndKey("InSetupStatusFontSize");
                if (appSetting != null && !string.IsNullOrWhiteSpace(appSetting.Value))
                {
                    strValue = appSetting.Value;
                }
                return strValue;
            }
            catch
            {
                return strValue;
            }
        }

        public async Task<string> GetTimeChangeTabMainDashboard()
        {
            string strValue = string.Empty;
            var dataPortal = new SVN_AppSetting_v1DataPortal(connectionString);
            try
            {
                var appSetting = await dataPortal.GetSettingByGroupAndKey("TimeChangeTabMainDashboard");
                if (appSetting != null && !string.IsNullOrWhiteSpace(appSetting.Value))
                {
                    strValue = appSetting.Value;
                }
                return strValue;
            }
            catch
            {
                return strValue;
            }
        }
    }
}
