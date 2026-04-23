using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO.NewDashboard
{
    public class SVN_target_v1UI
    {
        public string Operation { get; set; }
        public double Daily_plan { get; set; }
        public double UPH { get; set; }
        public double UPPH { get; set; }
        public double Labor { get; set; }
        public string Date_time { get; set; }
        public double Total_Qty { get; set; }
        public double MaxLabor { get; set; }
        public double Current_UPH { get; set; }
        public double Current_UPPH { get; set; }
        public double Defect { get; set; }
        public double Total_NG_Qty { get; set; }
        public string WC { get; set; }
        public double Workingtime { get; set; }
        public string Shift { get; set; }
    }
}
