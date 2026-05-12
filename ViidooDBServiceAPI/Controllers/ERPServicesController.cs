using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SVNShareLib;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
using SVNShareLib.Utils;
using System.Text;

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

        [Route("GetReport799_803")]
        [HttpGet]
        public async Task<BODataProcessResult> GetReport799_803(string condition)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                SavedSearchServices savedSearchServices = new SavedSearchServices(SVNDBConfig.ConnectionString);
                var data = await savedSearchServices.GetWOData(condition);
                if (data == null)
                {
                    processResult.OK = false;
                    processResult.Message = "Failed to get data from database";
                    return processResult;
                }
                processResult.OK = true;
                processResult.NumOfRow = data.Count;
                processResult.Content = data;
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return processResult;
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
                using (var client = new HttpClient())
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
                        if (exitingData != null)
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
            catch (Exception ex)
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

        [Route("GetSavedSearch799")]
        [HttpGet]
        public async Task<BODataProcessResult> GetSavedSearch799(DateTime fromDate, string savedSearchId = "customsearch799")
        {
            ERP_synch_dataPortal dataPortal = new ERP_synch_dataPortal(SVNDBConfig.ConnectionString);
            BODataProcessResult processResult = new BODataProcessResult();

            var dateFrom = fromDate.ToString("MM/dd/yyyy");

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
                                new { field = "trandate", join = "createdFrom", @operator = "ONORAFTER", value = $"{dateFrom}" }
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
                                exitingData.data = responseString;
                                exitingData.Synch_datetime = synchTime;
                                dataPortal.Update(exitingData);
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
                            processResult.Message = processResult.Message + Environment.NewLine + $"API call failed at skip {skip} for {savedSearchId} from {dateFrom}";
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

        [Route("GetSavedSearch799V1")]
        [HttpGet]
        public async Task<BODataProcessResult> GetSavedSearch799V1(int month, string savedSearchId = "customsearch799")
        {
            ERP_synch_dataPortal dataPortal = new ERP_synch_dataPortal(SVNDBConfig.ConnectionString);
            BODataProcessResult processResult = new BODataProcessResult();

            // 1. Lấy năm hiện tại
            int year = DateTime.Now.Year;

            // 2. Tạo ngày đầu tháng: 01/{month}/{year}
            DateTime firstDayOfMonth = new DateTime(year, month, 1);

            // 3. Tạo ngày cuối tháng: Lấy số ngày trong tháng đó của năm đó
            int daysInMonth = DateTime.DaysInMonth(year, month);
            DateTime lastDayOfMonth = new DateTime(year, month, daysInMonth);

            // Định dạng chuỗi theo yêu cầu của API (MM/dd/yyyy)
            var dateFrom = firstDayOfMonth.ToString("MM/dd/yyyy");
            var dateTo = lastDayOfMonth.ToString("MM/dd/yyyy");

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
                                new { field = "closedate", join = "createdFrom", @operator = "WITHIN", value = $"{dateFrom},{dateTo}" }
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


                            var synchTime = $"{firstDayOfMonth.ToString("yyyyMMdd")}-{page}";

                            // Lưu vào Database
                            var exitingData = dataPortal.GetDataByIDAndSyncDate(savedSearchId, synchTime);
                            if (exitingData != null)
                            {
                                exitingData.data = responseString;
                                exitingData.Synch_datetime = synchTime;
                                dataPortal.Update(exitingData);
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

        [Route("GetSavedSearch803")]
        [HttpGet]
        public async Task<BODataProcessResult> GetSavedSearch803(DateTime fromDate, string savedSearchId = "customsearch803")
        {
            ERP_synch_dataPortal dataPortal = new ERP_synch_dataPortal(SVNDBConfig.ConnectionString);
            BODataProcessResult processResult = new BODataProcessResult();

            var dateFrom = fromDate.ToString("MM/dd/yyyy");

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
                                exitingData.data = responseString;
                                exitingData.Synch_datetime = synchTime;
                                dataPortal.Update(exitingData);
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
                            processResult.Message = processResult.Message + Environment.NewLine + $"API call failed at skip {skip} for {savedSearchId} from {dateFrom}";
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
