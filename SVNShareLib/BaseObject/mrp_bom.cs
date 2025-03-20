using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.BaseObject
{
    public class mrp_bom
    {
        public int id { get; set; }
        public int message_main_attachment_id { get; set; }
        public int product_tmpl_id { get; set; }
        public object? product_id { get; set; }
        public object? product_uom_id { get; set; }
        public int sequence { get; set; }
        public int picking_type_id { get; set; }
        public int company_id { get; set; }
        public int create_uid { get; set; }
        public int write_uid { get; set; }
        public int origin_message_id { get; set; }
        public string origin_references { get; set; }
        public string code { get; set; }
        public string type { get; set; }
        public string ready_to_produce { get; set; }
        public string consumption { get; set; }
        public decimal product_qty { get; set; }
        public bool active { get; set; }
        public bool allow_operation_dependencies { get; set; }
        public DateTime create_date { get; set; }
        public DateTime write_date { get; set; }
        public int version { get; set; }
        public int previous_bom_id { get; set; }
    }
}
