using Sigma_Authentication_Portal_API.Models;
using System.Security.Claims;

namespace Sigma_Authentication_Portal_API.Services
{
    public sealed class InternalAuthService : IInternalAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        public InternalAuthService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;
        }

        public async Task<InternalLoginResponse> LoginWithMicrosoftAsync(
            ClaimsPrincipal principal,
            CancellationToken cancellationToken = default)
        {
            var tenantId =
    principal.FindFirst("tid")?.Value
    ?? principal.FindFirst(
        "http://schemas.microsoft.com/identity/claims/tenantid")
        ?.Value;

            var objectId =
                principal.FindFirst("oid")?.Value
                ?? principal.FindFirst(
                    "http://schemas.microsoft.com/identity/claims/objectidentifier")
                    ?.Value;

            var email =
                principal.FindFirst("preferred_username")?.Value
                ?? principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;

            var displayName =
                principal.FindFirst("name")?.Value
                ?? principal.Identity?.Name;

            // Không dùng email đơn lẻ làm định danh đáng tin cậy.
            if (string.IsNullOrWhiteSpace(tenantId) ||
                string.IsNullOrWhiteSpace(objectId))
            {
                return new InternalLoginResponse
                {
                    Success = false,
                    ErrorCode = "MICROSOFT_IDENTITY_MISSING",
                    Message = "Không lấy được tenant ID hoặc object ID."
                };
            }

            var request = new InternalMicrosoftLoginRequest
            {
                TenantId = tenantId,
                ObjectId = objectId,
                Email = email,
                DisplayName = displayName
            };

            var path =
                _configuration["InternalAuth:MicrosoftLoginPath"]
                ?? throw new InvalidOperationException(
                    "Thiếu InternalAuth:MicrosoftLoginPath.");

            using var response = await _httpClient.PostAsJsonAsync(
                path, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Không trả nội dung lỗi nội bộ nguyên bản cho người dùng.
                return new InternalLoginResponse
                {
                    Success = false,
                    ErrorCode = "INTERNAL_AUTH_HTTP_ERROR",
                    Message = "API xác thực nội bộ từ chối yêu cầu."
                };
            }

            var result =
                await response.Content.ReadFromJsonAsync<InternalLoginResponse>(
                    cancellationToken: cancellationToken);

            return result ?? new InternalLoginResponse
            {
                Success = false,
                ErrorCode = "INVALID_INTERNAL_AUTH_RESPONSE",
                Message = "API xác thực nội bộ trả về dữ liệu không hợp lệ."
            };
        }

        public async Task<InternalLoginResponse> LoginWithPasswordAsync(
            string username,
            string password,
            CancellationToken cancellationToken = default)
        {
            var path =
                _configuration["InternalAuth:UsernamePasswordPath"]
                ?? throw new InvalidOperationException(
                    "Thiếu InternalAuth:UsernamePasswordPath.");

            // Schema này chỉ là ví dụ. Phải khớp API thực tế.
            var request = new
            {
                username,
                password
            };

            using var response = await _httpClient.PostAsJsonAsync(
                path, request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new InternalLoginResponse
                {
                    Success = false,
                    ErrorCode = "INTERNAL_AUTH_HTTP_ERROR",
                    Message = "API xác thực nội bộ từ chối yêu cầu."
                };
            }

            var result =
                await response.Content.ReadFromJsonAsync<InternalLoginResponse>(
                    cancellationToken: cancellationToken);

            return result ?? new InternalLoginResponse
            {
                Success = false,
                ErrorCode = "INVALID_INTERNAL_AUTH_RESPONSE",
                Message = "API xác thực nội bộ trả về dữ liệu không hợp lệ."
            };
        }
    }
}
