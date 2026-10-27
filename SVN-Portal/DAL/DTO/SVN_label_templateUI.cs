namespace SVN_Portal.DAL.DTO
{
    public class SVN_label_templateUI
    {
        public int id { get; set; }
        public string partNumber { get; set; }
        public string zplData { get; set; }
        public string size { get; set; }
        public string type { get; set; }
        public string operation { get; set; }
        public DateTime createdAt { get; set; }
        public DateTime? updatedAt { get; set; }
    }
}
