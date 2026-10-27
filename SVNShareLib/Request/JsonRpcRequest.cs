using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.Request
{
    public class JsonRpcRequest
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("jsonrpc")]
        public string Jsonrpc { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("params")]
        public ParamsData Params { get; set; }
    }

    public class ParamsData
    {
        [JsonProperty("args")]
        public List<object> Args { get; set; } // Gồm [ [12261], [list field] ]

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("kwargs")]
        public Kwargs Kwargs { get; set; }
    }

    public class Kwargs
    {
        [JsonProperty("context")]
        public object Context { get; set; } // Để object linh hoạt cho cả Class Context lẫn Dynamic Context trả về từ MarkDone
    }

    public class Context
    {
        [JsonProperty("lang")]
        public string Lang { get; set; }

        [JsonProperty("tz")]
        public string Tz { get; set; }

        [JsonProperty("uid")]
        public int Uid { get; set; }

        [JsonProperty("allowed_company_ids")]
        public List<int> Allowed_Company_Ids { get; set; }

        [JsonProperty("bin_size")]
        public bool Bin_Size { get; set; }

        [JsonProperty("params")]
        public ParamsContext Params { get; set; }

        [JsonProperty("default_company_id")]
        public int Default_Company_Id { get; set; }
    }

    public class ParamsContext
    {
        //public int Id { get; set; }
        [JsonProperty("cids")]
        public int Cids { get; set; }

        [JsonProperty("menu_id")]
        public int Menu_Id { get; set; }

        [JsonProperty("action")]
        public int Action { get; set; }

        [JsonProperty("model")]
        public string Model { get; set; }

        [JsonProperty("view_type")]
        public string View_Type { get; set; }
    }
}
