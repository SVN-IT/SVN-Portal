namespace SVN_Portal.Models
{
    public class CostDailyViewModel
    {
        public string Date { get; set; }
        public double Cost { get; set; }
        public double TargetRevenue { get; set; }
        public double ActualRevenue { get; set; }
        public double RevenueRate { get; set; }
        public string Currency { get; set; }
    }
}
