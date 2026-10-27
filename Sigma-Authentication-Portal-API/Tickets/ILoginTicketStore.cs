using Sigma_Authentication_Portal_API.Models;

namespace Sigma_Authentication_Portal_API.Tickets
{
    public sealed class LoginTicket
    {
        public string AppId { get; init; } = string.Empty;

        public InternalLoginResponse LoginResult { get; init; } = new();

        public DateTimeOffset ExpiresAt { get; init; }
    }

    public interface ILoginTicketStore
    {
        string Create(string appId, InternalLoginResponse loginResult);

        bool TryRedeem(
            string code,
            string appId,
            out InternalLoginResponse? loginResult);
    }
}
