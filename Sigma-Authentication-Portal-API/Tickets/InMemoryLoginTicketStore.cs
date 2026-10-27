using Sigma_Authentication_Portal_API.Models;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Sigma_Authentication_Portal_API.Tickets
{
    public sealed class InMemoryLoginTicketStore : ILoginTicketStore
    {
        private static readonly TimeSpan TicketLifetime =
            TimeSpan.FromMinutes(1);

        private readonly ConcurrentDictionary<string, LoginTicket> _tickets =
            new();

        public string Create(
            string appId,
            InternalLoginResponse loginResult)
        {
            RemoveExpiredTickets();

            // Tạo code ngẫu nhiên mạnh, không chứa thông tin tài khoản.
            var code = Convert.ToBase64String(
                    RandomNumberGenerator.GetBytes(32))
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');

            var ticket = new LoginTicket
            {
                AppId = appId,
                LoginResult = loginResult,
                ExpiresAt = DateTimeOffset.UtcNow.Add(TicketLifetime)
            };

            if (!_tickets.TryAdd(code, ticket))
            {
                throw new InvalidOperationException(
                    "Không thể tạo login ticket.");
            }

            return code;
        }

        //public bool TryRedeem(
        //    string code,
        //    string appId,
        //    out InternalLoginResponse? loginResult)
        //{
        //    loginResult = null;

        //    // TryRemove làm cho mỗi code chỉ được lấy ra một lần
        //    // trong bộ nhớ của instance này.
        //    if (!_tickets.TryRemove(code, out var ticket))
        //        return false;

        //    if (ticket.ExpiresAt <= DateTimeOffset.UtcNow)
        //        return false;

        //    if (!string.Equals(
        //            ticket.AppId, appId, StringComparison.Ordinal))
        //        return false;

        //    loginResult = ticket.LoginResult;
        //    return true;
        //}

        private void RemoveExpiredTickets()
        {
            var now = DateTimeOffset.UtcNow;

            foreach (var entry in _tickets)
            {
                if (entry.Value.ExpiresAt <= now)
                {
                    _tickets.TryRemove(entry.Key, out _);
                }
            }
        }

        private readonly object _ticketLock = new();

        public bool TryRedeem(
            string code,
            string appId,
            out InternalLoginResponse? loginResult)
        {
            loginResult = null;

            lock (_ticketLock)
            {
                if (!_tickets.TryGetValue(code, out var ticket))
                    return false;

                if (ticket.ExpiresAt <= DateTimeOffset.UtcNow)
                {
                    _tickets.TryRemove(code, out _);
                    return false;
                }

                if (!string.Equals(
                        ticket.AppId, appId, StringComparison.Ordinal))
                {
                    // Không xóa ticket nếu AppId không khớp.
                    return false;
                }

                if (!_tickets.TryRemove(code, out _))
                    return false;

                loginResult = ticket.LoginResult;
                return true;
            }
        }
    }
}
