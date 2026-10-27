namespace Sigma_Authentication_Portal_API.Models
{
    public class PasswordLoginRequest
    {
        public string AppId { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;
    }
}
