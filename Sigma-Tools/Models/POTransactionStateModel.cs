using System;
using System.Collections.Generic;

namespace Sigma_Tools.Models
{
    public class POTransactionLineItem
    {
        public int Line { get; set; }
        public string? ItemCode { get; set; }
        public string? Description { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public string? ExpectedReceiptDate { get; set; }
    }

    public class POTransactionStateModel
    {
        public string TransactionId { get; set; } = Guid.NewGuid().ToString();
        public string CustomerPONumber { get; set; } = "";
        public string Customer { get; set; } = "";
        public string Date { get; set; } = "";
        public string Currency { get; set; } = "USD";
        public string Requisitioner { get; set; } = "";
        
        // Status: "PROCESSING", "PO_CREATED", "SO_COMPLETED", "REJECTED", "FAILED"
        public string Status { get; set; } = "PROCESSING";
        public int CurrentStep { get; set; } = 1; // 1: Review, 2: Price Compare, 3: Create PO, 4: Create SO
        
        public string? PdfFileName { get; set; }
        public string? PdfPath { get; set; }
        public string? RawOcrJson { get; set; }
        public List<POTransactionLineItem>? LineItems { get; set; }
        public string? VerificationResultJson { get; set; }
        
        // NetSuite PO Created Info (from Step 3)
        public int? CreatedPoId { get; set; }
        public string? CreatedPoNumber { get; set; }
        public string? CreatedPoUrl { get; set; }
        
        // NetSuite SO Created Info (from Step 4)
        public int? CreatedSoId { get; set; }
        public string? CreatedSoNumber { get; set; }
        public string? CreatedSoUrl { get; set; }
        
        // Form Configurations
        public int? Subsidiary { get; set; }
        public string? SubsidiaryName { get; set; }
        public int? Location { get; set; }
        public string? LocationName { get; set; }
        public int? TaxCode { get; set; }
        public string? ProjectCode { get; set; }
        public string? ProjectCodeText { get; set; }
        public string? PersonInCharge { get; set; }
        public int? PersonInChargeId { get; set; }
        public string? PoType { get; set; }
        public string? PrType { get; set; }
        public string? SoDate { get; set; }
        public bool AllowCrossSubsidiaryFulfillment { get; set; } = true;
        public string? InventoryLocation { get; set; } = "Sigma Asia";
        
        public string CreatedBy { get; set; } = "System";
        public DateTime CreatedOn { get; set; } = DateTime.Now;
        public DateTime ModifiedOn { get; set; } = DateTime.Now;
    }
}
