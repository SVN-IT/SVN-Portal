using Microsoft.AspNetCore.Mvc;
using SVNShareLib;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
using System.Text;
using ViidooDBServiceAPI.Services;
using static Org.BouncyCastle.Math.EC.ECCurve;
using static System.Net.WebRequestMethods;

namespace ViidooDBServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ERPServicesController : Controller
    {
        SVNDBConfig SVNDBConfig;
        public ERPServicesController(SVNDBConfig SVNDBConfig)
        {
            this.SVNDBConfig = SVNDBConfig;
        }

        [Route("GetSavedSearch")]
        [HttpGet]
        public async Task<BODataProcessResult> GetSavedSearch(DateTime fromDate, DateTime toDate, string savedSearchId = "customsearch799")
        {
            ERP_synch_dataPortal dataPortal = new ERP_synch_dataPortal(SVNDBConfig.ConnectionString);
            BODataProcessResult processResult = new BODataProcessResult();
            var dateFrom = fromDate.ToString("MM/dd/yyyy");
            var dateTo = toDate.ToString("MM/dd/yyyy");
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

                        var exitingData = dataPortal.GetDataByID(savedSearchId);
                        if(exitingData != null)
                        {
                            exitingData.data = responseString;
                            exitingData.Synch_datetime = fromDate.ToString("yyyyMMdd") + toDate.ToString("dd");
                            dataPortal.Update(exitingData);
                        }
                        else
                        {
                            ERP_synch_dataUI newData = new ERP_synch_dataUI
                            {
                                savedsearchID = savedSearchId,
                                data = responseString,
                                Synch_datetime = fromDate.ToString("yyyyMMdd") + toDate.ToString("dd")
                            };
                            dataPortal.Insert(newData);
                        }

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
