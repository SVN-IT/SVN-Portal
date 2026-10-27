namespace Sigma_Authentication_Portal_API.Models
{
    public class LoginModel : BaseAccountModel
    {
        public string UserName { get; set; } = string.Empty;
        public string UserType { get; set; } = "Email";
        public string Password { get; set; } = string.Empty;
        public bool KeepLogined { get; set; }
    }
}
