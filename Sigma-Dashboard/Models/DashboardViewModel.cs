namespace Sigma_Dashboard.Models
{
    public class PageDashboardViewModel
    {
        public PageDashboardViewModel()
        {
            DashboardData = new List<DashboardViewModel>();
        }
        public List<DashboardViewModel> DashboardData { get; set; }
    }

    public class DashboardViewModel
    {
        public DashboardViewModel()
        {
            BarChartData = new List<BarChartData>();
        }
        public string[] Sections { get; set; }
        public List<BarChartData> BarChartData { get; set; }
    }

    public class BarChartData
    {
        public string Operation { get; set; }
        public string Line { get; set; }
        public string ChartTitle { get; set; }
        public string ChartID { get; set; }
        public string TargetLabel { get; set; }
        public double[] TargetData { get; set; }
        public string ActualLabel { get; set; }
        public double[] ActualData { get; set; }
        public string[] DefectLabel { get; set; }
        public int[] DefectData { get; set; }
        public string HourlyPlanData { get; set; }
        public string HourlyPlanPercent { get; set; }
        public string UPHData { get; set; }
        public string UPHPercent { get; set; }
        public string UPPHData { get; set; }
        public string UPPHPercent { get; set; }
        public string DefectInfo { get; set; }
        public string DefectPercent { get; set; }
        public string CheckListStatus { get; set; }
        public string WorkingTimeStatus { get; set; }
        public string DowntimeStatus { get; set; }
    }

    public class DefectData
    {
        public string Category { get; set; }
        public int Value { get; set; }
    }
}
