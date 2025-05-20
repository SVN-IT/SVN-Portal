using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class mrp_unbuildUI
    {
        public int id { get; set; }
        public int message_main_attachment_id { get; set; }
        public int product_id { get; set; }
        public int company_id { get; set; }
        public int product_uom_id { get; set; }
        public int bom_id { get; set; }
        public int mo_id { get; set; }
        public int lot_id { get; set; }
        public int location_id { get; set; }
        public int location_dest_id { get; set; }
        public int create_uid { get; set; }
        public int write_uid { get; set; }
        public string origin_message_id { get; set; }
        public string origin_references { get; set; }
        public string name { get; set; }
        public string state { get; set; }
        public DateTime create_date { get; set; }
        public DateTime write_date { get; set; }
        public float product_qty { get; set; }
    }
}
