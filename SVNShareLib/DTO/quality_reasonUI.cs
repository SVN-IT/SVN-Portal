using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class quality_reasonUI
    {
        public int id { get; set; }
        public int? create_uid { get; set; }
        public DateTime? create_date { get; set; }
        public int? write_uid { get; set; }
        public DateTime? write_date { get; set; }
        public string name { get; set; }
    }
}
