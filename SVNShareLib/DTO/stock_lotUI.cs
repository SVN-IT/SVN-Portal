using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace SVNShareLib.DTO
{
    class stock_lotUI
    {
        public int id { get; set; }
        public int message_main_attachment_id { get; set; }
        public int product_id { get; set; }
        public int product_uom_id { get; set; }
        public int company_id { get; set; }
        public int create_uid { get; set; }
        public int write_uid { get; set; }
        public int origin_message_id { get; set; }
        public int origin_references { get; set; }
        public int name { get; set; }
        public int Ref { get; set; }
        public int note { get; set; }
        public int create_date { get; set; }
        public int write_date { get; set; }
        public int customer_id { get; set; }
        public int supplier_id { get; set; }
        public int country_state_id { get; set; }
        public int equipment_id { get; set; }

    }
}
