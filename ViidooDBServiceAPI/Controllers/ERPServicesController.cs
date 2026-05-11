using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
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

        [Route("GetSavedSearchAllPage")]
        [HttpGet]
        public async Task<BODataProcessResult> GetSavedSearchAllPage(DateTime fromDate, DateTime toDate, string savedSearchId = "customsearch799")
        {
            ERP_synch_dataPortal dataPortal = new ERP_synch_dataPortal(SVNDBConfig.ConnectionString);
            BODataProcessResult processResult = new BODataProcessResult();

            var dateFrom = fromDate.ToString("MM/dd/yyyy");
            var dateTo = toDate.ToString("MM/dd/yyyy");

            int skip = 0;
            int page = 1;
            int limitPerRequest = 1000;
            bool keepRunning = true;
            
            try
            {
                using (var client = new HttpClient())
                {
                    while (keepRunning)
                    {
                        var request = new
                        {
                            savedSearchId = savedSearchId,
                            skip = skip,
                            limit = limitPerRequest,
                            debug = false,
                            filters = new[] {
                                new { field = "trandate", join = "createdFrom", @operator = "WITHIN", value = $"{dateFrom},{dateTo}" }
                            }
                        };

                        var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                        var response = await client.PostAsync("https://api.sigmaworldwide.io/v1/api/saved-search/execute", content);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseString = await response.Content.ReadAsStringAsync();
                            // Deserialize chuỗi vừa lấy về thành object
                            var currentBatch = JsonConvert.DeserializeObject<dynamic>(responseString);
                            var currentData = currentBatch.data as Newtonsoft.Json.Linq.JArray;

                            // Logic dừng: Nếu số lượng data trả về ít hơn limit thì nghĩa là đã hết trang
                            if (currentData != null && currentData.Count == limitPerRequest)
                            {
                                skip++; // Tiếp tục tăng trang (hoặc skip += 1000 tùy logic API)
                            }
                            else
                            {
                                keepRunning = false;
                            }

                            
                            var synchTime = $"{fromDate.ToString("yyyyMMdd")}-{toDate.ToString("yyyyMMdd")}-{page}";

                            // Lưu vào Database
                            var exitingData = dataPortal.GetDataByIDAndSyncDate(savedSearchId, synchTime);
                            if (exitingData != null)
                            {
                                //exitingData.data = responseString;
                                //exitingData.Synch_datetime = synchTime;
                                //dataPortal.Update(exitingData);
                            }
                            else
                            {
                                var result = dataPortal.Insert(new ERP_synch_dataUI
                                             {
                                                savedsearchID = savedSearchId,
                                                data = responseString,
                                                Synch_datetime = synchTime
                                             });
                                if(!result)
                                {
                                    processResult.Message = processResult.Message + Environment.NewLine +  $"Failed to insert data for syncdate: {synchTime}";
                                }
                            }

                            page++;
                        }
                        else
                        {
                            processResult.Message = processResult.Message + Environment.NewLine + $"API call failed at skip {skip} for {savedSearchId} from {dateFrom} to {dateTo}";
                        }
                    }

                    if (!string.IsNullOrEmpty(processResult.Message))
                    {
                        processResult.OK = false;
                    }
                    else 
                    {
                        processResult.OK = true;
                        processResult.Message = "Update data successfully";
                    } 
                }
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        [Route("GetSavedSearch803")]
        [HttpGet]
        public async Task<BODataProcessResult> GetSavedSearch803(DateTime fromDate, DateTime toDate, string savedSearchId = "customsearch803")
        {
            ERP_synch_dataPortal dataPortal = new ERP_synch_dataPortal(SVNDBConfig.ConnectionString);
            BODataProcessResult processResult = new BODataProcessResult();

            var dateFrom = fromDate.ToString("MM/dd/yyyy");
            var dateTo = toDate.ToString("MM/dd/yyyy");

            int skip = 0;
            int page = 1;
            int limitPerRequest = 1000;
            bool keepRunning = true;

            try
            {
                using (var client = new HttpClient())
                {
                    while (keepRunning)
                    {
                        var request = new
                        {
                            savedSearchId = savedSearchId,
                            skip = skip,
                            limit = limitPerRequest,
                            debug = false,
                            filters = new[] {
                                new { field = "trandate", join = "", @operator = "ONORAFTER", value = $"{dateFrom}" }
                            }
                        };

                        var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
                        var response = await client.PostAsync("https://api.sigmaworldwide.io/v1/api/saved-search/execute", content);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseString = await response.Content.ReadAsStringAsync();
                            // Deserialize chuỗi vừa lấy về thành object
                            var currentBatch = JsonConvert.DeserializeObject<dynamic>(responseString);
                            var currentData = currentBatch.data as Newtonsoft.Json.Linq.JArray;

                            // Logic dừng: Nếu số lượng data trả về ít hơn limit thì nghĩa là đã hết trang
                            if (currentData != null && currentData.Count == limitPerRequest)
                            {
                                skip++; // Tiếp tục tăng trang (hoặc skip += 1000 tùy logic API)
                            }
                            else
                            {
                                keepRunning = false;
                            }


                            var synchTime = $"{fromDate.ToString("yyyyMMdd")}-{page}";

                            // Lưu vào Database
                            var exitingData = dataPortal.GetDataByIDAndSyncDate(savedSearchId, synchTime);
                            if (exitingData != null)
                            {
                                //exitingData.data = responseString;
                                //exitingData.Synch_datetime = synchTime;
                                //dataPortal.Update(exitingData);
                            }
                            else
                            {
                                var result = dataPortal.Insert(new ERP_synch_dataUI
                                {
                                    savedsearchID = savedSearchId,
                                    data = responseString,
                                    Synch_datetime = synchTime
                                });
                                if (!result)
                                {
                                    processResult.Message = processResult.Message + Environment.NewLine + $"Failed to insert data for syncdate: {synchTime}";
                                }
                            }

                            page++;
                        }
                        else
                        {
                            processResult.Message = processResult.Message + Environment.NewLine + $"API call failed at skip {skip} for {savedSearchId} from {dateFrom} to {dateTo}";
                        }
                    }

                    if (!string.IsNullOrEmpty(processResult.Message))
                    {
                        processResult.OK = false;
                    }
                    else
                    {
                        processResult.OK = true;
                        processResult.Message = "Update data successfully";
                    }
                }
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return processResult;
        }
    }
}
