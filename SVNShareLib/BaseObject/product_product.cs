using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.BaseObject
{
    public class product_product
    {
        public int id { get; set; }
        public object message_main_attachment_id { get; set; }
        public object product_tmpl_id { get; set; }
        public object create_uid { get; set; }
        public object write_uid { get; set; }
        public string origin_message_id { get; set; }
        public string origin_references { get; set; }
        public string default_code { get; set; }
        public string barcode { get; set; }
        public string combination_indices { get; set; }
        public decimal volume { get; set; }
        public decimal weight { get; set; }
        public bool active { get; set; }
        public bool can_image_variant_1024_be_zoomed { get; set; }
        public object create_date { get; set; }
        public object write_date { get; set; }
    }
}
