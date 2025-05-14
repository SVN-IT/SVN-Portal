using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.BaseObject
{
    public class stock_quant_package
    {
        public object package_type_id { get; set; }
        public object write_uid { get; set; }
        public DateTime __last_update { get; set; }
        public DateTime create_date { get; set; }
        public object valid_sscc { get; set; }
        public object owner_id { get; set; }
        public object create_uid { get; set; }
        public string package_use { get; set; }
        public int[] line_ids { get; set; }
        public string name { get; set; }
        public object quant_ids { get; set; }
        public object location_id { get; set; }
        public object company_id { get; set; }
        public string display_name { get; set; }
        public DateTime pack_date { get; set; }
        public int id { get; set; }
        public DateTime write_date { get; set; }
    }
}
