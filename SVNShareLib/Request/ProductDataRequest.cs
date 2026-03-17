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
        public string lotNumber { get; set; }
        public int count { get; set; }

    }

    public class InputProductDataRequest
    {
        public InputProductDataRequest()
        {
            LotScaneds = new List<LotScanedRequest>();
        }
        public string? WorkOrderNumber { get; set; }
        public string? LotNumber { get; set; }
        public int Quality { get; set; }
        public bool IsLastOrder { get; set; }
        public List<LotScanedRequest> LotScaneds { get; set; }
    }

    public class LotScanedRequest
    {
        public int product_id { get; set; }
        public string lotNumber { get; set; }
        public string tracking { get; set; }
    }
}
