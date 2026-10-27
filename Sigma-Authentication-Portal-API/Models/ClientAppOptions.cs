namespace Sigma_Authentication_Portal_API.Models
{
    public sealed class ClientAppOptions
    {
        public const string SectionName = "ClientApps";

        public List<ClientApp> Apps { get; set; } = [];
    }

    public sealed class ClientApp
    {
        public string AppId { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string CallbackUrl { get; set; } = string.Empty;
    }
}
