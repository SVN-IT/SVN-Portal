using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.BaseObject
{
    public class viin_quantity_alert_team
    {
        public int id { get; set; }
        public object? message_main_attachment_id { get; set; }
        public object? alias_id { get; set; }
        public object? company_id { get; set; }
        public object? sequence { get; set; }
        public object? color { get; set; }
        public object? create_uid { get; set; }
        public object? write_uid { get; set; }
        public string origin_message_id { get; set; }
        public string origin_references { get; set; }
        public string name { get; set; }
        public object? create_date { get; set; }
        public object? write_date { get; set; }
    }
}
