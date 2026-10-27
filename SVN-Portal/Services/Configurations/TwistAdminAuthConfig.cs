namespace SVN_Portal.Services.Configurations
{
    // Tài khoản đăng nhập khu quản trị Twist (/twist/admin), đọc từ Configs/twist-admin-accounts.json
    public class TwistAdminAuthConfig
    {
        public const string Scheme = "TwistAdminCookie";

        public List<TwistAdminAccount> Accounts { get; set; } = new();
    }

    public class TwistAdminAccount
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
    }
}
