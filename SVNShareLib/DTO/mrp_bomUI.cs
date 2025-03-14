using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SVNShareLib.DTO
{
    class mrp_bomUI
    {
        public int id { get; set; }
        public int message_main_attachment_id { get; set; }
        public int product_tmpl_id { get; set; }
        public int product_id { get; set; }
        public int product_uom_id { get; set; }
        public int sequence { get; set; }
        public int picking_type_id { get; set; }
        public int company_id { get; set; }
        public int create_uid { get; set; }
        public int write_uid { get; set; }
        public int origin_message_id { get; set; }
        public int origin_references { get; set; }
        public int code { get; set; }
        public int type { get; set; }
        public int ready_to_produce { get; set; }
        public int consumption { get; set; }
        public int product_qty { get; set; }
        public int active { get; set; }
        public int allow_operation_dependencies { get; set; }
        public int create_date { get; set; }
        public int write_date { get; set; }
        public int version { get; set; }
        public int previous_bom_id { get; set; }

    }
}
