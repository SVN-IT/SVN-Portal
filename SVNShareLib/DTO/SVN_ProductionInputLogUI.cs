using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class SVN_ProductionInputLogUI
    {
        public int id { get; set; }
        public int level { get; set; }
        public int product_id { get; set; }
        public decimal product_qty { get; set; }
        public DateTime date_finished { get; set; }
        public string product_type { get; set; }
        public string serial_code { get; set; }
        public string component_list { get; set; }
        public string state { get; set; }
        public string API_function { get; set; }
        public string API_parameters { get; set; }
        public string status { get; set; }
        public string wo_code { get; set; }
        public string master_wo_code { get; set; }
        public decimal total_qty { get; set; }
        public decimal remain_qty { get; set; }
        public string consumed_wo_code { get; set; }
    }
}
