using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class product_templateUI
    {
        public int id { get; set; }
        public int message_main_attachment_id { get; set; }
        public int sequence { get; set; }
        public int categ_id { get; set; }
        public int uom_id { get; set; }
        public int uom_po_id { get; set; }
        public int company_id { get; set; }
        public int color { get; set; }
        public int create_uid { get; set; }
        public int write_uid { get; set; }
        public string origin_message_id { get; set; }
        public string origin_references { get; set; }
        public string detailed_type { get; set; }
        public string type { get; set; }
        public string default_code { get; set; }
        public string priority { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string description_purchase { get; set; }
        public string description_sale { get; set; }
        public decimal list_price { get; set; }
        public decimal volume { get; set; }
        public decimal weight { get; set; }
        public bool sale_ok { get; set; }
        public bool purchase_ok { get; set; }
        public bool active { get; set; }
        public bool can_image_1024_be_zoomed { get; set; }
        public bool has_configurable_attributes { get; set; }
        public DateTime create_date { get; set; }
        public DateTime write_date { get; set; }
        public string tracking { get; set; }
        public string description_picking { get; set; }
        public string description_pickingout { get; set; }
        public string description_pickingin { get; set; }
        public float sale_delay { get; set; }
        public float produce_delay { get; set; }
        public float days_to_prepare_mo { get; set; }
        public string purchase_method { get; set; }
        public string purchase_line_warn { get; set; }
        public string purchase_line_warn_msg { get; set; }
        public string service_type { get; set; }
        public string sale_line_warn { get; set; }
        public string expense_policy { get; set; }
        public string invoice_policy { get; set; }
        public string sale_line_warn_msg { get; set; }
        public int technician_user_id { get; set; }
        public string equipment_assign_to { get; set; }
        public string period_uom { get; set; }
        public float recurring_sale_price { get; set; }
        public string service_tracking { get; set; }
    }
}
