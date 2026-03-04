using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.Request
{
    public class BOMDataRequest
    {
        public BOMDataRequest()
        {
            Items = new List<BOMItemRequest>();
        }
        public int ProductTempID { get; set; }
        public string? ItemCode { get; set; }
        public int ProductID { get; set; }
        public int ProductUomlID { get; set; }
        public decimal Quantity { get; set; }
        public List<BOMItemRequest> Items { get; set; }
    }

    public class BOMItemRequest
    {
        public int ProductID { get; set; }
        public int ProductUomlID { get; set; }
        public string? ItemCode { get; set; }
        public decimal Quantity { get; set; }
    }
}
