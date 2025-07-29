using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.Request
{
    public class JsonRpcRequest
    {
        public int Id { get; set; }
        public string Jsonrpc { get; set; }
        public string Method { get; set; }
        public ParamsData Params { get; set; }
    }

    public class ParamsData
    {
        public List<object> Args { get; set; } // Gồm [ [12261], [list field] ]
        public string Model { get; set; }
        public string Method { get; set; }
        public Kwargs Kwargs { get; set; }
    }

    public class Kwargs
    {
        public Context Context { get; set; }
    }

    public class Context
    {
        public string Lang { get; set; }
        public string Tz { get; set; }
        public int Uid { get; set; }
        public List<int> Allowed_Company_Ids { get; set; }
        public bool Bin_Size { get; set; }
        public ParamsContext Params { get; set; }
        public int Default_Company_Id { get; set; }
    }

    public class ParamsContext
    {
        public int Id { get; set; }
        public int Cids { get; set; }
        public int Menu_Id { get; set; }
        public int Action { get; set; }
        public string Model { get; set; }
        public string View_Type { get; set; }
    }
}
