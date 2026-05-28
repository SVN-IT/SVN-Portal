using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib
{
    public class OperInfoConfigV1
    {
        public List<OperInfoV1> OperInfo { get; set; }
    }

    public class OperInfoV1
    {
        public string Operation { get; set; }
        public string Name { get; set; }
        public double Price { get; set; }
        public string MasterOperation { get; set; }
        public string WCName { get; set; }
        public string WCType { get; set; }
        public List<int> Produce_id { get; set; }
        public List<WCV1> WC { get; set; }
        public int ColWidth { get; set; }
        public int StoreID { get; set; }
        public int Top_row { get; set; }
    }

    public class WCV1
    {
        public string WCName { get; set; }
        public List<int> Produce_id { get; set; }
        public int Top_row { get; set; }
    }

    public class ProductionResultCompare
    {
        public string Operation { get; set; }
        public string ProductID { get; set; }
        public string Shift { get; set; }
        public double SVNQty { get; set; }
        public decimal ViindooQty { get; set; }
    }
}
