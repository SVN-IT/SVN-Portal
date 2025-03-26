using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PrinterServices.Objects
{
    public class PrinterConfigData
    {
        public string ID_Printer { get; set; }
        public string Name_Printer { get; set; }
        public string IP_Printer { get; set; }
        public string MAC_Printer { get; set; }
        public string Port_Printer { get; set; }
        public string Size { get; set; }
        public string Type { get; set; }
        public string ZPL_Temp { get; set; }
        public string DPL_Temp { get; set; }
    }
}
