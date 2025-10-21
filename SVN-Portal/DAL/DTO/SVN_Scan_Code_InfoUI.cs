namespace SVN_Portal.DAL.DTO
{
    public class SVN_Scan_Code_InfoUI
    {
        public int ID { get; set; }
        public string Operation { get; set; }
        public string Code { get; set; }
        public string QuantityList { get; set; }
        public decimal SelectedQuantity { get; set; }
    }
}
