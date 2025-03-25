namespace SVN_Portal.Services.Configurations
{
    public class OperInfoConfig
    {
        public List<OperInfo> OperInfo { get; set; }
    }
    public class OperInfo
    {
        public string Operation { get; set; }
        public string WC { get; set; }
    }
}
