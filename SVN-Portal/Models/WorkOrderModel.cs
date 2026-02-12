using DocumentFormat.OpenXml.Wordprocessing;
using static System.Runtime.InteropServices.JavaScript.JSType;

public class WorkOrderModel
{
    public int WO_FGID { get; set; }
    public DateTime WO_Date { get; set; }
    public int WO_Close_Date { get; set; }
    public DateTime ISSUE_Date { get; set; }
    public string Type { get; set; }
    public int WO_ISSUE { get; set; }
    public int Material_InternalID { get; set; }
    public string Material_Name { get; set; }
    public int LOT_InternalID { get; set; }
    public string Lot_Display { get; set; }
    public decimal Quantity_MAT { get; set; }
    public string Units { get; set; }
    public decimal Amount_Foreign_Currency { get; set; }
    public decimal RMB { get; set; }
    public string Account2 { get; set; }
    public string Memo { get; set; }
    public string Subsidiary { get; set; }
    public string FGitem { get; set; }
    public decimal Quantity { get; set; }
    public string Location { get; set; }
    //Các trường cần phải tính toán
    public decimal Mat { get; set; }
    public decimal DL { get; set; }
    public decimal OH { get; set; }
    public decimal Total { get; set; }
    public decimal UnitPrice { get; set; }

}


