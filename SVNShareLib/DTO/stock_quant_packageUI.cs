using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class stock_quant_packageUI
    {
        public int id { get; set; }
        public int package_type_id { get; set; }
        public int location_id { get; set; }
        public int company_id { get; set; }
        public int create_uid { get; set; }
        public int write_uid { get; set; }
        public string name { get; set; }
        public string package_use { get; set; }
        public DateTime pack_date { get; set; }
        public DateTime create_date { get; set; }
        public DateTime write_date { get; set; }
    }
}
