namespace SVN_Portal.Services.Configurations
{
    public class AppConfig
    {
        public string ProductMode { get; set; }
        public string MasterOperList { get; set; }
        public string OperList { get; set; } //không dùng nữa
        public int TimeBeforeReload { get; set; }
        public int ChartCol { get; set; }
        public int Rounding { get; set; }
        public string ShowSingleChart { get; set; }
        public int timeChangeTabMainDashboard { get; set; }
        public string DefaultCompany { get; set; }
        public int AppID { get; set; }
        public int CompanyID { get; set; }

    }

    public class CurrencyInfo
    {
        public string Code { get; set; }
        public string Currency { get; set; }
    }
}
