using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.BaseObject
{
    public class mrp_production
    {
        public int id { get; set; }
        public int message_main_attachment_id { get; set; }
        public int backorder_sequence { get; set; }
        public object? product_id { get; set; }
        public object? product_uom_id { get; set; }
        public object? lot_producing_id { get; set; }
        public int picking_type_id { get; set; }
        public int location_src_id { get; set; }
        public int location_dest_id { get; set; }
        public object? bom_id { get; set; }
        public int user_id { get; set; }
        public int company_id { get; set; }
        public int procurement_group_id { get; set; }
        public int orderpoint_id { get; set; }
        public int production_location_id { get; set; }
        public int create_uid { get; set; }
        public int write_uid { get; set; }
        public string origin_message_id { get; set; }
        public string origin_references { get; set; }
        public string name { get; set; }
        public string priority { get; set; }
        public string origin { get; set; }
        public string state { get; set; }
        public string reservation_state { get; set; }
        public string product_description_variants { get; set; }
        public string consumption { get; set; }
        public decimal product_qty { get; set; }
        public decimal qty_producing { get; set; }
        public bool propagate_cancel { get; set; }
        public bool is_locked { get; set; }
        public bool is_planned { get; set; }
        public bool allow_workorder_dependencies { get; set; }
        public DateTime? date_planned_start { get; set; }
        public DateTime? date_planned_finished { get; set; }
        public DateTime? date_deadline { get; set; }
        public object? date_start { get; set; }
        public object? date_finished { get; set; }
        public DateTime? create_date { get; set; }
        public DateTime? write_date { get; set; }
        public decimal product_uom_qty { get; set; }
        public int analytic_account_id { get; set; }
        public decimal extra_cost { get; set; }
        public string x_Svn_customer_SN { get; set; }
    }
}
