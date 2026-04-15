using System.Data.SqlTypes;

namespace SVN_Portal.Services.Configurations
{
    public class SectionConfig
    {
        public string CompanyCode { get; set; }
        public string DaySection { get; set; }
        public string NightSection { get; set; }
        public int Hour { get; set; }
    }
}
