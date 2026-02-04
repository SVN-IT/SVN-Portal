namespace SVN_Portal.Services.Authentication.AuthenData
{
    public class BaseAccountModel
    {
        public int AppID { get; set; }
        public int CompanyID { get; set; }
    }

    public class LoginModel : BaseAccountModel
    {
        public string UserName { get; set; } = string.Empty;
        public string UserType { get; set; } = "Email";
        public string Password { get; set; } = string.Empty;
        public bool KeepLogined { get; set; }
    }
}
