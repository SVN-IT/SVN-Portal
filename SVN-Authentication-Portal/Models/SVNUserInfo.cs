using Microsoft.AspNetCore.Identity;

namespace SVN_Authentication_Portal.Models
{
    public class SVNUserInfo : IdentityUser
    {
        public SVNUserInfo()
        {
            loginInfo = new LoginInfo(); KeepLogined = false;
        }
        LoginInfo loginInfo;
        public LoginInfo LoginInfo { get => loginInfo; set => loginInfo = value; }
        public bool KeepLogined { get; set; }
    }
}
