using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.BaseObject
{
    public class stock_lot
    {
        public int id { get; set; }
        public object? message_main_attachment_id { get; set; }
        public object? product_id { get; set; }
        public object? product_uom_id { get; set; }
        public object? company_id { get; set; }
        public object? create_uid { get; set; }
        public object? write_uid { get; set; }
        public string origin_message_id { get; set; }
        public string origin_references { get; set; }
        public string name { get; set; }
        public string Ref { get; set; }
        public string note { get; set; }
        public DateTime create_date { get; set; }
        public DateTime write_date { get; set; }
        public object? customer_id { get; set; }
        public object? supplier_id { get; set; }
        public object? country_state_id { get; set; }
        public object? equipment_id { get; set; }
    }
}
