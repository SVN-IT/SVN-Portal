using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class stock_moveUI
    {
        //SVN_stock_move
        public int id { get; set; }
        public int sequence { get; set; }
        public int company_id { get; set; }
        public int product_id { get; set; }
        public int product_uom { get; set; }
        public int location_id { get; set; }
        public int location_dest_id { get; set; }
        public int partner_id { get; set; }
        public int picking_id { get; set; }
        public int group_id { get; set; }
        public int rule_id { get; set; }
        public int picking_type_id { get; set; }
        public int origin_returned_move_id { get; set; }
        public int restrict_partner_id { get; set; }
        public int warehouse_id { get; set; }
        public int package_level_id { get; set; }
        public int next_serial_count { get; set; }
        public int orderpoint_id { get; set; }
        public int product_packaging_id { get; set; }
        public int create_uid { get; set; }
        public int write_uid { get; set; }
        public string name { get; set; }
        public string priority { get; set; }
        public string state { get; set; }
        public string origin { get; set; }
        public string procure_method { get; set; }
        public string reference { get; set; }
        public string next_serial { get; set; }
        public DateTime reservation_date { get; set; }
        public string description_picking { get; set; }
        public decimal product_qty { get; set; }
        public decimal product_uom_qty { get; set; }
        public decimal quantity_done { get; set; }
        public bool scrapped { get; set; }
        public bool propagate_cancel { get; set; }
        public bool is_inventory { get; set; }
        public bool additional { get; set; }
        public DateTime date { get; set; }
        public DateTime date_deadline { get; set; }
        public DateTime delay_alert_date { get; set; }
        public DateTime create_date { get; set; }
        public DateTime write_date { get; set; }
        public decimal price_unit { get; set; }
        public bool is_done { get; set; }
        public decimal unit_factor { get; set; }
        public int created_production_id { get; set; }
        public int production_id { get; set; }
        public int raw_material_production_id { get; set; }
        public int unbuild_id { get; set; }
        public int consume_unbuild_id { get; set; }
        public int operation_id { get; set; }
        public int workorder_id { get; set; }
        public int bom_line_id { get; set; }
        public int byproduct_id { get; set; }
        public int order_finished_lot_id { get; set; }
        public decimal cost_share { get; set; }
        public bool manual_consumption { get; set; }
        public int analytic_account_line_id { get; set; }
        public int to_refund { get; set; }
        public int purchase_line_id { get; set; }
        public int created_purchase_line_id { get; set; }
        public int component_standard_consumption_id { get; set; }
        public int byproduct_standard_consumption_id { get; set; }
        public int sale_line_id { get; set; }
    }
}
