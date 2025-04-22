using AutomationService.Configurations;
using SVNShareLib;

namespace AutomationService.Services
{
    public class APIService
    {
        APIConfiguration _apiConfiguration;
        public APIService(APIConfiguration apiConfiguration)
        {
            _apiConfiguration = apiConfiguration;
        }

        /// <summary>
        /// Call list API
        /// </summary>
        /// <returns></returns>
        public async Task<BODataProcessResult> CallAPI()
        {
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(_apiConfiguration.BaseURL, _apiConfiguration.Timeout);
            BODataProcessResult processResult = new BODataProcessResult();
            foreach (var apiInfo in _apiConfiguration.APIURL)
            {
                var result = await httpClientHelper.PostRequest(apiInfo.URL, null, new CancellationToken(false));
                processResult = result;
            }
            return processResult;
        }
    }
}
