using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO.NewDashboard
{
    public class SVN_quantity_reason_v1UI
    {
        public string id { get; set; }
        public string create_uid { get; set; }
        public string write_uid { get; set; }
        public string name { get; set; }
        public string create_date { get; set; }
        public string write_date { get; set; }
        public string code { get; set; }
        public string priority { get; set; }
        public string operation { get; set; }
    }
}
