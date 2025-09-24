namespace SVNShareLib.DTO
{
    public class SVN_Calc_Run_Duration_UI
    {
        public int SessionID { get; set; }
        public DateTime MinStartTime { get; set; }
        public DateTime MaxEndTime { get; set; }
        public double DurationMinutes { get; set; }
        public double DurationHours { get; set; }
        public string CodesInGroup { get; set; }
        public string MaxESTTime { get; set; }
    }
}
