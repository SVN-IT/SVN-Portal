namespace SVN_Portal.Services.Configurations
{
    public class AppConfig
    {
        public string ProductMode { get; set; }
        public string MasterOperList { get; set; }
        public string OperList { get; set; } //không dùng nữa
        public int TimeBeforeReload { get; set; }
        public int ChartCol { get; set; }
        public string ShowSingleChart { get; set; }

    }
}
