using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.BaseObject
{
    public class quality_reason
    {
        public int id { get; set; }
        public object? create_uid { get; set; }
        public object? create_date { get; set; }
        public object? write_uid { get; set; }
        public object? write_date { get; set; }
        public string name { get; set; }

    }
}
