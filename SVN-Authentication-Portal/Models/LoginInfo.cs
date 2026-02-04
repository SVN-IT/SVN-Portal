namespace SVN_Authentication_Portal.Models
{
    public class LoginInfo
    {
        public LoginInfo()
        {
            LoginDate = DateTime.Now;
            JwtData = new JwtData();
        }
        public DateTime LoginDate { get; set; }
        public JwtData JwtData { get; set; }
    }
}
