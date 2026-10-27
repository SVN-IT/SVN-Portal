namespace Sigma_Authentication_Portal_API.Models
{
    public sealed class ExchangeRequest
    {
        public string AppId { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;
    }
}
