namespace Sigma_Tools.Models
{
    public class POLine
    {
        public int Line { get; set; }
        public string ItemCode { get; set; }
        public string Description { get; set; }
        public int Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
    }

    public class AuditReportViewModel
    {
        public bool LineIntegrityPassed { get; set; }
        public bool UnitsMatched { get; set; }
        public bool AmountMatched { get; set; }
        public int CalculatedUnits { get; set; }
        public int ExpectedUnits { get; set; }
        public decimal CalculatedAmount { get; set; }
        public decimal ExpectedAmount { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class InboundPOReviewViewModel
    {
        public string PONumber { get; set; }
        public string Date { get; set; }
        public string Customer { get; set; }
        public string Requisitioner { get; set; }
        public List<POLine> LineItems { get; set; } = new List<POLine>();
        public int CalculatedUnits { get; set; }
        public decimal CalculatedAmount { get; set; }
        public int ExpectedUnits { get; set; }
        public decimal ExpectedAmount { get; set; }

        // Thêm thuộc tính lưu Audit Report
        public AuditReportViewModel AuditReport { get; set; } = new AuditReportViewModel();
    }
}
