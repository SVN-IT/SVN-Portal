using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SVNShareLib.Response
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

    public class UserInfo
    {
        public UserInfo()
        {
            ObjectRights = new Dictionary<string, List<string>>();
            Roles = new List<string>();
            StoreIDs = new List<int>();
        }
        public string ID { get; set; }
        public string UserName { get; set; }
        public string FullName { get; set; }
        public string EmailAddress { get; set; }
        public string Department { get; set; }
        public List<string> Roles { get; set; }

        //objectName;right1,right2
        public Dictionary<string, List<string>> ObjectRights { get; set; }
        public int MyProperty { get; set; }

        public int AppID { get; set; }
        public int CompanyID { get; set; }
        public int ManagerID { get; set; }
        public string ManagerEmail { get; set; }
        public List<int> StoreIDs { get; set; }
        public Guid LoginID { get; set; }
        public DateTime LoginDate { get; set; }
    }
}
