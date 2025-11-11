namespace SVN_Portal.DAL.DTO
{
    public class SVN_ACT_downtimeUI
    {
        public string OperationName { get; set; }
        public string WorkDate { get; set; }
        public DateTime? StopTime { get; set; }
        public DateTime? RunTime { get; set; }
        public DateTime? first_finish_date_time { get; set; }
        public DateTime? last_finish_date_time { get; set; }
        public DateTime? adjusted_first_finish_date_time { get; set; }
        public double LanDauTien { get; set; }
        public double KhongPhaiLanDau { get; set; }
        public double TongDowntime { get; set; }

    }
}
