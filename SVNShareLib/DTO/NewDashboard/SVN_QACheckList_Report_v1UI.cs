using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO.NewDashboard
{
    public class SVN_QACheckList_Report_v1UI
    {
        public string Number { get; set; }
        public string Name { get; set; }
        public string ConfirmStatus { get; set; }
        public double PointBSC { get; set; }
        public string RestaurantStaffs { get; set; }
        public DateTime CheckDate { get; set; }
    }
}
