using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO.NewDashboard
{
    public class SVN_AppSetting_v1UI
    {
        public int id { get; set; }
        public string Group { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public string Application { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModifiedOn { get; set; }
    }
}
