using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.BaseObject
{
    public class mrp_bom_line
    {
        public int id { get; set; }
        public object product_id { get; set; }
        public object product_tmpl_id { get; set; }
        public int company_id { get; set; }
        public object product_uom_id { get; set; }
        public int sequence { get; set; }
        public object bom_id { get; set; }
        public int operation_id { get; set; }
        public int create_uid { get; set; }
        public int write_uid { get; set; }
        public decimal product_qty { get; set; }
        public bool manual_consumption { get; set; }
        public DateTime create_date { get; set; }
        public DateTime write_date { get; set; }
        public decimal cost_share { get; set; }
        public decimal standard_qty { get; set; }
        public decimal loss_rate { get; set; }
    }
}
