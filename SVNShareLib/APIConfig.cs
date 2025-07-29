using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib
{
    public class APIConfig
    {
        public string ReadMrpProductionAPIURL { get; set; }
        public string lang { get; set; }
        public string tz { get; set; }
        public int uid { get; set; }
        public int[] allowed_company_ids { get; set; }
        public bool bin_Size { get; set; }
        public int id { get; set; }
        public int cids { get; set; }
        public int menu_id { get; set; }
        public int action { get; set; }
        public string model { get; set; }
        public string view_type { get; set; }
        public int default_company_id { get; set; }
    }
}
