namespace SVN_Portal.DAL.DTO
{
    public class SVN_AppSettingUI
    {
        public int id { get; set; }
        public string Group { get; set; }
        public string Key { get; set; }
        public string Value { get; set; }
        public string Application { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime ModifiedOn { get; set; }
    }
}
