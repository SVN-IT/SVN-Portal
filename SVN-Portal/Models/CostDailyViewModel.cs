namespace SVN_Portal.Models
{
    public class CostDailyViewModel
    {
        public CostDailyViewModel()
        {
            CostYearlyPerOpers = new List<CostYearlyPerOperViewModel>();
        }
        public string Date { get; set; }
        public double Cost { get; set; }
        public double TargetRevenue { get; set; }
        public double ActualRevenue { get; set; }
        public double RevenueRate { get; set; }
        public string Currency { get; set; }
        public List<CostYearlyPerOperViewModel> CostYearlyPerOpers { get; set; }
    }

    public class CostYearlyPerOperViewModel
    {
        public string Operation { get; set; }
        public double Cost { get; set; }
        public double TargetRevenue { get; set; }
        public double ActualRevenue { get; set; }
    }
}
