using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.Request
{
    public class ItemDataRequest
    {
        public int ID { get; set; }
        public string? ItemCode { get; set; }
        public string? ItemName { get; set; }
    }
}
