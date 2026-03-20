using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class TrainingOperatorRecordUI
    {
        public string Traning_date { get; set; }
        public int Traing_hours { get; set; }
        public string Training_doc_code { get; set; }
        public string Operation { get; set; }
        public string Supervisor_code { get; set; }
        public string Operator_code { get; set; }
        public string Operator_name { get; set; }
        public string Status { get; set; }
        public string JsonData { get; set; }
    }
}
