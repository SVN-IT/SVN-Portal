using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class stock_move_lineUI
    {
        //SVN_stock_move_line
        public int id { get; set; }
        public int picking_id { get; set; }
        public int move_id { get; set; }
        public int company_id { get; set; }
        public int product_id { get; set; }
        public int product_uom_id { get; set; }
        public int package_id { get; set; }
        public int package_level_id { get; set; }
        public int lot_id { get; set; }
        public int result_package_id { get; set; }
        public int owner_id { get; set; }
        public int location_id { get; set; }
        public int location_dest_id { get; set; }
        public int create_uid { get; set; }
        public int write_uid { get; set; }
        public string product_category_name { get; set; }
        public string lot_name { get; set; }
        public string state { get; set; }
        public string reference { get; set; }
        public string description_picking { get; set; }
        public decimal reserved_qty { get; set; }
        public decimal reserved_uom_qty { get; set; }
        public decimal qty_done { get; set; }
        public DateTime date { get; set; }
        public DateTime create_date { get; set; }
        public DateTime write_date { get; set; }
        public int workorder_id { get; set; }
        public int production_id { get; set; }
        public int equipment_id { get; set; }
        public bool can_create_equipment { get; set; }
        public bool location_processed { get; set; }
        public object? consume_line_ids { get; set; }
    }
}
