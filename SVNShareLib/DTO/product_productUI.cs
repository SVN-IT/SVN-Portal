using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class product_productUI
    {
        public int id { get; set; }
        public int message_main_attachment_id { get; set; }
        public int product_tmpl_id { get; set; }
        public int create_uid { get; set; }
        public int write_uid { get; set; }
        public string origin_message_id { get; set; }
        public string origin_references { get; set; }
        public string default_code { get; set; }
        public string barcode { get; set; }
        public string combination_indices { get; set; }
        public decimal volume { get; set; }
        public decimal weight { get; set; }
        public bool active { get; set; }
        public bool can_image_variant_1024_be_zoomed { get; set; }
        public DateTime create_date { get; set; }
        public DateTime write_date { get; set; }
    }
}
