using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class SVN_Label_InfoUI
    {
        public string Date { get; set; }
        public string LotID { get; set; }
        public string SerialNumbers { get; set; }
        public DateTime ScanDateTime { get; set; }
        public string Status { get; set; }
        public string Operation { get; set; }
        public string EmployerID { get; set; }
    }
}
