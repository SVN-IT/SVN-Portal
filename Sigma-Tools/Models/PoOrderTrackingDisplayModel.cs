namespace Sigma_Tools.Models
{
    public class PoOrderTrackingDisplayModel
    {
        public DateTime CreatedOn { get; set; }
        public string CreatedBy { get; set; } = "System";
        public string PartNumber { get; set; } = "";
        public string SigmaPO { get; set; } = "";
        public string Description { get; set; } = "";
        public decimal OrderQty { get; set; }
        public decimal OpenOrderQty { get; set; }
        public string? PoUrl { get; set; }

        // Bổ sung các trường quản lý tiến trình Transaction
        public string? TransactionId { get; set; }
        public string? Customer { get; set; }
        public string? Status { get; set; } // "PROCESSING", "PO_CREATED", "SO_COMPLETED", "REJECTED", "FAILED"
        public string? StatusDisplay { get; set; }
        public int CurrentStep { get; set; } = 4;
        public bool CanResume { get; set; } = false;
        public string? SoNumber { get; set; }
        public string? SoUrl { get; set; }
    }
}
