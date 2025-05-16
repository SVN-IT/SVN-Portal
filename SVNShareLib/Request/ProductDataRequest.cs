using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.Request
{
    public class ProductDataRequest
    {
        public int product_id { get; set; }
        public string seriNumber { get; set; }
        public int count { get; set; }
    }
}
