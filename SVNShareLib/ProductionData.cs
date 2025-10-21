using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib
{
    public class ProductionData
    {
        public string Name { get; set; }
        public string SubName { get; set; }
        public string Quantity { get; set; }
        public string ProductTracking { get; set; }
        public string Serial { get; set; }
        public List<ProductSerial> Products { get; set; }
    }

    public class ProductSerial
    {
        public int Product_id { get; set; }
        public string Has_tracking { get; set; }
        public string Serial_code { get; set; }
    }
}
