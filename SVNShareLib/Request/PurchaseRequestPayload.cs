using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.Request
{
    public class PrItemDto
    {
        public string? Custcol_pr_item { get; set; }
        public string? Custcol_pr_item_purpose { get; set; }
        public string? Unit { get; set; }
        public double Quantity { get; set; }
        public double Estimate_rate { get; set; }
        public string? Delivery_date { get; set; }
    }

    public class PurchaseRequestPayload
    {
        public string? Custbody_pr_department { get; set; }
        public List<PrItemDto>? Items { get; set; }
    }

    public class ExcelResponse
    {
        public bool Success { get; set; }
        public string FileBase64 { get; set; } = string.Empty;
        public string? Error { get; set; }
    }
}
