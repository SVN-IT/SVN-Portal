namespace Sigma_Authentication_Portal_API.Models
{
    public sealed class InternalMicrosoftLoginRequest
    {
        public string TenantId { get; set; } = string.Empty;

        public string ObjectId { get; set; } = string.Empty;

        public string? Email { get; set; }

        public string? DisplayName { get; set; }
    }

    public sealed class InternalLoginResponse
    {
        public bool Success { get; set; }

        public string? AccessToken { get; set; }

        public int ExpiresIn { get; set; }

        public object? User { get; set; }

        public List<string> Permissions { get; set; } = [];

        public string? ErrorCode { get; set; }

        public string? Message { get; set; }
    }
}
