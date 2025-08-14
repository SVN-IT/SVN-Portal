namespace SVN_Portal.Services.Configurations
{
    public class DBConfiguration
    {
        public string ProdConnectionString { get; set; }
        public string DevConnectionString { get; set; }
        public string CheckListConnectionString { get; set; }
        public string ProductMode { get; set; }
        public string GetConnectionString()
        {
            string connectionString = DevConnectionString;
            if (ProductMode == "Prod")
            {
                connectionString = ProdConnectionString;
            }
            return connectionString;
        }
    }
}
