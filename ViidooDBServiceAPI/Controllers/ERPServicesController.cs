using Microsoft.AspNetCore.Mvc;
using SVNShareLib;
using System.Text;
using static Org.BouncyCastle.Math.EC.ECCurve;
using static System.Net.WebRequestMethods;

namespace ViidooDBServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ERPServicesController : Controller
    {
        [Route("GetSavedSearch799")]
        [HttpGet]
        public async Task<BODataProcessResult> GetSavedSearch799(DateTime fromDate, DateTime toDate)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            var dateFrom = fromDate.ToString("MM/dd/yyyy");
            var dateTo = toDate.ToString("MM/dd/yyyy");
            var savedSearchId = "customsearch799";
            var request = new
            {
                savedSearchId = savedSearchId,
                skip = 0,
                limit = 100000,
                debug = false,
                filters = new[]
                {
                    new {
                        field = "trandate",
                        join = "createdFrom",
                        @operator = "WITHIN",
                        value = $"{dateFrom},{dateTo}"
                    }
                }
            };
            try
            {
                using(var client = new HttpClient())
                {
                    var content = new StringContent(
                        Newtonsoft.Json.JsonConvert.SerializeObject(request),
                        Encoding.UTF8,
                        "application/json"
                    );
                    var response = await client.PostAsync("https://api.sigmaworldwide.io/v1/api/saved-search/execute", content);
                    if (response.IsSuccessStatusCode)
                    {
                        var responseString = await response.Content.ReadAsStringAsync();

                        processResult.OK = true;
                        processResult.Message = "API call successful";
                    }
                    else 
                    {
                        processResult.OK = false;
                        processResult.Message = $"API call failed with status code: {response.StatusCode}";
                    } 
                }
            }
            catch(Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return processResult;
        }
    }
}
