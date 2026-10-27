using Sigma_Authentication_Portal_API.Models;

namespace Sigma_Authentication_Portal_API.Services
{
    public sealed class ClientAppRegistry : IClientAppRegistry
    {
        private readonly Dictionary<string, ClientApp> _apps;

        public ClientAppRegistry(IConfiguration configuration)
        {
            var apps = configuration
                .GetSection("ClientApps")
                .Get<List<ClientApp>>() ?? [];

            _apps = new Dictionary<string, ClientApp>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var app in apps)
            {
                if (string.IsNullOrWhiteSpace(app.AppId) ||
                    string.IsNullOrWhiteSpace(app.CallbackUrl))
                {
                    throw new InvalidOperationException(
                        "Client App phải có AppId và CallbackUrl.");
                }

                if (!Uri.TryCreate(
                        app.CallbackUrl,
                        UriKind.Absolute,
                        out var callbackUri))
                {
                    throw new InvalidOperationException(
                        $"CallbackUrl không hợp lệ cho AppId {app.AppId}.");
                }

                // Chỉ cho phép HTTPS trong môi trường triển khai.
                // HTTP localhost được chấp nhận để phát triển.
                var isLocalHttp =
                    callbackUri.IsLoopback &&
                    callbackUri.Scheme == Uri.UriSchemeHttp;

                if (callbackUri.Scheme != Uri.UriSchemeHttps &&
                    !isLocalHttp)
                {
                    throw new InvalidOperationException(
                        $"CallbackUrl của {app.AppId} phải dùng HTTPS.");
                }

                if (!_apps.TryAdd(app.AppId, app))
                {
                    throw new InvalidOperationException(
                        $"AppId bị trùng: {app.AppId}");
                }
            }
        }

        public ClientApp? FindByAppId(string appId)
        {
            if (string.IsNullOrWhiteSpace(appId))
                return null;

            return _apps.TryGetValue(appId, out var app)
                ? app
                : null;
        }
    }
}
