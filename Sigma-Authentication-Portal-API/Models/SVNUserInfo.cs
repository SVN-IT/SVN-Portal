using Microsoft.AspNetCore.Identity;

namespace Sigma_Authentication_Portal_API.Models
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
