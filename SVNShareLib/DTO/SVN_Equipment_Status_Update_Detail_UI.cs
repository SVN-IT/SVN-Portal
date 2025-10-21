using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class SVN_Equipment_Status_Update_Detail_UI
    {
        public int Id { get; set; }
        public string Operation { get; set; }
        public string State { get; set; }
        public string EstimateTime { get; set; }
        public DateTime FromTime { get; set; }
        public string ToTime { get; set; }
        public float DurationMinutes { get; set; }
    }
}
