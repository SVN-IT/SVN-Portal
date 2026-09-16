namespace SVN_Portal.DAL.DTO
{
    public class SVN_ProductEnterScanInputLogEntryUI
    {
        public int product_id { get; set; }
        public string product_name { get; set; }
        public string field_type { get; set; }
        public string has_tracking { get; set; }
        public string scanned_value { get; set; }
        public DateTime scan_time { get; set; }
    }
}
