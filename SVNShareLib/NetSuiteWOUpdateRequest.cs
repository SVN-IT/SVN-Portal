using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib
{
    public class NetSuiteWOUpdateRequest
    {
        public int internalId { get; set; }
        public decimal quantityBuilt { get; set; }
    }

    public class CustomerPricingRequest
    {
        public string customerId { get; set; }
        public string currency { get; set; }
    }
}
