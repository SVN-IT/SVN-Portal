using System.Collections.Generic;

namespace SVN_Portal.Models
{
    public class PDResultDailyViewModel
    {
        public string MasterOperation { get; set; }
        public string OperationActive { get; set; }
        public string MonthlyPlanAchieve { get; set; }
        public string DailyPlanTarget { get; set; }
        public string DailyPlanCurrent { get; set; }
        public string DailyPlanAchieve { get; set; }
        public string UPH { get; set; }
        public string UPPH { get; set; }
        public string Labor { get; set; }
        public string DefectTargetRate { get; set; }
        public string DefectCurrentRate { get; set; }
        public string DefectRate { get; set; }
        public string CheckListOnSystem { get; set; }
        public string Remark { get; set; }
        public string Datetime { get; set; }
        public double DefectTarget { get; set; }
        public double DefectCurrent { get; set; }
        public double QuantityResultCurrent { get; set; }
        public string UPHCurrent { get; set; }
        public string UPHTarget { get; set; }
        public string UPPHCurrent { get; set; }
        public string UPPHTarget { get; set; }
        public string LaborCurrent { get; set; }
        public string LaborTarget { get; set; }
    }
}
