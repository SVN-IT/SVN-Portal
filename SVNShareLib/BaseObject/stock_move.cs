using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.BaseObject
{
    public class stock_move
    {
        public int id { get; set; }
        public int sequence { get; set; }
        public object? company_id { get; set; }
        public object? product_id { get; set; }
        public object? product_uom { get; set; }
        public object? location_id { get; set; }
        public object? location_dest_id { get; set; }
        public object? partner_id { get; set; }
        public object? picking_id { get; set; }
        public object? group_id { get; set; }
        public object? rule_id { get; set; }
        public object? picking_type_id { get; set; }
        public object? origin_returned_move_id { get; set; }
        public object? restrict_partner_id { get; set; }
        public object? warehouse_id { get; set; }
        public object? package_level_id { get; set; }
        public int next_serial_count { get; set; }
        public object? orderpoint_id { get; set; }
        public object? product_packaging_id { get; set; }
        public object? create_uid { get; set; }
        public object? write_uid { get; set; }
        public string name { get; set; }
        public string priority { get; set; }
        public string state { get; set; }
        public string origin { get; set; }
        public string procure_method { get; set; }
        public string reference { get; set; }
        public object? next_serial { get; set; }
        public DateTime reservation_date { get; set; }
        public object? description_picking { get; set; }
        public decimal product_qty { get; set; }
        public decimal product_uom_qty { get; set; }
        public decimal quantity_done { get; set; }
        public bool scrapped { get; set; }
        public bool propagate_cancel { get; set; }
        public bool is_inventory { get; set; }
        public bool additional { get; set; }
        public DateTime date { get; set; }
        public DateTime date_deadline { get; set; }
        public object? delay_alert_date { get; set; }
        public DateTime create_date { get; set; }
        public DateTime write_date { get; set; }
        public decimal price_unit { get; set; }
        public bool is_done { get; set; }
        public decimal unit_factor { get; set; }
        public object? created_production_id { get; set; }
        public object? production_id { get; set; }
        public object? raw_material_production_id { get; set; }
        public object? unbuild_id { get; set; }
        public object? consume_unbuild_id { get; set; }
        public object? operation_id { get; set; }
        public object? workorder_id { get; set; }
        public object? bom_line_id { get; set; }
        public object? byproduct_id { get; set; }
        public object? order_finished_lot_id { get; set; }
        public decimal cost_share { get; set; }
        public bool manual_consumption { get; set; }
        public object? analytic_account_line_id { get; set; }
        public object? to_refund { get; set; }
        public object? purchase_line_id { get; set; }
        public object? created_purchase_line_id { get; set; }
        public object? component_standard_consumption_id { get; set; }
        public object? byproduct_standard_consumption_id { get; set; }
        public object? sale_line_id { get; set; }
    }
}
