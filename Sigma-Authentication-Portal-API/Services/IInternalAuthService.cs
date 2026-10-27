using Sigma_Authentication_Portal_API.Models;
using System.Security.Claims;

namespace Sigma_Authentication_Portal_API.Services
{
    public interface IInternalAuthService
    {
        Task<InternalLoginResponse> LoginWithMicrosoftAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default);

        Task<InternalLoginResponse> LoginWithPasswordAsync(
            string username,
            string password,
            CancellationToken cancellationToken = default);
    }
}
