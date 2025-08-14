namespace SVN_Portal.Services.Configurations
{
    public class OperInfoConfig
    {
        public List<OperInfo> OperInfo { get; set; }
    }
    public class OperInfo
    {
        public string Operation { get; set; }
        public string Name { get; set; }
        public string MasterOperation { get; set; }
        public string WCName { get; set; }
        public List<int> Produce_id { get; set; }
        public List<WC> WC { get; set; }
        public int ColWidth { get; set; }
        public int StoreID { get; set; }
        public int Top_row { get; set; }
    }

    public class WC
    {
        public string WCName { get; set; }
        public List<int> Produce_id { get; set; }
        public int Top_row { get; set; }
    }
}
