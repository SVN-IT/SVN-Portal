using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib
{
    public class BODataProcessResult
    {
        public bool OK { get; set; }

        public string? Message { get; set; }

        public int NumOfRow { get; set; }

        public object? Content { get; set; }

        public int ErrorNumber { get; set; }
        public int UserID { get; set; }
    }
}
