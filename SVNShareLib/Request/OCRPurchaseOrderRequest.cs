using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.Request
{
    public class OCRPurchaseOrderRequest
    {
        public string? po_type { get; set; }
        public string? document_type { get; set; }
        public string? source_file { get; set; }
        public int total_pages_scanned { get; set; }
        public POHeader header { get; set; }
        public List<POLineItem> line_items { get; set; }
        public POTotals totals { get; set; }
    }

    public class POHeader
    {
        public string? po_type { get; set; }
        public string? po_number { get; set; }
        public string? po_date { get; set; }
        public BuyerInfo buyer { get; set; }
        public VendorInfo vendor { get; set; }
        public string? vendor_code { get; set; }
        public ContactInfo buyer_contact { get; set; }
        public ContactInfo vendor_contact { get; set; }
        public ShipToInfo ship_to { get; set; }
        public string? approval_status { get; set; }
        public string? payment_terms { get; set; }
        public string? trade_terms { get; set; }
        public string? currency { get; set; }
    }

    public class BuyerInfo { public string? name { get; set; } public string? address { get; set; } }
    public class VendorInfo { public string? name { get; set; } public string? address { get; set; } }
    public class ContactInfo { public string? name { get; set; } public string? tel { get; set; } public string? email { get; set; } }
    public class ShipToInfo { public string? name { get; set; } public string? address { get; set; } }

    public class POLineItem
    {
        public int line_no { get; set; }
        public string? item_no { get; set; }
        public string? item { get; set; }
        public string? category { get; set; }
        public string? description { get; set; }
        public string? inv_org { get; set; }
        public decimal quantity { get; set; }
        public string? uom { get; set; }
        public decimal price_each { get; set; }
        public decimal amount { get; set; }
        public string? eta_date { get; set; }
    }

    public class POTotals
    {
        public decimal total_units { get; set; }
        public decimal subtotal { get; set; }
        public decimal shipping_and_handling { get; set; }
        public decimal grand_total { get; set; }
    }

    public class SubsidiaryRequest
    {
        public string customerId { get; set; } // Nếu cần truyền thêm tham số lọc, có thể mở rộng sau
    }

    public class LocationRequest
    {
        public int subsidiaryId { get; set; } // Nếu cần truyền thêm tham số lọc, có thể mở rộng sau
    }

    public class SubsidiaryDto
    {
        public long id { get; set; }
        public string name { get; set; }
        public string namenohierarchy { get; set; }
        public string legalname { get; set; }
        public string isinactive { get; set; }
        public long? parent { get; set; }
    }

    public class SubsidiaryApiResponse
    {
        public bool success { get; set; }
        public int total { get; set; }
        public List<SubsidiaryDto> subsidiaries { get; set; }
        public string message { get; set; }
    }

    public class LocationApiResponse
    {
        public bool success { get; set; }
        public string subsidiaryId { get; set; }
        public int total { get; set; }
        public List<LocationItem> locations { get; set; }
        public string message { get; set; }
    }

    public class LocationItem
    {
        public string id { get; set; }          // ID của kho (Ví dụ: "105")
        public string name { get; set; }        // Tên kho (Ví dụ: "Sigma Worldwide")
        public bool isinactive { get; set; }    // Trạng thái vô hiệu hóa (false = đang hoạt động)
    }

    public class TaxCodeApiResponse
    {
        public bool success { get; set; }
        public int total { get; set; }
        public List<TaxCodeItem> taxCodes { get; set; }
        public string message { get; set; }
    }

    public class TaxCodeItem
    {
        public string id { get; set; }          // Internal ID (VD: "4050")
        public string itemid { get; set; }      // Tên Tax Code (VD: "VAT_CN:VAT 11%")
        public string rate { get; set; }      // Tỷ lệ thuế (VD: 11.00)
        public string country { get; set; }     // Quốc gia/Khu vực (VD: "China")
        public bool isinactive { get; set; }    // Trạng thái khóa hay không
    }

    public class ProjectCodeApiResponse
    {
        public bool success { get; set; }
        public int total { get; set; }
        public List<ProjectCodeItem> projectCodes { get; set; }
        public string message { get; set; }
    }

    public class ProjectCodeItem
    {
        public string id { get; set; }          // Internal ID của Project Code
        public string name { get; set; }        // Tên hiển thị của Project Code
        public bool isinactive { get; set; }    // Trạng thái vô hiệu hóa
    }
}
