using Microsoft.AspNetCore.Mvc;
using SVNShareLib;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
using System.Data.SqlClient;
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

            var jsonContent = new StringContent(
                JsonSerializer.Serialize(payload),
                Encoding.UTF8,
                "application/json"
            );

            try
            {
                // Nếu NetSuite Suitelet yêu cầu xác thực Token/OAuth, cần gắn thêm Header tương ứng ở đây
                var response = await _httpClient.PostAsync(netSuiteScriptService.UpdateQtyWOURL, jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseString = await response.Content.ReadAsStringAsync();
                    // Parse kết quả trả về từ NetSuite nếu cần
                    processResult.OK = true;
                    processResult.Message = "Cập nhật số lượng sản xuất thành công.";
                    return processResult;
                }

                processResult.OK = false;
                processResult.Message = $"Lỗi từ NetSuite: {response.StatusCode} - {response.ReasonPhrase}";
                return processResult;
            }
            catch (Exception ex)
            {
                // Log lỗi kết nối
                processResult.OK = false;
                processResult.Message = ex.Message;
                return processResult;
            }
        }
    }
}
