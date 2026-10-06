using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SVNShareLib;
using SVNShareLib.DTO;
using SVNShareLib.Request;

namespace ViidooDBServiceAPI.Services
{
    public class NewViindooAPIService
    {
        private readonly ViindooDBConfig _dbConfig;
        private readonly HttpClient _httpClient;

        public NewViindooAPIService(ViindooDBConfig dbConfig, HttpClient httpClient)
        {
            _dbConfig = dbConfig;
            _httpClient = httpClient;

            if (_httpClient.BaseAddress == null && !string.IsNullOrEmpty(_dbConfig.ServerUrl))
            {
                _httpClient.BaseAddress = new Uri(_dbConfig.ServerUrl.TrimEnd('/') + "/");
            }
        }

        #region Core JSON-RPC Executor (Tự động gán Session ID Cookie)
        private async Task<dynamic> CallKwAsync(string model, string method, JsonRpcRequest rpcRequest)
        {
            string relativeUrl = $"web/dataset/call_kw/{model}/{method}";

            string jsonBody = JsonConvert.SerializeObject(rpcRequest);
            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, relativeUrl)
            {
                Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
            };

            // Tự động gán Session ID vào Header Cookie cho MỌI request
            if (!string.IsNullOrEmpty(_dbConfig.SessionID))
            {
                requestMessage.Headers.Add("Cookie", $"session_id={_dbConfig.SessionID}");
            }

            HttpResponseMessage response = await _httpClient.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Viindoo API HTTP Error: {response.StatusCode}");
            }

            string responseContent = await response.Content.ReadAsStringAsync();
            dynamic jsonResult = JsonConvert.DeserializeObject(responseContent);

            if (jsonResult?.error != null)
            {
                string errMsg = jsonResult.error.data?.message?.ToString()
                               ?? jsonResult.error.message?.ToString()
                               ?? "Lỗi từ Viindoo API";
                throw new Exception(errMsg);
            }

