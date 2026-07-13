
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class workOrderInfoUI
    {
        public int WO_FGID { get; set; }
        public DateTime WO_Date { get; set; }
        public string? WO_Close_Date { get; set; }
        public DateTime ISSUE_Date { get; set; }
        public string Type { get; set; }
        public int WO_ISSUE { get; set; }
        public string? Material_InternalID { get; set; }
        public string? Material_Name { get; set; }
        public string? LOT_InternalID { get; set; }
        public string? Lot_Display { get; set; }
        public string? Quantity_MAT { get; set; }
        public string? Units { get; set; }
        public decimal Amount_Foreign_Currency { get; set; }
        public string? RMB { get; set; }
        public string? Account2 { get; set; }
        public string? Memo { get; set; }
        public string? Subsidiary { get; set; }
        public string FGitem { get; set; }
        public decimal Quantity { get; set; }
        public string? Location { get; set; }
        public string item_type { get; set; }
        public DateTime Last_Updated { get; set; }

        //property để lưu parentWOID
        public int ParentWOID { get; set; }
        public int CurWOID { get; set; }
        public string Status { get; set; }

        //Các trường cần phải tính toán
        public decimal Mat { get; set; }
        public decimal DL { get; set; }
        public decimal OH { get; set; }
        public decimal Total { get; set; }
        public decimal UnitPrice { get; set; }
    }

    public class FGInfo
    {
        public string FGitem { get; set; }
        public int WO_FGID { get; set; }
        public decimal Quantity { get; set; }
    }
}
