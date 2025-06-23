using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class SeriFGAndWipUI
    {
        public int id { get; set; }
        public string name { get; set; }
        public string seriTP { get; set; }
        public string seriBTP { get; set; }
        public DateTime? date_finished { get; set; }
        public int? productID { get; set; }
    }
}
