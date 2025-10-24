namespace SVNShareLib.DTO
{
    public class SVN_Downtime_InfoUI
    {
        public int Id { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string State { get; set; }
        public string Operation { get; set; }
        public string EstimateTime { get; set; }
        public string Description { get; set; }
        public string Image { get; set; }
        public DateTime Datetime { get; set; }
        public string SVNCode { get; set; }
    }
}
