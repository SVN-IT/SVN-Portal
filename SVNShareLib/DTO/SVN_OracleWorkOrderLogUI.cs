using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SVNShareLib.DTO
{
    public class SVN_OracleWorkOrderLogUI
    {
        // Khóa chính NetSuite (id)
        [JsonPropertyName("internalId")]
        public int InternalId { get; set; }

        // Mã Work Order hiển thị (tranid) - ví dụ: WO10042
        [JsonPropertyName("tranId")]
        public string TranId { get; set; }

        // Internal ID của Assembly Item
        [JsonPropertyName("itemId")]
        public string ItemId { get; set; }

        // Số lượng sản xuất
        [JsonPropertyName("quantity")]
        public decimal Quantity { get; set; }

        // Ngày bắt đầu (chuỗi ngày dạng Text từ NetSuite)
        [JsonPropertyName("startDate")]
        public string StartDate { get; set; }

        // Ngày kết thúc
        [JsonPropertyName("endDate")]
        public string EndDate { get; set; }

        // Tên Chi nhánh / Kho (Location)
        [JsonPropertyName("location")]
        public string Location { get; set; }
    }
}
