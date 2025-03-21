using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib
{
    public class ViindooDBConfig
    {
        public string ServerUrl { get; set; }
        public string OdooServerUrl { get; set; }
        public string DbName { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public List<QueryConfig> QueryConfig { get; set; }
    }

    public class QueryConfig
    {
        public string TableName { get; set; }
        public string Domain { get; set; }
        public string Fields { get; set; }
        public int Limit { get; set; }
        public string Order { get; set; }
    }
}
