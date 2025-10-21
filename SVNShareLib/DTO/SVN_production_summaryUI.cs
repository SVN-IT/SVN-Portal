using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class SVN_production_summaryUI
    {
        public string Operation { get; set; }
        public decimal Target { get; set; }
        public decimal Production { get; set; }
        public decimal Rate { get; set; }
        public string Month { get; set; }
        public string Year { get; set; }
    }
}
