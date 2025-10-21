namespace AutomationService.Configurations
{
    public class APIConfiguration
    {
        public string BaseURL { get; set; }
        public int Timeout { get; set; }
        public int TimeReload { get; set; }
        public List<APIURL> APIURL { get; set; }
    }

    public class APIURL
    {
        public int TimeReload { get; set; }
        public string URL { get; set; }

    }
}
