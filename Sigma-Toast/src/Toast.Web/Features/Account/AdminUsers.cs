namespace Toast.Web.Features.Account;

public sealed class AdminUser
{
    public string UserName { get; set; } = "";
    /// <summary>Mật khẩu dạng chữ thường (giống TwistAdmin). Để trống = tài khoản không đăng nhập được.</summary>
    public string Password { get; set; } = "";
}

public sealed class AdminUsersOptions
{
    public const string Section = "Toast";
    public List<AdminUser> AdminUsers { get; set; } = [];
}
