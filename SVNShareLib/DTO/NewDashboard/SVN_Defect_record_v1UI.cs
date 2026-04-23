using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO.NewDashboard
{
    public class SVN_Defect_record_v1UI
    {
        public string Item_code { get; set; }
        public string Defect_Code { get; set; }
        public int Qty_NG { get; set; }
        public string INSDatetime { get; set; }
        public string Operation { get; set; }
        public string Employer_code { get; set; }
        public string Employ_name { get; set; }
    }
}
