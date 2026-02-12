public class WorkOrderModel
{
    public string WONumber { get; set; }
    public string WODate { get; set; }
    public string WOCloseDate { get; set; }
    public string IssueDate { get; set; }
    public string Type { get; set; }
    public string WOIssueNumber { get; set; }
    public string Memo { get; set; }
    public string ItemR { get; set; }
    public string LotNumber { get; set; }
    public string BinNumber { get; set; }
    public string ExpirationDate { get; set; }
    public string Quantity { get; set; }
    public string Units { get; set; }
    public string Status { get; set; }
    public string ItemCount { get; set; }
    public string AmountForeignCurrency { get; set; }
    public string AmountUSD { get; set; }
    public string AmountRMB { get; set; }
    public string Account { get; set; }
    public string LineId { get; set; }
    public string RMB { get; set; }
    public string Item2 { get; set; }
    public string Account2 { get; set; }
    public string Lot2 { get; set; }
    public string Status2 { get; set; }
    public string Type2 { get; set; }
}

public class WorkOrderIssueModel
{
    public string WONumber { get; set; }
    public string Item { get; set; }
    public string Date { get; set; }
    public string Quantity { get; set; }
    public string Location { get; set; }
    public string WorkOrderType { get; set; }
    public string Status { get; set; }
    public string Name { get; set; }
    public string Memo { get; set; }
}

