namespace Sigma_Tools.Models
{
    public class POLineItem
    {
        public int Line { get; set; }
        public string ItemCode { get; set; }
        public string Description { get; set; }
        public int Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount => Qty * UnitPrice;
    }
    public class InboundPOReviewViewModel
    {
        public string PONumber { get; set; }
        public string Date { get; set; }
        public string Customer { get; set; }
        public string Requisitioner { get; set; }
        public List<POLineItem> LineItems { get; set; } = new List<POLineItem>();
        public int CalculatedUnits { get; set; }
        public decimal CalculatedAmount { get; set; }
        public int ExpectedUnits { get; set; }
        public decimal ExpectedAmount { get; set; }
    }
}
