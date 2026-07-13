using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class SVN_SavedSearch_FormulaUI
    {
        public string? SPSearchID { get; set; }
        public string? Field { get; set; }
        public string? Formula { get; set; }
        public string? Regex { get; set; }
    }
}
