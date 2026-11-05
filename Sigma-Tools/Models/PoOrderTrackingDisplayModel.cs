namespace Sigma_Tools.Models
{
    public class PoOrderTrackingDisplayModel
    {
        public DateTime CreatedOn { get; set; }
        public string CreatedBy { get; set; }
        public string PartNumber { get; set; }
        public string SigmaPO { get; set; }
        public string Description { get; set; }
        public decimal OrderQty { get; set; }
        public decimal OpenOrderQty { get; set; }
        public string PoUrl { get; set; }
    }
}
