using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class viin_quantity_checkUI
    {
        public int id { get; set; }
        public int? message_main_attachment_id { get; set; }
        public int? point_id { get; set; }
        public int? user_id { get; set; }
        public int team_id { get; set; }
        public int? company_id { get; set; }
        public int? type_id { get; set; }
        public int? product_id { get; set; }
        public int? create_uid { get; set; }
        public int? write_uid { get; set; }
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
        public DateTime? control_date { get; set; }
        public DateTime? create_date { get; set; }
        public DateTime? write_date { get; set; }
        public double? quantity_to_check { get; set; }
        public double? checked_quantity { get; set; }
        public double? checked_qty_deviation { get; set; }
        public int? picking_id { get; set; }
        public int? lot_id { get; set; }
        public int? picking_type_id { get; set; }
        public int? procurement_group_id { get; set; }
        public string origin { get; set; }
        public int? production_id { get; set; }
        public int? workorder_id { get; set; }
        public int? workcenter_id { get; set; }
        public string x_description { get; set; }
    }
}
