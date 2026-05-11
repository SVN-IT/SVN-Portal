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
            var synchTime = fromDate.ToString("yyyyMMdd") + toDate.ToString("dd");

            int skip = 0;
            int limitPerRequest = 1000; // Đặt limit cố định là 1000 theo yêu cầu
            bool keepRunning = true;
            StringBuilder fullResponseBuilder = new StringBuilder();

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

                        var content = new StringContent(
                            Newtonsoft.Json.JsonConvert.SerializeObject(request),
                            Encoding.UTF8,
                            "application/json"
                        );

                        var response = await client.PostAsync("https://api.sigmaworldwide.io/v1/api/saved-search/execute", content);

                        if (response.IsSuccessStatusCode)
                        {
                            var responseString = await response.Content.ReadAsStringAsync();

                            // Cộng dồn dữ liệu vào StringBuilder (nếu bạn muốn lưu tất cả vào 1 bản ghi duy nhất)
                            // Hoặc xử lý lưu từng phần vào DB tại đây tùy theo thiết kế Database của bạn
                            fullResponseBuilder.Append(responseString);

                            // Kiểm tra xem trong response có chứa "limit": 1000 hay không
                            if (responseString.Contains("\"limit\": 1000") || responseString.Contains("\"limit\":1000"))
                            {
                                skip++; // Tăng skip lên 1 (hoặc skip += limitPerRequest tùy theo logic API của bạn)
                            }
                            else
                            {
                                keepRunning = false; // Dừng vòng lặp
                            }
                        }
                        else
                        {
                            processResult.OK = false;
                            processResult.Message = $"API call failed at skip {skip} with status code: {response.StatusCode}";
                            return processResult;
                        }
                    }

                    // Sau khi lấy hết dữ liệu, tiến hành cập nhật/chèn vào Database
                    string finalData = fullResponseBuilder.ToString();
                    var exitingData = dataPortal.GetDataByID(savedSearchId);

                    if (exitingData != null)
                    {
                        exitingData.data = finalData;
                        exitingData.Synch_datetime = synchTime;
                        dataPortal.Update(exitingData);
                    }
                    else
                    {
                        ERP_synch_dataUI newData = new ERP_synch_dataUI
                        {
                            savedsearchID = savedSearchId,
                            data = finalData,
                            Synch_datetime = synchTime
                        };
                        dataPortal.Insert(newData);
                    }

                    processResult.OK = true;
                    processResult.Message = "All pages synchronized successfully";
                }
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        [Route("GetSavedSearchAllPageV1")]
        [HttpGet]
        public async Task<BODataProcessResult> GetSavedSearchAllPageV1(DateTime fromDate, DateTime toDate, string savedSearchId = "customsearch799")
        {
            ERP_synch_dataPortal dataPortal = new ERP_synch_dataPortal(SVNDBConfig.ConnectionString);
            BODataProcessResult processResult = new BODataProcessResult();

            var dateFrom = fromDate.ToString("MM/dd/yyyy");
            var dateTo = toDate.ToString("MM/dd/yyyy");
            var synchTime = fromDate.ToString("yyyyMMdd") + toDate.ToString("dd");

            int skip = 0;
            int limitPerRequest = 1000;
            bool keepRunning = true;

            // Đối tượng gốc để chứa dữ liệu gộp
            dynamic finalJsonObject = null;

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

                            if (finalJsonObject == null)
                            {
                                // Lần đầu tiên: Lấy nguyên cấu trúc của response đầu làm gốc
                                finalJsonObject = currentBatch;
                            }
                            else
                            {
                                // Các lần sau: Chỉ lấy mảng data đổ thêm vào đối tượng gốc
                                if (currentData != null && currentData.Count > 0)
                                {
                                    ((Newtonsoft.Json.Linq.JArray)finalJsonObject.data).Merge(currentData);
                                }
                            }

                            // Logic dừng: Nếu số lượng data trả về ít hơn limit thì nghĩa là đã hết trang
                            if (currentData != null && currentData.Count == limitPerRequest)
                            {
                                skip++; // Tiếp tục tăng trang (hoặc skip += 1000 tùy logic API)
                            }
                            else
                            {
                                keepRunning = false;
                            }
                        }
                        else
                        {
                            processResult.OK = false;
                            processResult.Message = $"API call failed at skip {skip}";
                            return processResult;
                        }
                    }

                    // Sau khi gộp xong, cập nhật lại số lượng tổng vào trường 'count' hoặc 'limit' nếu cần
                    finalJsonObject.count = ((Newtonsoft.Json.Linq.JArray)finalJsonObject.data).Count;

                    // Chuyển đối tượng đã gộp hoàn chỉnh thành chuỗi JSON duy nhất
                    string finalJsonString = JsonConvert.SerializeObject(finalJsonObject);

                    // Lưu vào Database
                    var exitingData = dataPortal.GetDataByID(savedSearchId);
                    if (exitingData != null)
                    {
                        exitingData.data = finalJsonString;
                        exitingData.Synch_datetime = synchTime;
                        dataPortal.Update(exitingData);
                    }
                    else
                    {
                        dataPortal.Insert(new ERP_synch_dataUI
                        {
                            savedsearchID = savedSearchId,
                            data = finalJsonString,
                            Synch_datetime = synchTime
                        });
                    }

                    processResult.OK = true;
                    processResult.Message = "Gộp dữ liệu thành công!";
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
