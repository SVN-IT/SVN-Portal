using AutomationService.Configurations;
using SVNShareLib;

namespace AutomationService.Services
{
    public class APIService
    {
        APIConfiguration _apiConfiguration;
        private readonly ILogger<APIService> _logger;
        public APIService(APIConfiguration apiConfiguration, ILogger<APIService> logger)
        {
            _apiConfiguration = apiConfiguration;
            _logger = logger;
        }

        /// <summary>
        /// Call list API
        /// </summary>
        /// <returns></returns>
        public async Task<BODataProcessResult> CallAPI()
        {
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(_apiConfiguration.BaseURL, _apiConfiguration.Timeout);
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                foreach (var apiInfo in _apiConfiguration.APIURL)
                {
                    var result = await httpClientHelper.PostRequest(apiInfo.URL, null, new CancellationToken(false));
                    processResult = result;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Time]: {DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss")} [Status]: {false} [Message]: {ex.Message}");
            }
            finally
            {
                _logger.LogInformation($"[Time]: {DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss")} [Status]: {processResult.OK} [Message]: {processResult.Message}");
            }
            return processResult;
        }
    }
}
