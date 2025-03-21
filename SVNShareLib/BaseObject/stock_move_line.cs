using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.BaseObject
{
    public class stock_move_line
    {
        public int id { get; set; }
        public object? picking_id { get; set; }
        public object? move_id { get; set; }
        public object? company_id { get; set; }
        public object? product_id { get; set; }
        public object? product_uom_id { get; set; }
        public object? package_id { get; set; }
        public object? package_level_id { get; set; }
        public object? lot_id { get; set; }
        public object? result_package_id { get; set; }
        public object? owner_id { get; set; }
        public object? location_id { get; set; }
        public object? location_dest_id { get; set; }
        public object? create_uid { get; set; }
        public object? write_uid { get; set; }
        public string product_category_name { get; set; }
        public object? lot_name { get; set; }
        public string state { get; set; }
        public string reference { get; set; }
        public object? description_picking { get; set; }
        public decimal reserved_qty { get; set; }
        public decimal reserved_uom_qty { get; set; }
        public decimal qty_done { get; set; }
        public DateTime date { get; set; }
        public DateTime create_date { get; set; }
        public DateTime write_date { get; set; }
        public object? workorder_id { get; set; }
        public object? production_id { get; set; }
        public object? equipment_id { get; set; }
        public bool can_create_equipment { get; set; }
        public bool location_processed { get; set; }
    }
}
