namespace SVN_Portal.Services.Configurations
{
    public class QCInfoConfig
    {
        public List<UserInfo> UserInfo { get; set; }
    }

    public class UserInfo
    {
        public string Operation { get; set; }
        public string QCName { get; set; }
        public string PDName { get; set; }
    }
}
