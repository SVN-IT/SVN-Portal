using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.BaseObject
{
    public class product_category
    {
        public int id { get; set; }
        public int parent_id { get; set; }
        public int create_uid { get; set; }
        public int write_uid { get; set; }
        public string name { get; set; }
        public string complete_name { get; set; }
        public string parent_path { get; set; }
        public DateTime create_date { get; set; }
        public DateTime write_date { get; set; }
        public int message_main_attachment_id { get; set; }
        public string origin_message_id { get; set; }
        public string origin_references { get; set; }
        public int removal_strategy_id { get; set; }
        public string packaging_reserve_method { get; set; }
        public int technician_user_id { get; set; }
        public string equipment_assign_to { get; set; }
    }
}
