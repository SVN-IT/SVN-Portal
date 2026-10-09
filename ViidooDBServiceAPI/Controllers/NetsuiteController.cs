using Microsoft.AspNetCore.Mvc;
using SVNShareLib;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ViidooDBServiceAPI.Services;

namespace ViidooDBServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class NetsuiteController : Controller
    {
        SVNDBConfig svnDBConfig;
        NetSuiteScriptService netSuiteScriptService;
        private readonly HttpClient _httpClient;
        public NetsuiteController(SVNDBConfig svnDBConfig, NetSuiteScriptService netSuiteScriptService, HttpClient httpClient)
        {
            this.svnDBConfig = svnDBConfig;
            this.netSuiteScriptService = netSuiteScriptService;
            _httpClient = httpClient;
        }

        [HttpPost("ReceiveWorkOrderBatch")]
        public async Task<IActionResult> ReceiveWorkOrderBatch([FromBody] List<SVN_OracleWorkOrderLogUI> dtoList)
        {
            var dataPortal = new SVN_OracleWorkOrderLogDataPortal(svnDBConfig.ConnectionString);
            var result = await dataPortal.SynchWOLogs(dtoList);
            string statusMessage = result > 0 ? "Success" : "Failed";
            return Ok(new { status = statusMessage, count = result });
        }

        [HttpPost("UpdateWorkOrderProductionResultAsync")]
        public async Task<BODataProcessResult> UpdateWorkOrderProductionResultAsync(NetSuiteWOUpdateRequest dataRequest)
        {
            BODataProcessResult processResult = new BODataProcessResult();

            var payload = new
            {
                internalId = dataRequest.internalId,
                quantityBuilt = dataRequest.quantityBuilt
            };

            var jsonString = System.Text.Json.JsonSerializer.Serialize(payload);
            var jsonContent = new StringContent(jsonString, Encoding.UTF8, "application/json");

            try
            {
                // 1. URL của Suitelet (dùng URL chuẩn hoặc External URL)
                string requestUrl = netSuiteScriptService.UpdateQtyWOURL;

                // 2. Cấu hình thông tin Token (Lấy từ NetSuite Integration & Access Token)
                string accountId = netSuiteScriptService.accountId;
                string consumerKey = netSuiteScriptService.ConsumerKey;
                string consumerSecret = netSuiteScriptService.ConsumerSecret;
                string tokenId = netSuiteScriptService.TokenId;
                string tokenSecret = netSuiteScriptService.TokenSecret;

                // 3. Tạo chuỗi Header Authorization OAuth 1.0
                string authHeader = GenerateNetsuiteOAuthHeader(requestUrl, "POST", accountId, consumerKey, consumerSecret, tokenId, tokenSecret);

                // Gắn Header vào HttpRequestMessage
                var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Content = jsonContent
                };
                request.Headers.Add("Authorization", authHeader);

                // 4. Thực thi Request
                var response = await _httpClient.SendAsync(request);

                var responseString = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    processResult.OK = true;
                    processResult.Message = "Cập nhật số lượng sản xuất thành công: " + responseString;
                    return processResult;
                }

                processResult.OK = false;
                processResult.Message = $"Lỗi từ NetSuite ({response.StatusCode}): {responseString}";
                return processResult;
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
                return processResult;
            }
        }

        private string GenerateNetsuiteOAuthHeader(string fullUrl, string httpMethod, string accountId, string consumerKey, string consumerSecret, string tokenId, string tokenSecret)
        {
            string nonce = Guid.NewGuid().ToString("N");
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            string signatureMethod = "HMAC-SHA256";
            string version = "1.0";

            Uri uri = new Uri(fullUrl);
            string baseUrl = uri.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.Unescaped);

            // 1. Tập hợp tham số để tính Signature
            var sortedParams = new SortedDictionary<string, string>
    {
        { "oauth_consumer_key", consumerKey },
        { "oauth_nonce", nonce },
        { "oauth_signature_method", signatureMethod },
        { "oauth_timestamp", timestamp },
        { "oauth_token", tokenId },
        { "oauth_version", version }
    };

            // Parse các query string từ URL (script=2496, deploy=1)
            var queryParams = System.Web.HttpUtility.ParseQueryString(uri.Query);
            foreach (string key in queryParams.AllKeys)
            {
                if (!string.IsNullOrEmpty(key))
                {
                    sortedParams.Add(Uri.EscapeDataString(key), Uri.EscapeDataString(queryParams[key]));
                }
            }

            StringBuilder paramString = new StringBuilder();
            foreach (var p in sortedParams)
            {
                if (paramString.Length > 0) paramString.Append("&");
                paramString.Append($"{p.Key}={p.Value}");
            }

            // 2. Tạo Base String & Signing Key
            string baseString = $"{httpMethod.ToUpper()}&{Uri.EscapeDataString(baseUrl)}&{Uri.EscapeDataString(paramString.ToString())}";
            string signingKey = $"{Uri.EscapeDataString(consumerSecret)}&{Uri.EscapeDataString(tokenSecret)}";

            string rawSignature;
            using (HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingKey)))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString));
                rawSignature = Convert.ToBase64String(hash); // Chữ ký Base64 nguyên bản (chứa +, /, =)
            }

            // 3. Ghép chuỗi Header (Chỉ EscapeDataString cho Signature khi đưa vào Header)
            string escapedSignature = Uri.EscapeDataString(rawSignature);

            return $"OAuth realm=\"{accountId.Replace("-", "_").ToUpper()}\", " +
                   $"oauth_consumer_key=\"{consumerKey}\", " +
                   $"oauth_token=\"{tokenId}\", " +
                   $"oauth_signature_method=\"{signatureMethod}\", " +
                   $"oauth_timestamp=\"{timestamp}\", " +
                   $"oauth_nonce=\"{nonce}\", " +
                   $"oauth_version=\"{version}\", " +
                   $"oauth_signature=\"{escapedSignature}\"";
        }
    }
}
