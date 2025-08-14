using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib
{
    public class WorkOrderInfo
    {
        public WorkOrderInfo()
        {
            OrderInfo = new Dictionary<string, string>();
            StockMoveInfo = new List<Dictionary<string, object>>();
        }
        public Dictionary<string, string> OrderInfo { get; set; }
        public List<Dictionary<string, object>> StockMoveInfo { get; set; }
    }
}
