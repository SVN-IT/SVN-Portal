namespace SVNShareLib.DTO
{
    public class SVN_Top_Downtime_UI
    {
        public string Operation { get; set; }
        public string Code { get; set; }
        public string IssCode { get; set; }
        public string ErrorName { get; set; }
        public int Occurrences { get; set; }
        public double TotalMinutes { get; set; }
    }
}
