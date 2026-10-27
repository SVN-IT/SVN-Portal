using System;

namespace SVN_Portal.DAL.DTO
{
    public class SVN_PrintLogTwistUI
    {
        public int id { get; set; }
        public string wo_code { get; set; }
        public int product_id { get; set; }
        public string product_code { get; set; }
        public int print_qty { get; set; }
        public int print_count { get; set; }
        public DateTime print_time { get; set; }
    }
}
