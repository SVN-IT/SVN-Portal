using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class SVN_Equipment_StatusUI
    {
        public int id { get; set; }
        public string Name { get; set; }
        public string Operation { get; set; }
        public string StartTime { get; set; } 
        public double Duration { get; set; }
        public double TotalDuration { get; set; }
        public DateTime DateTime { get; set; }
    }
}
