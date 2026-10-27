using Sigma_Authentication_Portal_API.Models;

namespace Sigma_Authentication_Portal_API.Services
{
    public interface IClientAppRegistry
    {
        ClientApp? FindByAppId(string appId);
    }
}
