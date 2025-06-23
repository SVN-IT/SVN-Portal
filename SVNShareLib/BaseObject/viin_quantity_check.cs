using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.BaseObject
{
    public class viin_quantity_check
    {
        public int id { get; set; }
        public object? message_main_attachment_id { get; set; }
        public object? point_id { get; set; }
        public object? user_id { get; set; }
        public object team_id { get; set; }
        public object? company_id { get; set; }
        public object? type_id { get; set; }
        public object? product_id { get; set; }
        public object? create_uid { get; set; }
        public object? write_uid { get; set; }
        public string origin_message_id { get; set; }
        public string origin_references { get; set; }
        public string name { get; set; }
        public string measure_success { get; set; }
        public string quality_state { get; set; }
        public string comment { get; set; }
        public decimal? measure { get; set; }
        public decimal? tolerance_min { get; set; }
        public decimal? tolerance_max { get; set; }
        public decimal? norm { get; set; }
        public object? control_date { get; set; }
        public object? create_date { get; set; }
        public object? write_date { get; set; }
        public double? quantity_to_check { get; set; }
        public double? checked_quantity { get; set; }
        public double? checked_qty_deviation { get; set; }
        public object? picking_id { get; set; }
        public object? lot_id { get; set; }
        public object? picking_type_id { get; set; }
        public object? procurement_group_id { get; set; }
        public string origin { get; set; }
        public object? production_id { get; set; }
        public object? workorder_id { get; set; }
        public object? workcenter_id { get; set; }
        public string x_description { get; set; }
    }
}
