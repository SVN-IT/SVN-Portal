using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml;

namespace SVNShareLib.DTO
{
    class mrp_workcenterUI
    {
        public int id { get; set; }
        public int resource_id { get; set; }
        public int company_id { get; set; }
        public int resource_calendar_id { get; set; }
        public int sequence { get; set; }
        public int color { get; set; }
        public int create_uid { get; set; }
        public int write_uid { get; set; }
        public int name { get; set; }
        public int code { get; set; }
        public int working_state { get; set; }
        public int note { get; set; }
        public int active { get; set; }
        public int create_date { get; set; }
        public int write_date { get; set; }
        public int time_efficiency { get; set; }
        public int default_capacity { get; set; }
        public int costs_hour { get; set; }
        public int time_start { get; set; }
        public int time_stop { get; set; }
        public int oee_target { get; set; }
        public int costs_hour_account_id { get; set; }
        public int manufacturing_overhead_cost_id { get; set; }
        public int wip_analytic_account_id { get; set; }
        public int direct_labor_cost_id { get; set; }
        public int employee_costs_hour { get; set; }

    }
}