            return jsonResult;
        }

        private JsonRpcRequest CreateBaseRpcRequest(string model, string method, List<object> args, Kwargs kwargs = null)
        {
            return new JsonRpcRequest
            {
                Id = Random.Shared.Next(1, 10000),
                Jsonrpc = "2.0",
                Method = "call",
                Params = new ParamsData
                {
                    Model = model,
                    Method = method,
                    Args = args ?? new List<object>(),
                    Kwargs = kwargs ?? new Kwargs
                    {
                        Context = new Context
                        {
                            Lang = "vi_VN",
                            Tz = "Asia/Ho_Chi_Minh",
                            Uid = _dbConfig.UserID,
                            Allowed_Company_Ids = new List<int> { 1 },
                            Bin_Size = true,
                            Default_Company_Id = 1
                        }
                    }
                }
            };
        }
        #endregion

        #region Authentication
        public async Task<BODataProcessResult> LoginAsync()
        {
            var result = new BODataProcessResult();
            try
            {
                var authPayload = new
                {
                    jsonrpc = "2.0",
                    method = "call",
                    params_Data = new
                    {
                        db = _dbConfig.DbName,
                        login = _dbConfig.Username,
                        password = _dbConfig.Password
                    },
                    id = Random.Shared.Next(1, 10000)
                };

                string jsonContent = JsonConvert.SerializeObject(authPayload);
                using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "web/session/authenticate")
                {
                    Content = new StringContent(jsonContent, Encoding.UTF8, "application/json")
                };

                HttpResponseMessage response = await _httpClient.SendAsync(requestMessage);
                if (!response.IsSuccessStatusCode)
                {
                    result.OK = false;
                    result.Message = $"Lỗi kết nối máy chủ Viindoo HTTP {response.StatusCode}";
                    return result;
                }

                string responseBody = await response.Content.ReadAsStringAsync();
                dynamic jsonResponse = JsonConvert.DeserializeObject(responseBody);

                if (jsonResponse?.error != null)
                {
                    result.OK = false;
                    result.Message = jsonResponse.error.data?.message?.ToString() ?? "Đăng nhập Viindoo thất bại.";
                    return result;
                }

                int uid = jsonResponse?.result?.uid != null ? (int)jsonResponse.result.uid : 0;
                if (uid <= 0)
                {
                    result.OK = false;
                    result.Message = "Tài khoản hoặc mật khẩu Viindoo không chính xác.";
                    return result;
                }

                string sessionId = string.Empty;
                if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
                {
                    foreach (var cookie in cookies)
                    {
                        if (cookie.Contains("session_id="))
                        {
                            var sessionPart = cookie.Split(';')
                                                    .FirstOrDefault(p => p.Trim().StartsWith("session_id="));
                            if (sessionPart != null)
                            {
                                sessionId = sessionPart.Split('=')[1].Trim();
                                break;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(sessionId) && jsonResponse?.result?.session_id != null)
                {
                    sessionId = jsonResponse.result.session_id.ToString();
                }

                _dbConfig.UserID = uid;
                _dbConfig.SessionID = sessionId;

                result.OK = true;
                result.UserID = uid;
                result.DataType = sessionId;
                result.Message = "Đăng nhập Viindoo thành công.";
            }
            catch (Exception ex)
            {
                result.OK = false;
                result.Message = $"Lỗi ngoại lệ khi đăng nhập Viindoo: {ex.Message}";
            }

            return result;
        }
        #endregion

        #region Production & Consume API Calls
        public async Task<dynamic> ReadProductionByProductIDAsync(string name)
        {
            var request = CreateBaseRpcRequest("mrp.production", "search_read", new List<object>
            {
                new List<object> { new List<object> { "name", "=", name } },
                new List<string> { "id", "name", "product_id", "product_qty", "qty_producing", "qty_produced",
                                   "product_tracking", "company_id", "move_raw_ids", "state" }
            });

            dynamic response = await CallKwAsync("mrp.production", "search_read", request);
            JArray records = response?.result;
            return records != null && records.Count > 0 ? records[0] : null;
        }

        public async Task<dynamic> GetStockMoveByIDAsync(List<int> moveIds, int companyId)
        {
            if (moveIds == null || !moveIds.Any()) return new JArray();

            var request = CreateBaseRpcRequest("stock.move", "read", new List<object>
            {
                moveIds,
                new List<string> { "id", "product_id", "has_tracking", "move_line_ids", "location_id", "location_dest_id", "warehouse_id", "picking_type_id", "company_id", "product_uom" }
            });

            dynamic response = await CallKwAsync("stock.move", "read", request);
            return response?.result ?? new JArray();
        }

        public async Task<int> GetLotInfoAsync(string lotName, int productId)
        {
            var request = CreateBaseRpcRequest("stock.production.lot", "search_read", new List<object>
            {
                new List<object>
                {
                    new List<object> { "name", "=", lotName },
                    new List<object> { "product_id", "=", productId }
                },
                new List<string> { "id", "name" }
            });

            dynamic response = await CallKwAsync("stock.production.lot", "search_read", request);
            JArray result = response?.result;
            return result != null && result.Count > 0 ? (int)result[0]["id"] : 0;
        }

        /// <summary>
        /// Tạo hoặc cập nhật Stock Move Line cho NVL tiêu hao (Consume Component)
        /// </summary>
        public async Task<dynamic> ConsumeComponentAsync(dynamic stockMoveItem, int lotId, decimal quantity, int moId)
        {
            var moveLineData = new JObject
            {
                ["move_id"] = stockMoveItem.id,
                ["product_id"] = stockMoveItem.product_id[0],
                ["product_uom_id"] = stockMoveItem.product_uom[0],
                ["location_id"] = stockMoveItem.location_id[0],
                ["location_dest_id"] = stockMoveItem.location_dest_id[0],
                ["qty_done"] = quantity,
                ["lot_id"] = lotId,
                ["production_id"] = moId
            };

            var request = CreateBaseRpcRequest("stock.move.line", "create", new List<object> { moveLineData });
            return await CallKwAsync("stock.move.line", "create", request);
        }

        public async Task<dynamic> GetStockMoveLineByIDAsync(int moveLineId)
        {
            var request = CreateBaseRpcRequest("stock.move.line", "read", new List<object>
            {
                new List<int> { moveLineId },
                new List<string> { "id", "product_id", "lot_id", "qty_done", "move_id" }
            });

            dynamic response = await CallKwAsync("stock.move.line", "read", request);
            JArray result = response?.result;
            return result != null && result.Count > 0 ? result[0] : null;
        }

        public async Task<dynamic> SaveSerialStockMoveAsync(dynamic item, object[] moveLineIds)
        {
            int moveId = (int)item.id;
            var vals = new JObject { ["move_line_ids"] = JArray.FromObject(moveLineIds) };

            var request = CreateBaseRpcRequest("stock.move", "write", new List<object> { new List<int> { moveId }, vals });
            return await CallKwAsync("stock.move", "write", request);
        }

        public async Task<dynamic> LotSearchAsync(string lotName, int productId, int companyId)
        {
            var request = CreateBaseRpcRequest("stock.production.lot", "search_read", new List<object>
            {
                new List<object>
                {
                    new List<object> { "name", "=", lotName },
                    new List<object> { "product_id", "=", productId },
                    new List<object> { "company_id", "=", companyId }
                },
                new List<string> { "id", "name" }
            });

            dynamic response = await CallKwAsync("stock.production.lot", "search_read", request);
            return response?.result;
        }

        public async Task<dynamic> CreateLotAsync(string lotName, int productId, int companyId)
        {
            var vals = new JObject
            {
                ["name"] = lotName,
                ["product_id"] = productId,
                ["company_id"] = companyId
            };

            var request = CreateBaseRpcRequest("stock.production.lot", "create", new List<object> { vals });
            return await CallKwAsync("stock.production.lot", "create", request);
        }

        public async Task<dynamic> CheckUsedLotIDAsync(int lotId)
        {
            if (lotId <= 0) return null;

            var request = CreateBaseRpcRequest("mrp.production", "search_read", new List<object>
            {
                new List<object> { new List<object> { "lot_producing_id", "=", lotId } },
                new List<string> { "id", "name" }
            });

            dynamic response = await CallKwAsync("mrp.production", "search_read", request);
            JArray result = response?.result;
            return result != null && result.Count > 0 ? result[0] : null;
        }

        public async Task<dynamic> SaveProductionOrderAsyncv1(int mrpProductionId, int lotId, decimal quantity, object[] moveRawIds, object[] workOrderIds)
        {
            var vals = new JObject
            {
                ["qty_producing"] = quantity,
                ["move_raw_ids"] = JArray.FromObject(moveRawIds)
            };

            if (lotId > 0) vals["lot_producing_id"] = lotId;
            if (workOrderIds != null && workOrderIds.Length > 0) vals["workorder_ids"] = JArray.FromObject(workOrderIds);

            var request = CreateBaseRpcRequest("mrp.production", "write", new List<object> { new List<int> { mrpProductionId }, vals });
            return await CallKwAsync("mrp.production", "write", request);
        }

        public async Task<dynamic> MarkDoneProductionOrderAsync(int mrpProductionId)
        {
            var request = CreateBaseRpcRequest("mrp.production", "button_mark_done", new List<object> { new List<int> { mrpProductionId } });
            return await CallKwAsync("mrp.production", "button_mark_done", request);
        }

        public async Task<dynamic> BackOrderOnchange(int mrpProductionId)
        {
            var request = CreateBaseRpcRequest("mrp.production", "onchange_producing_quantity", new List<object> { new List<int> { mrpProductionId } });
            return await CallKwAsync("mrp.production", "onchange_producing_quantity", request);
        }

        public async Task<int> BackOrderCreate(int mrpProductionId, int lotId)
        {
            var request = CreateBaseRpcRequest("mrp.production.backorder", "create", new List<object>
            {
                new JObject { ["mrp_production_ids"] = new JArray { mrpProductionId } }
            });

            dynamic response = await CallKwAsync("mrp.production.backorder", "create", request);
            return response?.result != null ? (int)response.result : 0;
        }

        public async Task<dynamic> BackOrderAction(int mrpProductionId, int backOrderId)
        {
            if (backOrderId <= 0) return null;
            var request = CreateBaseRpcRequest("mrp.production.backorder", "action_backorder", new List<object> { new List<int> { backOrderId } });
            return await CallKwAsync("mrp.production.backorder", "action_backorder", request);
        }
        #endregion
    }
}