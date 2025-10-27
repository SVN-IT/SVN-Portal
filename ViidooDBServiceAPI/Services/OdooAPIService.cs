using Newtonsoft.Json.Linq;
using SVNShareLib;
using System.Text;
using System;
using System.Net;
using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Collections.Generic;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace ViidooDBServiceAPI.Services
{
    public class OdooAPIService
    {
        ViindooDBConfig dbConfig;
        public OdooAPIService(ViindooDBConfig dbConfig)
        {
            this.dbConfig = dbConfig;
        }


        /// <summary>
        /// Hàm API để đăng nhập vào Odoo và lấy thông tin người dùng.
        /// </summary>
        /// <returns></returns>
        public async Task<BODataProcessResult> LoginAsync()
        {
            BODataProcessResult processResult = new BODataProcessResult();
            int uid = 0;
            string session_id = string.Empty;
            try
            {

                using (var client = new HttpClient())
                {


                    var payload = new
                    {
                        jsonrpc = "2.0",
                        method = "call",
                        @params = new
                        {
                            db = dbConfig.DbName,
                            login = dbConfig.Username,
                            password = dbConfig.Password
                        },
                        id = 1
                    };

                    var content = new StringContent(
                        Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                        Encoding.UTF8,
                        "application/json"
                    );

                    var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/session/authenticate", content);

                    if (response.IsSuccessStatusCode)
                    {
                        response.EnsureSuccessStatusCode();
                        var responseString = await response.Content.ReadAsStringAsync();

                        var json = JObject.Parse(responseString);
                        uid = json["result"]?["uid"]?.Value<int>() ?? 0;

                        if (response.Headers.TryGetValues("Set-Cookie", out var setCookieValues))
                        {

                            foreach (var cookie in setCookieValues)
                            {
                                if (cookie.StartsWith("session_id"))
                                {
                                    var sessionIdPart = cookie.Split(';')[0]; // "session_id=xxxxx"

                                    // Lấy phần sau dấu '='
                                    session_id = sessionIdPart.Split('=')[1];
                                }
                            }
                        }
                    }

                    if (uid == 0)
                    {
                        processResult.OK = false;
                        processResult.Message = "Login failed. Please check your credentials.";
                    }
                    else
                    {
                        processResult.OK = true;
                        processResult.UserID = uid;
                        processResult.DataType = session_id; // Assuming session_id is used as DataType here
                        processResult.OdooUserID = uid; // Assuming Odoo User ID is the same as the UID returned
                        processResult.Message = "Login successful.";
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


        #region Lệnh sản xuất
        /// <summary>
        /// Hàm API để đọc thông tin sản xuất từ Odoo.
        /// </summary>
        /// <param name="productionId"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, string>> ReadProductionAsync(int productionId, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");
                var payload = new
                {
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        model = "mrp.production",
                        method = "read",
                        args = new object[]
                        {
                            new int[] { productionId },
                            new string[]
                            {
                                "confirm_cancel", "show_lock", "move_byproduct_ids", "state",
                                "show_serial_mass_produce", "check_ids", "check_todo", "reservation_state", "date_planned_finished", "is_locked", "qty_produced",
                                "unreserve_visible", "reserve_visible", "consumption", "is_planned", "show_allocation", "workorder_ids", "eco_count", "purchase_order_count",
                                "sale_order_count", "mrp_production_child_count", "mrp_production_source_count", "mrp_production_backorder_count", "unbuild_count", "scrap_count",
                                "delivery_count", "alert_count", "package_count", "account_moves_count", "maintenance_count", "document_count", "overview_progress", "priority",
                                "name", "id", "use_create_components_lots", "show_lot_ids", "product_tracking", "show_valuation", "product_id", "product_tmpl_id",
                                "forecasted_issue", "company_id", "product_description_variants", "bom_id", "qty_producing", "product_qty", "product_uom_category_id",
                                "product_uom_id", "product_packaging_id", "lot_producing_id", "date_planned_start", "delay_alert_date", "json_popover",
                                "components_availability_state", "components_availability", "show_final_lots", "production_location_id", "move_finished_ids",
                                "move_raw_ids", "picking_type_id", "location_src_id", "warehouse_id", "location_dest_id", "origin", "date_deadline", "display_name"
                            }
                        },
                        kwargs = new
                        {
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                allowed_company_ids = new List<int> { 1 },
                                bin_size = true,
                                uid = uid
                            }
                        }
                    },
                    id = 34
                };

                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/mrp.production/read", content);
                var responseString = await response.Content.ReadAsStringAsync();

                var json = JObject.Parse(responseString);

                var resultArray = (JArray)json["result"];

                var dictionary = ((JObject)resultArray[0])
                 .Properties()
                 .ToDictionary(p => p.Name, p => p.Value.ToString());

                return dictionary;
            }
        }

        /// <summary>
        /// Hàm tìm kiếm lot
        /// </summary>
        /// <param name="lotNumber"></param>
        /// <param name="product_id"></param>
        /// <param name="company_id"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public async Task<JArray> LotSearchAsync(string lotNumber, int product_id, int company_id, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");
                var payload = new
                {
                    id = 56,
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        model = "stock.lot",
                        method = "name_search",
                        args = new object[] { },
                        kwargs = new
                        {
                            name = lotNumber,
                            @operator = "ilike",
                            args = new object[]
                            {
                                "&",
                                new object[] { "product_id", "=", product_id },
                                new object[] { "company_id", "=", company_id }
                            },
                            limit = 8,
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = 2,
                                allowed_company_ids = new int[] { company_id },
                                default_product_id = product_id,
                                default_company_id = company_id
                            }
                        }
                    }
                };
                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );
                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/stock.lot/name_search", content);
                var responseString = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseString);
                var resultArray = (JArray)json["result"];
                if (resultArray.Count == 0)
                {
                    return null;
                }
                return resultArray;
            }
        }

        public async Task<JArray> CreateLotAsync(string lotNumber, int product_id, int company_id, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");
                var payload = new
                {
                    id = 57,
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        args = new object[]
                        {
                            lotNumber
                        },
                        model = "stock.lot",
                        method = "name_create",
                        kwargs = new
                        {
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = 2,
                                allowed_company_ids = new int[] { company_id },
                                default_product_id = product_id,
                                default_company_id = company_id
                            }
                        }
                    }
                };
                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );
                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/stock.lot/name_create", content);
                var responseString = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseString);
                var resultArray = (JArray)json["result"];
                if (resultArray.Count == 0)
                {
                    return null;
                }
                return resultArray;
            }
        }

        /// <summary>
        /// Hàm API để đọc thông tin sản xuất bằng mã productID từ Odoo.
        /// </summary>
        /// <param name="productionId"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, string>> ReadProductionByProductIDAsync(string name, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");
                var payload = new
                {
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        model = "mrp.production",
                        method = "search_read",
                        args = new object[]
                        {

                        },
                        kwargs = new
                        {
                            domain = new object[] { new object[] { "name", "ilike", name } },
                            fields = new string[]
                            {
                                "confirm_cancel", "show_lock", "move_byproduct_ids", "state",
                                "show_serial_mass_produce", "check_ids", "check_todo", "reservation_state", "date_planned_finished", "is_locked", "qty_produced",
                                "unreserve_visible", "reserve_visible", "consumption", "is_planned", "show_allocation", "workorder_ids", "eco_count", "purchase_order_count",
                                "sale_order_count", "mrp_production_child_count", "mrp_production_source_count", "mrp_production_backorder_count", "unbuild_count", "scrap_count",
                                "delivery_count", "alert_count", "package_count", "account_moves_count", "maintenance_count", "document_count", "overview_progress", "priority",
                                "name", "id", "use_create_components_lots", "show_lot_ids", "product_tracking", "show_valuation", "product_id", "product_tmpl_id",
                                "forecasted_issue", "company_id", "product_description_variants", "bom_id", "qty_producing", "product_qty", "product_uom_category_id",
                                "product_uom_id", "product_packaging_id", "lot_producing_id", "date_planned_start", "delay_alert_date", "json_popover",
                                "components_availability_state", "components_availability", "show_final_lots", "production_location_id", "move_finished_ids",
                                "move_raw_ids", "picking_type_id", "location_src_id", "warehouse_id", "location_dest_id", "origin", "date_deadline", "display_name"
                            },
                            order = "create_date desc",
                            limit = 1,
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                allowed_company_ids = new List<int> { 1 },
                                bin_size = true,
                                uid = uid
                            }
                        }
                    },
                    id = 100
                };

                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/mrp.production/read", content);
                var responseString = await response.Content.ReadAsStringAsync();

                var json = JObject.Parse(responseString);

                var resultArray = (JArray)json["result"];

                var dictionary = ((JObject)resultArray[0])
                 .Properties()
                 .ToDictionary(p => p.Name, p => p.Value.ToString());

                return dictionary;
            }
        }

        /// <summary>
        /// Hàm API để kiểm tra mã lot đã dc sử dụng chưa.
        /// </summary>
        /// <param name="lotId"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, string>> CheckUsedLotIDAsync(int lotId, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");
                var payload = new
                {
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        model = "mrp.production",
                        method = "search_read",
                        args = new object[]
                        {

                        },
                        kwargs = new
                        {
                            domain = new object[] { new object[] { "lot_producing_id", "=", lotId } },
                            fields = new string[]
                            {
                                "name", "state", "product_id", "name"
                            },
                            order = "create_date desc",
                            limit = 1,
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                allowed_company_ids = new List<int> { 1 },
                                bin_size = true,
                                uid = uid
                            }
                        }
                    },
                    id = 100
                };

                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/mrp.production/read", content);
                var responseString = await response.Content.ReadAsStringAsync();

                var json = JObject.Parse(responseString);

                var resultArray = (JArray)json["result"];

                try
                {
                    var dictionary = ((JObject)resultArray[0])
                         .Properties()
                         .ToDictionary(p => p.Name, p => p.Value.ToString());

                    return dictionary;
                }
                catch (Exception ex)
                {
                    return null;
                }
                    
            }
        }

        /// <summary>
        /// Hàm tìm kiếm mã lot để input vào lệnh sản xuất
        /// Check xem lệnh đã được nhập cho thành phẩm nào chưa
        /// </summary>
        /// <param name="lot_name"></param>
        /// <param name="product_id"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, string>> GetLotByNameAndProductIDAsync(int move_id, string mo_name, string lot_name, int product_id, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");

                var payload = new
                {
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        model = "stock.move.line",
                        method = "search_read",
                        args = new object[]
                        {

                        },
                        kwargs = new
                        {
                            domain = new object[] {
                                new object[] { "lot_id.name", "=", lot_name },
                                new object[] { "reference", "ilike", mo_name },
                                new object[] { "qty_done", "!=", 0 },
                                new object[] { "product_id", "=", product_id }
                            },
                            fields = new string[]
                            {
                                "id", "move_id", "lot_id", "product_id", "qty_done", "workorder_id", "production_id", "reference"
                            },
                            order = "create_date desc",
                            limit = 3,
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                allowed_company_ids = new List<int> { 1 },
                                bin_size = true,
                                uid = uid
                            }
                        }
                    },
                    id = 100
                };

                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/stock.move.line/read", content);
                var responseString = await response.Content.ReadAsStringAsync();

                var json = JObject.Parse(responseString);

                var resultArray = (JArray)json["result"];

                if(resultArray.Count != 0) /*|| resultArray.Count > 1*/
                {
                    return null; // Nhiều hơn 1 kết quả, không thể xác định duy nhất
                }
                else
                {
                    // Lấy stock.move.line của lệnh sx
                    payload = new
                    {
                        jsonrpc = "2.0",
                        method = "call",
                        @params = new
                        {
                            model = "stock.move.line",
                            method = "search_read",
                            args = new object[]
                        {

                        },
                            kwargs = new
                            {
                                domain = new object[] {
                                new object[] { "lot_id.name", "=", lot_name },
                                new object[] { "product_id", "=", product_id },
                                new object[] { "move_id", "=", move_id }
                            },
                                fields = new string[]
                            {
                                "id", "move_id", "lot_id", "product_id", "qty_done"
                            },
                                order = "create_date desc",
                                limit = 3,
                                context = new
                                {
                                    lang = "vi_VN",
                                    tz = "Asia/Ho_Chi_Minh",
                                    allowed_company_ids = new List<int> { 1 },
                                    bin_size = true,
                                    uid = uid
                                }
                            }
                        },
                        id = 100
                    };
                    content = new StringContent(
                        Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                        Encoding.UTF8,
                        "application/json"
                    );
                    response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/stock.move.line/read", content);
                    responseString = await response.Content.ReadAsStringAsync();
                    json = JObject.Parse(responseString);
                    resultArray = (JArray)json["result"];
                    try
                    {
                        var dictionary = ((JObject)resultArray[0])
                             .Properties()
                             .ToDictionary(p => p.Name, p => p.Value.ToString());
                        return dictionary;
                    }
                    catch (Exception ex)
                    {
                        return null;
                    }
                }

            }
        }

        /// <summary>
        /// Chuyền vào mã lot
        /// lấy ra danh sách stock.move.line đang tồn tại
        /// </summary>
        /// <param name="lot_name"></param>
        /// <param name="product_id"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, string>> GetStockMoveLineByLotNameAsync(string lot_name, int product_id, List<string> stockMoveLine, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");
                var payload = new
                {
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        model = "stock.move.line",
                        method = "search_read",
                        args = new object[]
                        {
                        },
                        kwargs = new
                        {
                            domain = new object[] {
                                new object[] { "lot_id.name", "=", lot_name },
                                new object[] { "product_id", "=", product_id }
                            },
                            fields = new string[]
                            {
                                "id", "move_id", "lot_id", "product_id", "qty_done"
                            },
                            order = "create_date desc",
                            limit = 0,
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                allowed_company_ids = new List<int> { 1 },
                                bin_size = true,
                                uid = uid
                            }
                        }
                    },
                    id = 100
                };
                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );
                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/stock.move.line/read", content);
                var responseString = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseString);
                var resultArray = (JArray)json["result"];
                try
                {
                    List<string> ids = resultArray
                                .Select(x => x["id"]?.ToString())
                                .Where(x => !string.IsNullOrEmpty(x))
                                .ToList();

                    object[] move_id = resultArray.Select(x => x["move_id"]).ToArray();
                    object[] lot_id = resultArray.Select(x => x["lot_id"]).ToArray();
                    object[] product_ids = resultArray.Select(x => x["product_id"]).ToArray();
                    List<string> qty_done = resultArray.Select(x => x["qty_done"]?.ToString())
                                            .Where(x => !string.IsNullOrEmpty(x))
                                            .ToList();

                    var selectedID = ids.FirstOrDefault(x => stockMoveLine.Contains(x));
                    if (!string.IsNullOrWhiteSpace(selectedID))
                    {
                        int index = ids.IndexOf(selectedID);

                        Dictionary<string, string> dictionary = new Dictionary<string, string>();
                        dictionary["id"] = selectedID;
                        dictionary["move_id"] = move_id[index].ToString();
                        dictionary["lot_id"] = lot_id[index].ToString();
                        dictionary["product_id"] = product_ids[index].ToString();
                        dictionary["qty_done"] = qty_done[index].ToString();
                        return dictionary;
                    }
                    else
                    {
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Hàm tìm kiếm mã lot để input vào lệnh sản xuất
        /// Check xem lệnh đã được nhập cho thành phẩm nào chưa
        /// </summary>
        /// <param name="lot_name"></param>
        /// <param name="product_id"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, string>> GetUsedLotForComponemtAsync(string mo_name, string lot_name, int product_id, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");
                var payload = new
                {
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        model = "stock.move.line",
                        method = "search_read",
                        args = new object[]
                        {

                        },
                        kwargs = new
                        {
                            domain = new object[] {
                                new object[] { "lot_id.name", "=", lot_name },
                                new object[] { "reference", "ilike", mo_name },
                                new object[] { "qty_done", "!=", 0 },
                                new object[] { "product_id", "=", product_id }
                            },
                            fields = new string[]
                            {
                                "id", "move_id", "lot_id", "product_id", "qty_done", "workorder_id", "production_id", "reference"
                            },
                            order = "create_date desc",
                            limit = 3,
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                allowed_company_ids = new List<int> { 1 },
                                bin_size = true,
                                uid = uid
                            }
                        }
                    },
                    id = 100
                };

                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/stock.move.line/read", content);
                var responseString = await response.Content.ReadAsStringAsync();

                var json = JObject.Parse(responseString);

                var resultArray = (JArray)json["result"];

                if (resultArray.Count == 0)
                {
                    return null; // Nhiều hơn 1 kết quả, không thể xác định duy nhất
                }
                try
                {
                    var dictionary = ((JObject)resultArray[0])
                         .Properties()
                         .ToDictionary(p => p.Name, p => p.Value.ToString());
                    return dictionary;
                }
                catch (Exception ex)
                {
                    return null;
                }

            }
        }

        /// <summary>
        /// Hàm xử lý tiêu hao nguyên vật liệu theo BOM trong Odoo.
        /// </summary>
        /// <param name="productionOrderInfo"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <param name="qty_producing"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<List<Dictionary<string, object>>> GetStockMoveByIDAsync(int[] move_ids, int company_id, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");
                var payload = new
                {
                    id = 369,
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        args = new object[]
                        {
                            move_ids,
                            new string[]
                            {
                                "product_id","location_id","product_uom","propagate_cancel","price_unit","company_id",
                                "product_uom_category_id","name","allowed_operation_ids","unit_factor","date_deadline","date",
                                "additional","picking_type_id","has_tracking","operation_id","is_done","bom_line_id","sequence",
                                "warehouse_id","is_locked","move_lines_count","location_dest_id","state","should_consume_qty",
                                "product_uom_qty","product_type","product_qty","reserved_availability","forecast_expected_date",
                                "forecast_availability","quantity_done","component_standard_consumption_id","manual_consumption",
                                "show_details_visible","lot_ids","group_id","move_line_ids"
                            }
                        },
                        model = "stock.move",
                        method = "read",
                        kwargs = new
                        {
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = uid,
                                allowed_company_ids = new int[] { company_id }
                            }
                        }
                    }
                };
                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );
                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/stock.move/read", content);
                var responseString = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseString);
                var resultArray = (JArray)json["result"];

                try
                {
                    List<Dictionary<string, object>> dictionarys = new List<Dictionary<string, object>>();
                    for(var i =0; i < resultArray.Count; i++)
                    {
                        var dictionary = ((JObject)resultArray[i])
                         .Properties()
                         .ToDictionary(p => p.Name, p => (object)p.Value);

                        dictionarys.Add(dictionary);
                    }
                    

                    return dictionarys;
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Hàm xử lý tiêu hao nguyên vật liệu theo BOM trong Odoo.
        /// </summary>
        /// <param name="productionOrderInfo"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <param name="qty_producing"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<Dictionary<string, object>> SaveSerialStockMoveAsync(Dictionary<string, string> stockMoveInfo, object[] move_line_ids, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                var arrMoveID = JsonConvert.DeserializeObject<object[]>(stockMoveInfo["move_id"].ToString());
                var id = Convert.ToInt32(arrMoveID[0]);

                var arrLocationID = JsonConvert.DeserializeObject<object[]>(stockMoveInfo["location_id"].ToString());
                var location_id = Convert.ToInt32(arrLocationID[0]);

                var arrLocationDestID = JsonConvert.DeserializeObject<object[]>(stockMoveInfo["location_dest_id"].ToString());
                var location_dest_id = Convert.ToInt32(arrLocationDestID[0]);

                var arrWarehouseID = JsonConvert.DeserializeObject<object[]>(stockMoveInfo["warehouse_id"].ToString());
                var warehouse_id = Convert.ToInt32(arrWarehouseID[0]);

                var arrPickingTypeID = JsonConvert.DeserializeObject<object[]>(stockMoveInfo["picking_type_id"].ToString());
                var picking_type_id = Convert.ToInt32(arrPickingTypeID[0]);

                var arrCompanyID = JsonConvert.DeserializeObject<object[]>(stockMoveInfo["company_id"].ToString());
                var company_id = Convert.ToInt32(arrCompanyID[0]);

                var mo_id = Convert.ToInt32(stockMoveInfo["mo_id"].ToString());

                string update_date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");



                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");

                // Gửi request tiêu thụ nguyên vật liệu
                var payload = new
                {
                    id = 669,
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        args = new object[]
                        {
                            new object[] { id },
                            new
                            {
                                move_line_ids = move_line_ids
                            }
                        },
                        model = "stock.move",
                        method = "write",
                        kwargs = new
                        {
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = uid,
                                allowed_company_ids = new int[] { company_id },
                                default_product_uom_qty = 0,
                                active_model = "stock.move",
                                active_id = id,
                                active_ids = new int[] { id },
                                default_date = update_date,
                                default_date_deadline = update_date,
                                default_location_id = location_id,
                                default_location_dest_id = location_dest_id,
                                default_warehouse_id = warehouse_id,
                                default_state = "draft",
                                default_raw_material_production_id = mo_id,
                                default_picking_type_id = picking_type_id,
                                default_company_id = company_id,
                                show_owner = true,
                                show_lots_m2o = true,
                                show_lots_text = false,
                                show_source_location = true,
                                show_destination_location = false,
                                show_package = true,
                                show_reserved_quantity = true,
                                force_manual_consumption = true,
                                active_mo_id = mo_id
                            }
                        }
                    }
                };

                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );
                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/stock.move/write", content);
                var responseString = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseString);
                if (json["error"] != null)
                {
                    throw new Exception(json["error"]["message"].ToString());
                }
                return json.ToObject<Dictionary<string, object>>();
            }
        }

        /// <summary>
        /// Hàm xử lý tiêu hao nguyên vật liệu theo BOM trong Odoo.
        /// </summary>
        /// <param name="productionOrderInfo"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <param name="qty_producing"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<Dictionary<string, object>> ConsumeMaterialsByBOMAsync(Dictionary<string, string> productionOrderInfo, int uid, string sessionId, int qty_producing)
        {
            using (var client = new HttpClient())
            {

                var str_move_ids = productionOrderInfo["move_raw_ids"].Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Split(",");
                var move_ids = Array.ConvertAll(str_move_ids, int.Parse);
                var move_raw_ids = move_ids
                    .Select(id => new object[] { 4, id, false })
                    .ToArray();

                //Lấy product_id
                var arrProductID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["product_id"]);
                var product_id = Convert.ToInt32(arrProductID[0]);

                //Lấy product_tmpl_id
                var arrProductTmplID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["product_tmpl_id"]);
                var product_tmpl_id = Convert.ToInt32(arrProductTmplID[0]);

                //Lấy company_id
                var arrCompanyID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["company_id"]);
                var company_id = Convert.ToInt32(arrCompanyID[0]);

                //Lấy bom_id
                var arrBomID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["bom_id"]);
                var bom_id = Convert.ToInt32(arrBomID[0]);

                //Lấy product_uom_category_id
                var arrProductUomCatID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["product_uom_category_id"]);
                var product_uom_category_id = Convert.ToInt32(arrProductUomCatID[0]);

                //Lấy product_uom_id
                var arrProductUomID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["product_uom_id"]);
                var product_uom_id = Convert.ToInt32(arrProductUomID[0]);

                //Lấy move_finished_ids
                var arrMoveFinishedID = productionOrderInfo["move_finished_ids"].Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Split(",");
                var arr_move_finished_ids = Array.ConvertAll(arrMoveFinishedID, int.Parse);
                var move_finished_ids = arr_move_finished_ids
                    .Select(id => new object[] { 4, id, false })
                    .ToArray();

                //Lấy production_location_id
                var arrProductLocID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["production_location_id"]);
                var production_location_id = Convert.ToInt32(arrProductLocID[0]);

                //Lấy picking_type_id
                var arrPickingTypeID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["picking_type_id"]);
                var picking_type_id = Convert.ToInt32(arrPickingTypeID[0]);

                //Lấy location_src_id
                var arrLocSrcID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["location_src_id"]);
                var location_src_id = Convert.ToInt32(arrLocSrcID[0]);

                //Lấy warehouse_id
                var arrWarehouseID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["warehouse_id"]);
                var warehouse_id = Convert.ToInt32(arrWarehouseID[0]);

                //Lấy location_dest_id
                var arrLocDescID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["location_dest_id"]);
                var location_dest_id = Convert.ToInt32(arrLocDescID[0]);

                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");

                // Gửi request tiêu thụ nguyên vật liệu
                var payload = new
                {
                    id = 127,
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        args = new object[]
                        {
                            new int[] { int.Parse(productionOrderInfo["id"]) }, // ID

                            // Object thông tin chi tiết
                            new
                            {
                                id = int.Parse(productionOrderInfo["id"]),
                                confirm_cancel = bool.Parse(productionOrderInfo["confirm_cancel"]),
                                show_lock = bool.Parse(productionOrderInfo["show_lock"]),
                                move_byproduct_ids = new object[] { },
                                state = productionOrderInfo["state"],
                                show_serial_mass_produce = bool.Parse(productionOrderInfo["show_serial_mass_produce"]),
                                check_ids = new object[] { },
                                check_todo = bool.Parse(productionOrderInfo["check_todo"]),
                                reservation_state = productionOrderInfo["reservation_state"],
                                date_planned_finished = productionOrderInfo["date_planned_finished"],
                                is_locked = bool.Parse(productionOrderInfo["is_locked"]),
                                qty_produced = int.Parse(productionOrderInfo["qty_produced"]),
                                unreserve_visible = bool.Parse(productionOrderInfo["unreserve_visible"]),
                                reserve_visible = bool.Parse(productionOrderInfo["reserve_visible"]),
                                consumption = productionOrderInfo["consumption"],
                                is_planned = bool.Parse(productionOrderInfo["is_planned"]),
                                show_allocation = bool.Parse(productionOrderInfo["show_allocation"]),
                                workorder_ids = new object[] { },
                                eco_count = int.Parse(productionOrderInfo["eco_count"]),
                                scrap_count = int.Parse(productionOrderInfo["scrap_count"]),
                                delivery_count = int.Parse(productionOrderInfo["delivery_count"]),
                                alert_count = int.Parse(productionOrderInfo["alert_count"]),
                                package_count = int.Parse(productionOrderInfo["package_count"]),
                                account_moves_count = int.Parse(productionOrderInfo["account_moves_count"]),
                                maintenance_count = int.Parse(productionOrderInfo["maintenance_count"]),
                                document_count = int.Parse(productionOrderInfo["document_count"]),
                                overview_progress = decimal.Parse(productionOrderInfo["overview_progress"]),
                                priority = int.Parse(productionOrderInfo["priority"]),
                                name = productionOrderInfo["name"],
                                use_create_components_lots = bool.Parse(productionOrderInfo["use_create_components_lots"]),
                                show_lot_ids = bool.Parse(productionOrderInfo["show_lot_ids"]),
                                product_tracking = productionOrderInfo["product_tracking"],
                                show_valuation = bool.Parse(productionOrderInfo["show_valuation"]),
                                product_id = product_id,
                                product_tmpl_id = product_tmpl_id,
                                forecasted_issue = bool.Parse(productionOrderInfo["forecasted_issue"]),
                                company_id = company_id,
                                product_description_variants = productionOrderInfo["product_description_variants"],
                                bom_id = bom_id,
                                qty_producing = qty_producing,
                                product_qty = int.Parse(productionOrderInfo["product_qty"]),
                                product_uom_category_id = product_uom_category_id,
                                product_uom_id = product_uom_id,
                                product_packaging_id = bool.Parse(productionOrderInfo["product_packaging_id"]),
                                lot_producing_id = bool.Parse(productionOrderInfo["lot_producing_id"]),
                                date_planned_start = productionOrderInfo["date_planned_start"],
                                delay_alert_date = bool.Parse(productionOrderInfo["delay_alert_date"]),
                                json_popover = bool.Parse(productionOrderInfo["json_popover"]),
                                components_availability_state = productionOrderInfo["components_availability_state"],
                                components_availability = productionOrderInfo["components_availability"],
                                show_final_lots = bool.Parse(productionOrderInfo["show_final_lots"]),
                                production_location_id = production_location_id,
                                move_finished_ids = move_finished_ids,
                                move_raw_ids = move_raw_ids, //danh sách consume
                                picking_type_id = picking_type_id,
                                location_src_id = location_src_id,
                                warehouse_id = warehouse_id,
                                location_dest_id = location_dest_id,
                                origin = productionOrderInfo["origin"],
                                date_deadline = productionOrderInfo["date_deadline"]
                            },

                            // Key onchange
                            "qty_producing",

                            // Object mapping key-value onchange
                            new Dictionary<string, object>
                            {
                                { "confirm_cancel", "" },
                                { "show_lock", "" },
                                { "move_byproduct_ids", "" },
                                { "state", "1" },
                                { "check_ids", "1" },
                                { "reservation_state", "1" },
                                { "date_planned_finished", "1" },
                                { "is_locked", "1" },
                                { "qty_produced", "1" },
                                { "is_planned", "1" },
                                { "workorder_ids", "1" },
                                { "workorder_ids.production_state", "1" },
                                { "workorder_ids.qty_producing", "1" },
                                { "priority", "1" },
                                { "product_id", "1" },
                                { "company_id", "1" },
                                { "bom_id", "1" },
                                { "qty_producing", "1" },
                                { "product_qty", "1" },
                                { "product_uom_id", "1" },
                                { "date_planned_start", "1" },
                                { "move_finished_ids", "1" },
                                { "move_finished_ids.product_id", "1" },
                                { "move_finished_ids.product_uom_qty", "1" },
                                { "move_finished_ids.product_uom", "1" },
                                { "move_finished_ids.operation_id", "1" },
                                { "move_finished_ids.date_deadline", "1" },
                                { "move_finished_ids.picking_type_id", "1" },
                                { "move_finished_ids.location_id", "1" },
                                { "move_finished_ids.group_id", "1" },
                                { "move_finished_ids.state", "1" },
                                { "move_finished_ids.quantity_done", "1" },
                                { "move_finished_ids.product_packaging_id", "1" },
                                { "move_raw_ids", "1" },
                                { "move_raw_ids.product_id", "1" },
                                { "move_raw_ids.location_id", "1" },
                                { "move_raw_ids.product_uom", "1" },
                                { "move_raw_ids.date_deadline", "1" },
                                { "move_raw_ids.date", "1" },
                                { "move_raw_ids.picking_type_id", "1" },
                                { "move_raw_ids.has_tracking", "1" },
                                { "move_raw_ids.operation_id", "1" },
                                { "move_raw_ids.state", "1" },
                                { "move_raw_ids.product_uom_qty", "1" },
                                { "move_raw_ids.product_qty", "1" },
                                { "move_raw_ids.reserved_availability", "1" },
                                { "move_raw_ids.forecast_expected_date", "1" },
                                { "move_raw_ids.forecast_availability", "1" },
                                { "move_raw_ids.quantity_done", "1" },
                                { "move_raw_ids.lot_ids", "1" },
                                { "move_raw_ids.group_id", "1" },
                                { "picking_type_id", "1" },
                                { "location_src_id", "1" },
                                { "location_dest_id", "1" }
                            }
                        },
                        model = "mrp.production",
                        method = "onchange",
                        kwargs = new
                        {
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = uid,
                                allowed_company_ids = new int[] { 1 },
                                default_company_id = 1
                            }
                        }
                    }
                };
                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );
                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/mrp.production/onchange", content);
                var responseString = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseString);
                if (json["error"] != null)
                {
                    throw new Exception(json["error"]["message"].ToString());
                }
                return json.ToObject<Dictionary<string, object>>();
            }
        }

        /// <summary>
        /// Hàm xử lý tiêu hao nguyên vật liệu theo BOM trong Odoo.
        /// Được viết 1 cách linh động hơn
        /// </summary>
        /// <param name="productionOrderInfo"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <param name="qty_producing"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<Dictionary<string, object>> ConsumeMaterialsByBOMAsyncv1(Dictionary<string, string> productionOrderInfo, int uid, string sessionId, 
                int qty_producing, int lot_id = 0)
        {
            using (var client = new HttpClient())
            {

                var str_move_ids = productionOrderInfo["move_raw_ids"].Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Replace("[", "").Replace("]", "").Split(",");
                object[] move_raw_ids = new object[] { };
                if (str_move_ids.Where(x => !string.IsNullOrWhiteSpace(x)).Count() > 0)
                {
                    // Chuyển đổi chuỗi ID thành mảng object
                    move_raw_ids = Array.ConvertAll(str_move_ids, int.Parse)
                        .Select(id => new object[] { 4, id, false })
                        .ToArray();
                }

                var str_check_ids = productionOrderInfo["check_ids"].Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Replace("[", "").Replace("]", "").Split(",");
                object[] check_ids = new object[] { };
                if (str_check_ids.Where(x => !string.IsNullOrWhiteSpace(x)).Count() > 0)
                {
                    // Chuyển đổi chuỗi ID thành mảng object
                    check_ids = Array.ConvertAll(str_check_ids, int.Parse)
                    .Select(id => new object[] { 4, id, false })
                    .ToArray();
                }
                
                var str_workorder_ids = productionOrderInfo["workorder_ids"].Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Replace("[", "").Replace("]", "").Split(",");
                object[] workorder_ids = new object[] { };
                if (str_workorder_ids.Where(x => !string.IsNullOrWhiteSpace(x)).Count() > 0)
                {
                    // Chuyển đổi chuỗi ID thành mảng object
                    workorder_ids = Array.ConvertAll(str_workorder_ids, int.Parse)
                        .Select(id => new object[] { 4, id, false })
                        .ToArray();
                }

                //Lấy product_id
                var arrProductID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["product_id"]);
                var product_id = Convert.ToInt32(arrProductID[0]);

                //Lấy product_tmpl_id
                var arrProductTmplID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["product_tmpl_id"]);
                var product_tmpl_id = Convert.ToInt32(arrProductTmplID[0]);

                //Lấy company_id
                var arrCompanyID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["company_id"]);
                var company_id = Convert.ToInt32(arrCompanyID[0]);

                //Lấy bom_id
                var arrBomID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["bom_id"]);
                var bom_id = Convert.ToInt32(arrBomID[0]);

                //Lấy product_uom_category_id
                var arrProductUomCatID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["product_uom_category_id"]);
                var product_uom_category_id = Convert.ToInt32(arrProductUomCatID[0]);

                //Lấy product_uom_id
                var arrProductUomID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["product_uom_id"]);
                var product_uom_id = Convert.ToInt32(arrProductUomID[0]);

                //Lấy move_finished_ids
                var arrMoveFinishedID = productionOrderInfo["move_finished_ids"].Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Replace("[", "").Replace("]", "").Split(",");
                object[] move_finished_ids = new object[] { };
                if(arrMoveFinishedID.Where(x => !string.IsNullOrWhiteSpace(x)).Count() > 0)
                {
                    // Chuyển đổi chuỗi ID thành mảng object
                    move_finished_ids = Array.ConvertAll(arrMoveFinishedID, int.Parse)
                        .Select(id => new object[] { 4, id, false })
                        .ToArray();
                }

                //Lấy production_location_id
                var arrProductLocID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["production_location_id"]);
                var production_location_id = Convert.ToInt32(arrProductLocID[0]);

                //Lấy picking_type_id
                var arrPickingTypeID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["picking_type_id"]);
                var picking_type_id = Convert.ToInt32(arrPickingTypeID[0]);

                //Lấy location_src_id
                var arrLocSrcID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["location_src_id"]);
                var location_src_id = Convert.ToInt32(arrLocSrcID[0]);

                //Lấy warehouse_id
                var arrWarehouseID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["warehouse_id"]);
                var warehouse_id = Convert.ToInt32(arrWarehouseID[0]);

                //Lấy location_dest_id
                var arrLocDescID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["location_dest_id"]);
                var location_dest_id = Convert.ToInt32(arrLocDescID[0]);

                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");

                // Gửi request tiêu thụ nguyên vật liệu
                var payload = new
                {
                    id = 127,
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        args = new object[]
                        {
                            new int[] { int.Parse(productionOrderInfo["id"]) }, // ID

                            // Object thông tin chi tiết
                            new
                            {
                                id = (object)productionOrderInfo["id"],
                                confirm_cancel = (object)productionOrderInfo["confirm_cancel"],
                                show_lock = (object)productionOrderInfo["show_lock"],
                                move_byproduct_ids = new object[] { },
                                state = (object)productionOrderInfo["state"],
                                show_serial_mass_produce = (productionOrderInfo["product_tracking"] == "serial") ? "False" :"True",
                                check_ids = check_ids,
                                check_todo = (object)productionOrderInfo["check_todo"],
                                reservation_state = (object)productionOrderInfo["reservation_state"],
                                date_planned_finished = (productionOrderInfo["date_planned_finished"] == "False") ? false :(object) productionOrderInfo["date_planned_finished"],
                                is_locked = (object)productionOrderInfo["is_locked"],
                                qty_produced = (object)productionOrderInfo["qty_produced"],
                                unreserve_visible = (object)productionOrderInfo["unreserve_visible"],
                                reserve_visible = (object)productionOrderInfo["reserve_visible"],
                                consumption = (object)productionOrderInfo["consumption"],
                                is_planned = (object)productionOrderInfo["is_planned"],
                                show_allocation = (object)productionOrderInfo["show_allocation"],
                                workorder_ids = workorder_ids,
                                eco_count = (object)productionOrderInfo["eco_count"],
                                scrap_count = (object)productionOrderInfo["scrap_count"],
                                delivery_count = (object)productionOrderInfo["delivery_count"],
                                alert_count = (object)productionOrderInfo["alert_count"],
                                package_count = (object)productionOrderInfo["package_count"],
                                account_moves_count = (object)productionOrderInfo["account_moves_count"],
                                maintenance_count = (object)productionOrderInfo["maintenance_count"],
                                document_count = (object)productionOrderInfo["document_count"],
                                overview_progress = (object)productionOrderInfo["overview_progress"],
                                priority = (object)productionOrderInfo["priority"],
                                name = (object)productionOrderInfo["name"],
                                use_create_components_lots = (object)productionOrderInfo["use_create_components_lots"],
                                show_lot_ids = (object)productionOrderInfo["show_lot_ids"],
                                product_tracking = (object)productionOrderInfo["product_tracking"],
                                show_valuation = (object)productionOrderInfo["show_valuation"],
                                product_id = product_id,
                                product_tmpl_id = product_tmpl_id,
                                forecasted_issue = (object)productionOrderInfo["forecasted_issue"],
                                company_id = company_id,
                                product_description_variants = (object)productionOrderInfo["product_description_variants"],
                                bom_id = bom_id,
                                qty_producing = qty_producing,
                                product_qty = (object)productionOrderInfo["product_qty"],
                                product_uom_category_id = product_uom_category_id,
                                product_uom_id = product_uom_id,
                                product_packaging_id = (object)productionOrderInfo["product_packaging_id"],
                                lot_producing_id = (lot_id == 0) ? false : (object)lot_id,
                                date_planned_start = (productionOrderInfo["date_planned_start"] == "False") ? false :(object) productionOrderInfo["date_planned_start"],
                                delay_alert_date = (productionOrderInfo["delay_alert_date"] == "False") ? false : (object)productionOrderInfo["delay_alert_date"],
                                json_popover = (object)productionOrderInfo["json_popover"],
                                components_availability_state = (object)productionOrderInfo["components_availability_state"],
                                components_availability = (object)productionOrderInfo["components_availability"],
                                show_final_lots = (object)productionOrderInfo["show_final_lots"],
                                production_location_id = production_location_id,
                                move_finished_ids = move_finished_ids,
                                move_raw_ids = move_raw_ids, //danh sách consume
                                picking_type_id = picking_type_id,
                                location_src_id = location_src_id,
                                warehouse_id = warehouse_id,
                                location_dest_id = location_dest_id,
                                origin = (object)productionOrderInfo["origin"],
                                date_deadline = (productionOrderInfo["date_deadline"] == "False") ? false : (object)productionOrderInfo["date_deadline"]
                            },

                            // Key onchange
                            "qty_producing",

                            // Object mapping key-value onchange
                            new Dictionary<string, object>
                            {
                                { "confirm_cancel", "" },
                                { "show_lock", "" },
                                { "move_byproduct_ids", "" },
                                { "state", "1" },
                                {"show_serial_mass_produce", "" },
                                { "check_ids", "1" },
                                { "check_todo", "" },
                                { "reservation_state", "1" },
                                { "date_planned_finished", "1" },
                                { "is_locked", "1" },
                                { "qty_produced", "1" },
                                { "is_planned", "1" },
                                { "workorder_ids", "1" },
                                { "workorder_ids.consumption", "" },
                                { "workorder_ids.company_id", "" },
                                { "workorder_ids.is_produced", "" },
                                { "workorder_ids.is_user_working", "" },
                                { "workorder_ids.product_uom_id", "" },
                                { "workorder_ids.production_state", "" },
                                { "workorder_ids.production_bom_id", "" },
                                { "workorder_ids.qty_producing", "1" },
                                { "workorder_ids.time_ids", "1" },
                                { "workorder_ids.working_state", "" },
                                { "workorder_ids.operation_id", "1" },
                                { "workorder_ids.name", "" },
                                { "workorder_ids.workcenter_id", "1" },
                                { "workorder_ids.product_id", "" },
                                { "workorder_ids.qty_remaining", "" },
                                { "workorder_ids.qty_produced", "1" },
                                { "workorder_ids.finished_lot_id", "1" },
                                { "workorder_ids.date_planned_start", "1" },
                                { "workorder_ids.date_planned_finished", "1" },
                                { "workorder_ids.date_start", "" },
                                { "workorder_ids.date_finished", "" },
                                { "workorder_ids.duration_expected", "1" },
                                { "workorder_ids.duration", "" },
                                { "workorder_ids.state", "1" },
                                { "workorder_ids.check_ids", "1" },
                                { "workorder_ids.check_todo", "" },
                                { "workorder_ids.show_json_popover", "" },
                                { "workorder_ids.json_popover", "" },
                                { "eco_count", "1" },
                                { "scrap_count", "1" },
                                { "delivery_count", "1" },
                                { "alert_count", "1" },
                                { "package_count", "1" },
                                { "account_moves_count", "1" },
                                { "maintenance_count", "1" },
                                { "document_count", "1" },
                                { "overview_progress", "" },
                                { "priority", "1" },
                                { "product_id", "1" },
                                { "company_id", "1" },
                                { "bom_id", "1" },
                                { "qty_producing", "1" },
                                { "product_qty", "1" },
                                { "product_uom_id", "1" },
                                { "date_planned_start", "1" },
                                { "move_finished_ids", "1" },
                                { "move_finished_ids.product_id", "1" },
                                { "move_finished_ids.product_uom_qty", "1" },
                                { "move_finished_ids.product_uom", "1" },
                                { "move_finished_ids.operation_id", "1" },
                                { "move_finished_ids.date_deadline", "1" },
                                { "move_finished_ids.picking_type_id", "1" },
                                { "move_finished_ids.location_id", "1" },
                                { "move_finished_ids.group_id", "1" },
                                { "move_finished_ids.state", "1" },
                                { "move_finished_ids.quantity_done", "1" },
                                { "move_finished_ids.product_packaging_id", "1" },
                                { "move_raw_ids", "1" },
                                { "move_raw_ids.product_id", "1" },
                                { "move_raw_ids.location_id", "1" },
                                { "move_raw_ids.product_uom", "1" },
                                { "move_raw_ids.date_deadline", "1" },
                                { "move_raw_ids.date", "1" },
                                { "move_raw_ids.picking_type_id", "1" },
                                { "move_raw_ids.has_tracking", "1" },
                                { "move_raw_ids.operation_id", "1" },
                                { "move_raw_ids.state", "1" },
                                { "move_raw_ids.product_uom_qty", "1" },
                                { "move_raw_ids.product_qty", "1" },
                                { "move_raw_ids.reserved_availability", "1" },
                                { "move_raw_ids.forecast_expected_date", "1" },
                                { "move_raw_ids.forecast_availability", "1" },
                                { "move_raw_ids.quantity_done", "1" },
                                { "move_raw_ids.lot_ids", "1" },
                                { "move_raw_ids.group_id", "1" },
                                { "picking_type_id", "1" },
                                { "location_src_id", "1" },
                                { "location_dest_id", "1" }
                            }
                        },
                        model = "mrp.production",
                        method = "onchange",
                        kwargs = new
                        {
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = uid,
                                allowed_company_ids = new int[] { 1 },
                                default_company_id = 1
                            }
                        }
                    }
                };

                var jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);

                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );
                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/mrp.production/onchange", content);
                var responseString = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseString);
                if (json["error"] != null)
                {
                    throw new Exception(json["error"]["message"].ToString());
                }
                return json.ToObject<Dictionary<string, object>>();
            }
        }

        /// <summary>
        /// Sử lí trường hợp cảnh báo tiêu thụ và không cảnh báo tiêu thụ
        /// Nếu có thế :))))
        /// </summary>
        /// <param name="productionOrderInfo"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <param name="qty_producing"></param>
        /// <param name="lot_id"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<Dictionary<string, object>> ConsumeMaterialsByBOMAsyncv2(Dictionary<string, string> productionOrderInfo, int uid, string sessionId,
                int qty_producing, int lot_id = 0, int tryCount = 0)
        {
            using (var client = new HttpClient())
            {
                var setting = new Dictionary<string, object>
                {
                    { "confirm_cancel", "" },
                    { "show_lock", "" },
                    { "move_byproduct_ids", "" },
                    { "state", "1" },
                    {"show_serial_mass_produce", "" },
                    { "check_ids", "1" },
                    { "check_todo", "" },
                    { "reservation_state", "1" },
                    { "date_planned_finished", "1" },
                    { "is_locked", "1" },
                    { "qty_produced", "1" },
                    { "is_planned", "1" },
                    { "workorder_ids", "1" },
                    { "workorder_ids.consumption", "" },
                    { "workorder_ids.company_id", "" },
                    { "workorder_ids.is_produced", "" },
                    { "workorder_ids.is_user_working", "" },
                    { "workorder_ids.product_uom_id", "" },
                    { "workorder_ids.production_state", "" },
                    { "workorder_ids.production_bom_id", "" },
                    { "workorder_ids.qty_producing", "1" },
                    { "workorder_ids.time_ids", "1" },
                    { "workorder_ids.working_state", "" },
                    { "workorder_ids.operation_id", "1" },
                    { "workorder_ids.name", "" },
                    { "workorder_ids.workcenter_id", "1" },
                    { "workorder_ids.product_id", "" },
                    { "workorder_ids.qty_remaining", "" },
                    { "workorder_ids.qty_produced", "1" },
                    { "workorder_ids.finished_lot_id", "1" },
                    { "workorder_ids.date_planned_start", "1" },
                    { "workorder_ids.date_planned_finished", "1" },
                    { "workorder_ids.date_start", "" },
                    { "workorder_ids.date_finished", "" },
                    { "workorder_ids.duration_expected", "1" },
                    { "workorder_ids.duration", "" },
                    { "workorder_ids.state", "1" },
                    { "workorder_ids.check_ids", "1" },
                    { "workorder_ids.check_todo", "" },
                    { "workorder_ids.show_json_popover", "" },
                    { "workorder_ids.json_popover", "" },
                    { "eco_count", "1" },
                    { "scrap_count", "1" },
                    { "delivery_count", "1" },
                    { "alert_count", "1" },
                    { "package_count", "1" },
                    { "account_moves_count", "1" },
                    { "maintenance_count", "1" },
                    { "document_count", "1" },
                    { "overview_progress", "" },
                    { "priority", "1" },
                    { "product_id", "1" },
                    { "company_id", "1" },
                    { "bom_id", "1" },
                    { "qty_producing", "1" },
                    { "product_qty", "1" },
                    { "product_uom_id", "1" },
                    { "date_planned_start", "1" },
                    { "move_finished_ids", "1" },
                    { "move_finished_ids.product_id", "1" },
                    { "move_finished_ids.product_uom_qty", "1" },
                    { "move_finished_ids.product_uom", "1" },
                    { "move_finished_ids.operation_id", "1" },
                    { "move_finished_ids.date_deadline", "1" },
                    { "move_finished_ids.picking_type_id", "1" },
                    { "move_finished_ids.location_id", "1" },
                    { "move_finished_ids.group_id", "1" },
                    { "move_finished_ids.state", "1" },
                    { "move_finished_ids.quantity_done", "1" },
                    { "move_finished_ids.product_packaging_id", "1" },
                    { "move_raw_ids", "1" },
                    { "move_raw_ids.product_id", "1" },
                    { "move_raw_ids.location_id", "1" },
                    { "move_raw_ids.product_uom", "1" },
                    { "move_raw_ids.date_deadline", "1" },
                    { "move_raw_ids.date", "1" },
                    { "move_raw_ids.picking_type_id", "1" },
                    { "move_raw_ids.has_tracking", "1" },
                    { "move_raw_ids.operation_id", "1" },
                    { "move_raw_ids.state", "1" },
                    { "move_raw_ids.product_uom_qty", "1" },
                    { "move_raw_ids.product_qty", "1" },
                    { "move_raw_ids.reserved_availability", "1" },
                    { "move_raw_ids.forecast_expected_date", "1" },
                    { "move_raw_ids.forecast_availability", "1" },
                    { "move_raw_ids.quantity_done", "1" },
                    { "move_raw_ids.lot_ids", "1" },
                    { "move_raw_ids.group_id", "1" },
                    { "picking_type_id", "1" },
                    { "location_src_id", "1" },
                    { "location_dest_id", "1" }
                };

                string onchangeField = "qty_producing";
                if (productionOrderInfo["product_tracking"] == "serial" && (tryCount == 0 || tryCount == 2))
                {
                    productionOrderInfo["state"] = "progress";
                    onchangeField = "lot_producing_id";
                    qty_producing = 0;
                    setting = new Dictionary<string, object>
                    {
                        { "confirm_cancel", "" },
                        { "show_lock", "" },
                        { "move_byproduct_ids", "" },
                        { "state", "1" },
                        { "show_serial_mass_produce", "" },
                        { "check_ids", "1" },
                        { "check_todo", "" },
                        { "reservation_state", "1" },
                        { "date_planned_finished", "1" },
                        { "is_locked", "1" },
                        { "qty_produced", "1" },
                        { "is_planned", "1" },
                        { "workorder_ids", "1" },
                        { "workorder_ids.consumption", "" },
                        { "workorder_ids.company_id", "" },
                        { "workorder_ids.is_produced", "" },
                        { "workorder_ids.is_user_working", "" },
                        { "workorder_ids.product_uom_id", "" },
                        { "workorder_ids.production_state", "1" },
                        { "workorder_ids.production_bom_id", "" },
                        { "workorder_ids.qty_producing", "1" },
                        { "workorder_ids.time_ids", "1" },
                        { "workorder_ids.working_state", "" },
                        { "workorder_ids.operation_id", "1" },
                        { "workorder_ids.name", "" },
                        { "workorder_ids.workcenter_id", "1" },
                        { "workorder_ids.product_id", "" },
                        { "workorder_ids.qty_remaining", "" },
                        { "workorder_ids.qty_produced", "1" },
                        { "workorder_ids.finished_lot_id", "1" },
                        { "workorder_ids.date_planned_start", "1" },
                        { "workorder_ids.date_planned_finished", "1" },
                        { "workorder_ids.date_start", "" },
                        { "workorder_ids.date_finished", "" },
                        { "workorder_ids.duration_expected", "1" },
                        { "workorder_ids.duration", "" },
                        { "workorder_ids.state", "1" },
                        { "workorder_ids.check_ids", "1" },
                        { "workorder_ids.check_todo", "" },
                        { "workorder_ids.show_json_popover", "" },
                        { "workorder_ids.json_popover", "" },
                        { "eco_count", "" },
                        { "scrap_count", "" },
                        { "delivery_count", "" },
                        { "alert_count", "" },
                        { "package_count", "" },
                        { "account_moves_count", "" },
                        { "maintenance_count", "" },
                        { "document_count", "" },
                        { "overview_progress", "" },
                        { "priority", "1" },
                        { "name", "" },
                        { "id", "" },
                        { "use_create_components_lots", "" },
                        { "show_lot_ids", "" },
                        { "product_tracking", "" },
                        { "show_valuation", "" },
                        { "product_id", "1" },
                        { "product_tmpl_id", "" },
                        { "forecasted_issue", "" },
                        { "company_id", "1" },
                        { "product_description_variants", "" },
                        { "bom_id", "1" },
                        { "qty_producing", "1" },
                        { "product_qty", "1" },
                        { "product_uom_category_id", "" },
                        { "product_uom_id", "1" },
                        { "product_packaging_id", "1" },
                        { "lot_producing_id", "1" },
                        { "date_planned_start", "1" },
                        { "delay_alert_date", "" },
                        { "json_popover", "" },
                        { "components_availability_state", "" },
                        { "components_availability", "" },
                        { "date_deadline", "" },
                        { "show_final_lots", "" },
                        { "production_location_id", "" },
                        { "move_finished_ids", "1" },
                        { "move_finished_ids.product_id", "1" },
                        { "move_finished_ids.product_uom_qty", "1" },
                        { "move_finished_ids.product_uom", "1" },
                        { "move_finished_ids.operation_id", "1" },
                        { "move_finished_ids.byproduct_id", "" },
                        { "move_finished_ids.name", "" },
                        { "move_finished_ids.date_deadline", "1" },
                        { "move_finished_ids.picking_type_id", "1" },
                        { "move_finished_ids.location_id", "1" },
                        { "move_finished_ids.location_dest_id", "" },
                        { "move_finished_ids.company_id", "" },
                        { "move_finished_ids.warehouse_id", "" },
                        { "move_finished_ids.origin", "" },
                        { "move_finished_ids.group_id", "1" },
                        { "move_finished_ids.propagate_cancel", "" },
                        { "move_finished_ids.move_dest_ids", "" },
                        { "move_finished_ids.state", "1" },
                        { "move_finished_ids.product_uom_category_id", "" },
                        { "move_finished_ids.allowed_operation_ids", "" },
                        { "move_finished_ids.quantity_done", "1" },
                        { "move_finished_ids.product_packaging_id", "1" },
                        { "move_finished_ids.cost_share", "" },
                        { "move_raw_ids", "1" },
                        { "move_raw_ids.product_id", "1" },
                        { "move_raw_ids.location_id", "1" },
                        { "move_raw_ids.product_uom", "1" },
                        { "move_raw_ids.propagate_cancel", "" },
                        { "move_raw_ids.price_unit", "" },
                        { "move_raw_ids.company_id", "" },
                        { "move_raw_ids.product_uom_category_id", "" },
                        { "move_raw_ids.name", "" },
                        { "move_raw_ids.allowed_operation_ids", "" },
                        { "move_raw_ids.unit_factor", "" },
                        { "move_raw_ids.date_deadline", "1" },
                        { "move_raw_ids.date", "1" },
                        { "move_raw_ids.additional", "" },
                        { "move_raw_ids.picking_type_id", "1" },
                        { "move_raw_ids.has_tracking", "1" },
                        { "move_raw_ids.operation_id", "1" },
                        { "move_raw_ids.is_done", "" },
                        { "move_raw_ids.bom_line_id", "" },
                        { "move_raw_ids.sequence", "" },
                        { "move_raw_ids.warehouse_id", "" },
                        { "move_raw_ids.is_locked", "" },
                        { "move_raw_ids.move_lines_count", "" },
                        { "move_raw_ids.location_dest_id", "" },
                        { "move_raw_ids.state", "1" },
                        { "move_raw_ids.should_consume_qty", "" },
                        { "move_raw_ids.product_uom_qty", "1" },
                        { "move_raw_ids.product_type", "" },
                        { "move_raw_ids.product_qty", "1" },
                        { "move_raw_ids.reserved_availability", "1" },
                        { "move_raw_ids.forecast_expected_date", "1" },
                        { "move_raw_ids.forecast_availability", "1" },
                        { "move_raw_ids.quantity_done", "1" },
                        { "move_raw_ids.component_standard_consumption_id", "" },
                        { "move_raw_ids.manual_consumption", "" },
                        { "move_raw_ids.show_details_visible", "" },
                        { "move_raw_ids.lot_ids", "1" },
                        { "move_raw_ids.group_id", "1" },
                        { "picking_type_id", "1" },
                        { "location_src_id", "1" },
                        { "warehouse_id", "" },
                        { "location_dest_id", "1" },
                        { "origin", "" }
                    };
                }

                var str_move_ids = productionOrderInfo["move_raw_ids"].Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Replace("[", "").Replace("]", "").Split(",");
                object[] move_raw_ids = new object[] { };
                if (str_move_ids.Where(x => !string.IsNullOrWhiteSpace(x)).Count() > 0)
                {
                    // Chuyển đổi chuỗi ID thành mảng object
                    move_raw_ids = Array.ConvertAll(str_move_ids, int.Parse)
                        .Select(id => new object[] { 4, id, false })
                        .ToArray();
                }

                var str_check_ids = productionOrderInfo["check_ids"].Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Replace("[", "").Replace("]", "").Split(",");
                object[] check_ids = new object[] { };
                if (str_check_ids.Where(x => !string.IsNullOrWhiteSpace(x)).Count() > 0)
                {
                    // Chuyển đổi chuỗi ID thành mảng object
                    check_ids = Array.ConvertAll(str_check_ids, int.Parse)
                    .Select(id => new object[] { 4, id, false })
                    .ToArray();
                }

                var str_workorder_ids = productionOrderInfo["workorder_ids"].Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Replace("[", "").Replace("]", "").Split(",");
                object[] workorder_ids = new object[] { };
                if (str_workorder_ids.Where(x => !string.IsNullOrWhiteSpace(x)).Count() > 0)
                {
                    // Chuyển đổi chuỗi ID thành mảng object
                    workorder_ids = Array.ConvertAll(str_workorder_ids, int.Parse)
                        .Select(id => new object[] { 4, id, false })
                        .ToArray();
                }

                //Lấy product_id
                var arrProductID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["product_id"]);
                var product_id = Convert.ToInt32(arrProductID[0]);

                //Lấy product_tmpl_id
                var arrProductTmplID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["product_tmpl_id"]);
                var product_tmpl_id = Convert.ToInt32(arrProductTmplID[0]);

                //Lấy company_id
                var arrCompanyID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["company_id"]);
                var company_id = Convert.ToInt32(arrCompanyID[0]);

                //Lấy bom_id
                var arrBomID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["bom_id"]);
                var bom_id = Convert.ToInt32(arrBomID[0]);

                //Lấy product_uom_category_id
                var arrProductUomCatID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["product_uom_category_id"]);
                var product_uom_category_id = Convert.ToInt32(arrProductUomCatID[0]);

                //Lấy product_uom_id
                var arrProductUomID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["product_uom_id"]);
                var product_uom_id = Convert.ToInt32(arrProductUomID[0]);

                //Lấy move_finished_ids
                var arrMoveFinishedID = productionOrderInfo["move_finished_ids"].Replace("[\r\n  ", "").Replace("\r\n  ", "").Replace("\r\n]", "").Replace("[", "").Replace("]", "").Split(",");
                object[] move_finished_ids = new object[] { };
                if (arrMoveFinishedID.Where(x => !string.IsNullOrWhiteSpace(x)).Count() > 0)
                {
                    // Chuyển đổi chuỗi ID thành mảng object
                    move_finished_ids = Array.ConvertAll(arrMoveFinishedID, int.Parse)
                        .Select(id => new object[] { 4, id, false })
                        .ToArray();
                }

                //Lấy production_location_id
                var arrProductLocID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["production_location_id"]);
                var production_location_id = Convert.ToInt32(arrProductLocID[0]);

                //Lấy picking_type_id
                var arrPickingTypeID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["picking_type_id"]);
                var picking_type_id = Convert.ToInt32(arrPickingTypeID[0]);

                //Lấy location_src_id
                var arrLocSrcID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["location_src_id"]);
                var location_src_id = Convert.ToInt32(arrLocSrcID[0]);

                //Lấy warehouse_id
                var arrWarehouseID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["warehouse_id"]);
                var warehouse_id = Convert.ToInt32(arrWarehouseID[0]);

                //Lấy location_dest_id
                var arrLocDescID = JsonConvert.DeserializeObject<object[]>(productionOrderInfo["location_dest_id"]);
                var location_dest_id = Convert.ToInt32(arrLocDescID[0]);

                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");

                // Gửi request tiêu thụ nguyên vật liệu
                var payload = new
                {
                    id = 81,
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        args = new object[]
                        {
                            // 1️⃣ ID của MO
                            new int[] { int.Parse(productionOrderInfo["id"]) },

                            // 2️⃣ Toàn bộ thông tin chi tiết MO
                            new
                            {
                                id = (object)productionOrderInfo["id"],
                                confirm_cancel = (productionOrderInfo["confirm_cancel"] == "False") ? false : true,
                                show_lock = (productionOrderInfo["show_lock"] == "False") ? false : true,
                                move_byproduct_ids = new object[] { },
                                state = (object)productionOrderInfo["state"],
                                show_serial_mass_produce = (productionOrderInfo["product_tracking"] == "serial") ? false :true,
                                check_ids = check_ids,
                                check_todo = (productionOrderInfo["check_todo"] == "False") ? false : true,
                                reservation_state = (object)productionOrderInfo["reservation_state"],
                                date_planned_finished = (productionOrderInfo["date_planned_finished"] == "False") ? false :(object) productionOrderInfo["date_planned_finished"],
                                is_locked = (productionOrderInfo["is_locked"] == "False") ? false : true,
                                qty_produced = (object)productionOrderInfo["qty_produced"],
                                unreserve_visible = (productionOrderInfo["unreserve_visible"] == "False") ? false : true,
                                reserve_visible = (productionOrderInfo["reserve_visible"] == "False") ? false : true,
                                consumption = (object)productionOrderInfo["consumption"],
                                is_planned = (productionOrderInfo["is_planned"] == "False") ? false : true,
                                show_allocation = (productionOrderInfo["show_allocation"] == "False") ? false : true,
                                workorder_ids = workorder_ids,
                                eco_count = (object)productionOrderInfo["eco_count"],
                                scrap_count = (object)productionOrderInfo["scrap_count"],
                                delivery_count = (object)productionOrderInfo["delivery_count"],
                                alert_count = (object)productionOrderInfo["alert_count"],
                                package_count = (object)productionOrderInfo["package_count"],
                                account_moves_count = (object)productionOrderInfo["account_moves_count"],
                                maintenance_count = (object)productionOrderInfo["maintenance_count"],
                                document_count = (object)productionOrderInfo["document_count"],
                                overview_progress = (object)productionOrderInfo["overview_progress"],
                                priority = (object)productionOrderInfo["priority"],
                                name = (object)productionOrderInfo["name"],
                                use_create_components_lots = (productionOrderInfo["use_create_components_lots"] == "False") ? false : true,
                                show_lot_ids = (productionOrderInfo["show_lot_ids"] == "False") ? false : true,
                                product_tracking = (object)productionOrderInfo["product_tracking"],
                                show_valuation = (productionOrderInfo["show_valuation"] == "False") ? false : true,
                                product_id = product_id,
                                product_tmpl_id = product_tmpl_id,
                                forecasted_issue = (productionOrderInfo["forecasted_issue"] == "False") ? false : true,
                                company_id = company_id,
                                product_description_variants = (object)productionOrderInfo["product_description_variants"],
                                bom_id = bom_id,
                                qty_producing = qty_producing,
                                product_qty = (object)productionOrderInfo["product_qty"],
                                product_uom_category_id = product_uom_category_id,
                                product_uom_id = product_uom_id,
                                product_packaging_id = (productionOrderInfo["product_packaging_id"] == "False") ? false :(object) productionOrderInfo["product_packaging_id"],
                                lot_producing_id = (lot_id == 0) ? false : (object)lot_id,
                                date_planned_start = (productionOrderInfo["date_planned_start"] == "False") ? false :(object) productionOrderInfo["date_planned_start"],
                                delay_alert_date = (productionOrderInfo["delay_alert_date"] == "False") ? false : (object)productionOrderInfo["delay_alert_date"],
                                json_popover = (object)productionOrderInfo["json_popover"],
                                components_availability_state = (object)productionOrderInfo["components_availability_state"],
                                components_availability = (object)productionOrderInfo["components_availability"],
                                date_deadline = (productionOrderInfo["date_deadline"] == "False") ? false : (object)productionOrderInfo["date_deadline"],
                                show_final_lots = (productionOrderInfo["show_final_lots"] == "False") ? false : true,
                                production_location_id = production_location_id,
                                move_finished_ids = move_finished_ids,
                                move_raw_ids = move_raw_ids,
                                picking_type_id = picking_type_id,
                                location_src_id = location_src_id,
                                warehouse_id = warehouse_id,
                                location_dest_id = location_dest_id,
                                origin = (object)productionOrderInfo["origin"]
                            },

                            // 3️⃣ Field onchange
                            onchangeField,
                            setting
                            // 4️⃣ Mapping onchange fields
                            
                        },
                        model = "mrp.production",
                        method = "onchange",
                        kwargs = new
                        {
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = 2,
                                allowed_company_ids = new int[] { 1 },
                                active_model = "mrp.production",
                                force_skip_consumption = true,
                                button_mark_done_production_ids = (object?)null,
                                //active_id = 88703,
                                //active_ids = new int[] { 88703 },
                                skip_backorder = false,
                                mo_ids_to_backorder = (object?)null,
                                default_product_id = product_id,
                                default_company_id = 1
                            }
                        }
                    }
                };

                var jsonPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);

                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );
                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/mrp.production/onchange", content);
                var responseString = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseString);
                if (json["error"] != null)
                {
                    throw new Exception(json["error"]["message"].ToString());
                }
                return json.ToObject<Dictionary<string, object>>();
            }
        }

        /// <summary>
        /// Hàm API để đọc thông tin sản xuất bằng mã productID từ Odoo.
        /// </summary>
        /// <param name="productionId"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, object>> SaveProductionOrderAsync(int id, int qty_producing, object[] move_raw_ids, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");
                var payload = new
                {
                    id = 58,
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        args = new object[]
                        {
                            new int[] { id }, // ID của mrp.production
                            new
                            {
                                qty_producing = qty_producing,
                                move_raw_ids = move_raw_ids
                            }
                        },
                        model = "mrp.production",
                        method = "write",
                        kwargs = new
                        {
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = uid,
                                allowed_company_ids = new int[] { 1 },
                                default_company_id = 1
                            }
                        }
                    }
                };


                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/mrp.production/write", content);
                var responseString = await response.Content.ReadAsStringAsync();

                var json = JObject.Parse(responseString);

                if (json["error"] != null)
                {
                    throw new Exception(json["error"]["message"].ToString());
                }
                return json.ToObject<Dictionary<string, object>>();
            }
        }

        /// <summary>
        /// Hàm API để đọc thông tin sản xuất bằng mã productID từ Odoo.
        /// </summary>
        /// <param name="productionId"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, object>> SaveProductionOrderAsyncv1(int id, int lot_producing_id, int qty_producing, object[] move_raw_ids, object[] workorder_ids, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");

                var data = new Dictionary<string, object>
                {
                    { "qty_producing", qty_producing },
                    { "move_raw_ids", move_raw_ids }
                };

                if (lot_producing_id != 0)
                {
                    data["lot_producing_id"] = lot_producing_id;

                    data["workorder_ids"] = workorder_ids;
                }

                var payload = new
                {
                    id = 58,
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        args = new object[]
                        {
                            new int[] { id }, // ID của mrp.production
                            data
                        },
                        model = "mrp.production",
                        method = "write",
                        kwargs = new
                        {
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = uid,
                                allowed_company_ids = new int[] { 1 },
                                default_company_id = 1
                            }
                        }
                    }
                };


                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/mrp.production/write", content);
                var responseString = await response.Content.ReadAsStringAsync();

                var json = JObject.Parse(responseString);

                if (json["error"] != null)
                {
                    throw new Exception(json["error"]["message"].ToString());
                }
                return json.ToObject<Dictionary<string, object>>();
            }
        }

        /// <summary>
        /// Hàm API để đọc thông tin sản xuất bằng mã productID từ Odoo.
        /// </summary>
        /// <param name="productionId"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, object>> MarkDoneProductionOrderAsync(int id, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");
                var payload = new
                {
                    id = 138,
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        args = new object[]
                        {
                            new int[] { id } // ID của mrp.production
                        },
                        kwargs = new
                        {
                            context = new
                            {
                                default_company_id = 1,
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = uid,
                                allowed_company_ids = new int[] { 1 },
                                produce_all = true
                            }
                        },
                        method = "button_mark_done", // sửa key "method " -> "method"
                        model = "mrp.production"
                    }
                };


                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_button", content);
                var responseString = await response.Content.ReadAsStringAsync();

                var json = JObject.Parse(responseString);

                if (json["error"] != null)
                {
                    throw new Exception(json["error"]["message"].ToString());
                }
                return json.ToObject<Dictionary<string, object>>();
            }
        }

        /// <summary>
        /// Hàm API để xử lý backorder trong Odoo.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<Dictionary<string, object>> BackOrderOnchange(int id, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");
                var payload = new
                {
                    id = 142,
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        args = new object[]
                        {
                            new object[] { }, // args[0] = mảng trống
                            new { },          // args[1] = object trống
                            new object[] { }, // args[2] = mảng trống
                            // args[3] = object mapping onchange fields
                            new Dictionary<string, object>
                            {
                                { "show_backorder_lines", "" },
                                { "mrp_production_backorder_line_ids", "1" },
                                { "mrp_production_backorder_line_ids.mrp_production_id", "" },
                                { "mrp_production_backorder_line_ids.to_backorder", "" }
                            }
                        },
                        model = "mrp.production.backorder",
                        method = "onchange",
                        kwargs = new
                        {
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = uid,
                                allowed_company_ids = new int[] { 1 },
                                active_model = "mrp.production",
                                active_id = id,
                                active_ids = new int[] { id },
                                default_company_id = 1,
                                force_skip_consumption = true,
                                button_mark_done_production_ids = new int[] { id },
                                default_mrp_production_ids = new int[] { id },
                                default_mrp_production_backorder_line_ids = new object[]
                                {
                                    new object[]
                                    {
                                        0,
                                        0,
                                        new
                                        {
                                            mrp_production_id = id,
                                            to_backorder = true
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/mrp.production.backorder/onchange", content);
                var responseString = await response.Content.ReadAsStringAsync();

                var json = JObject.Parse(responseString);

                if (json["error"] != null)
                {
                    throw new Exception(json["error"]["message"].ToString());
                }
                return json.ToObject<Dictionary<string, object>>();
            }
        }

        /// <summary>
        /// Hàm API để xử lý tạo backorder trong Odoo.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<int> BackOrderCreate(int id, int uid, string sessionId, int lot_id = 0)
        {
            using (var client = new HttpClient())
            {
                string @virtual = "virtual_6"; // Giá trị này có thể thay đổi tùy theo yêu cầu của bạn
                if (lot_id != 0)
                {
                    @virtual = "virtual_4";
                }
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");
                var payload = new
                {
                    id = 147,
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        args = new object[]
                        {
                            new
                            {
                                mrp_production_backorder_line_ids = new object[]
                                {
                                    new object[]
                                    {
                                        0,
                                        @virtual,
                                        new
                                        {
                                            mrp_production_id = id,
                                            to_backorder = true
                                        }
                                    }
                                }
                            }
                        },
                        model = "mrp.production.backorder",
                        method = "create",
                        kwargs = new
                        {
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = uid,
                                allowed_company_ids = new int[] { 1 },
                                active_model = "mrp.production",
                                active_id = id,
                                active_ids = new int[] { id },
                                default_company_id = 1,
                                force_skip_consumption = true,
                                button_mark_done_production_ids = new int[] { id },
                                default_mrp_production_ids = new int[] { id },
                                default_mrp_production_backorder_line_ids = new object[]
                                {
                                    new object[]
                                    {
                                        0,
                                        0,
                                        new
                                        {
                                            mrp_production_id = id,
                                            to_backorder = true
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/mrp.production.backorder/create", content);
                var responseString = await response.Content.ReadAsStringAsync();

                var json = JObject.Parse(responseString);

                var backorder_id = int.Parse(json["result"].ToString());
                return backorder_id;
            }
        }

        /// <summary>
        /// Hàm API để xử lý tạo backorder trong Odoo.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<Dictionary<string, object>> BackOrderAction(int id, int backorder_id, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");
                var payload = new
                {
                    id = 150,
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        args = new object[]
                        {
                            new int[] { backorder_id } // ID của bản ghi backorder vừa tạo
                        },
                        kwargs = new
                        {
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = uid,
                                allowed_company_ids = new int[] { 1 },
                                active_model = "mrp.production",
                                active_id = id,
                                active_ids = new int[] { id },
                                default_company_id = 1,
                                force_skip_consumption = true,
                                button_mark_done_production_ids = new int[] { id },
                                default_mrp_production_ids = new int[] { id },
                                default_mrp_production_backorder_line_ids = new object[]
                                {
                                    new object[]
                                    {
                                        0,
                                        0,
                                        new
                                        {
                                            mrp_production_id = id,
                                            to_backorder = true
                                        }
                                    }
                                }
                            }
                        },
                        method = "action_backorder",
                        model = "mrp.production.backorder"
                    }
                };
                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_button", content);
                var responseString = await response.Content.ReadAsStringAsync();

                var json = JObject.Parse(responseString);

                if (json["error"] != null)
                {
                    throw new Exception(json["error"]["message"].ToString());
                }
                return json.ToObject<Dictionary<string, object>>();
            }
        }

        /// <summary>
        /// Lấy thông tin của stockMoveLine đầu tiên trong danh sách
        /// Bước thực hiện tiêu hao thành phần của lSX quản lý theo mã serial
        /// </summary>
        /// <param name="id"></param>
        /// <param name="stockMoveInfo"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, string>> GetStockMoveLineByID(int id, int mo_id, Dictionary<string, object> stockMoveInfo, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                int move_id = int.Parse(stockMoveInfo["id"].ToString());

                var arrProductUomID = JsonConvert.DeserializeObject<object[]>(stockMoveInfo["product_uom"].ToString());
                var product_uom_id = Convert.ToInt32(arrProductUomID[0]);

                var arrProductID = JsonConvert.DeserializeObject<object[]>(stockMoveInfo["product_id"].ToString());
                var product_id = Convert.ToInt32(arrProductID[0]);

                var arrLocationID = JsonConvert.DeserializeObject<object[]>(stockMoveInfo["location_id"].ToString());
                var location_id = Convert.ToInt32(arrLocationID[0]);

                var arrLocationDestID = JsonConvert.DeserializeObject<object[]>(stockMoveInfo["location_dest_id"].ToString());
                var location_dest_id = Convert.ToInt32(arrLocationDestID[0]);

                var arrCompanyID = JsonConvert.DeserializeObject<object[]>(stockMoveInfo["company_id"].ToString());
                var company_id = Convert.ToInt32(arrCompanyID[0]);

                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");

                var payload = new
                {
                    id = 127,
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        args = new object[]
                        {
                            new int[]
                            {
                                id
                            },
                            new string[]
                            {
                                "company_id","picking_id","move_id","product_uom_category_id","product_id",
                                "package_level_id","location_id","location_dest_id","package_id","result_package_id",
                                "lot_id","lot_name","can_create_equipment","reserved_uom_qty","state","is_locked",
                                "picking_code","qty_done","product_uom_id"
                            }
                        },
                        model = "stock.move.line",
                        method = "read",
                        kwargs = new
                        {
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = uid,
                                allowed_company_ids = new int[] { company_id },
                                active_model = "stock.move",
                                active_id = move_id,
                                active_ids = new int[] { move_id },
                                show_owner = true,
                                show_lots_m2o = true,
                                show_lots_text = false,
                                show_source_location = true,
                                show_destination_location = false,
                                show_package = true,
                                show_reserved_quantity = true,
                                force_manual_consumption = true,
                                active_mo_id = mo_id,
                                tree_view_ref = "stock.view_stock_move_line_operation_tree",
                                form_view_ref = "stock.view_move_line_mobile_form",
                                default_product_uom_id = product_uom_id,
                                //default_picking_id = false,
                                default_move_id = move_id,
                                default_product_id = product_id,
                                default_location_id = location_id,
                                default_location_dest_id = location_dest_id,
                                default_company_id = company_id
                            }
                        }
                    }
                };

                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );
                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/stock.move.line/read", content);
                var responseString = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseString);

                var resultArray = (JArray)json["result"];

                try
                {
                    var dictionary = ((JObject)resultArray[0])
                         .Properties()
                         .ToDictionary(p => p.Name, p => p.Value.ToString());

                    return dictionary;
                }
                catch (Exception ex)
                {
                    return null;
                }
            }
        }

        public async Task<int> GetLotInfo(string lot_name, int mo_id, Dictionary<string, object> stockMoveInfo, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                var arrProductID = JsonConvert.DeserializeObject<object[]>(stockMoveInfo["product_id"].ToString());
                var product_id = Convert.ToInt32(arrProductID[0]);

                var arrCompanyID = JsonConvert.DeserializeObject<object[]>(stockMoveInfo["company_id"].ToString());
                var company_id = Convert.ToInt32(arrCompanyID[0]);

                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");

                var payload = new
                {
                    id = 130,
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        model = "stock.lot",
                        method = "name_search",
                        args = new object[] { },
                        kwargs = new
                        {
                            name = lot_name,
                            @operator = "ilike",
                            args = new object[]
                            {
                                "&",
                                new object[] { "product_id", "=", product_id },
                                new object[] { "company_id", "=", company_id }
                            },
                            limit = 8,
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = uid,
                                allowed_company_ids = new int[] { company_id },
                                active_mo_id = mo_id,
                                //active_picking_id = false,
                                default_company_id = company_id,
                                default_product_id = product_id
                            }
                        }
                    }
                };

                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );
                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/stock.lot/name_search", content);
                var responseString = await response.Content.ReadAsStringAsync();
                var json = await response.Content.ReadAsStringAsync();
                //var resultArray = (JArray)json["result"];
                try
                {
                    dynamic data = JsonConvert.DeserializeObject(json);
                    int lotId = data.result[0][0];
                    string lotName = data.result[0][1];

                    return lotId;
                }
                catch (Exception ex)
                {
                    return 0;
                }
            }
        }
        #endregion

        #region Nhân viên

        /// <summary>
        /// Lấy ra danh sách mã nhân viên
        /// </summary>
        /// <returns></returns>
        public async Task<List<Dictionary<string, string>>> GetEmployeeCategory(int uid, string sessionId)
        {
            List<Dictionary<string, string>> dictionaries = new List<Dictionary<string, string>>();
            using (var client = new HttpClient())
            {
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");
                var payload = new
                {
                    jsonrpc = "2.0",
                    method = "call",
                    id = 101,
                    @params = new
                    {
                        model = "hr.employee.category",
                        method = "search_read",
                        args = new object[]
                        {
                            new object[] { }, // domain rỗng => lấy tất cả
                            new string[] { "id", "display_name" }
                        },
                        kwargs = new
                        {
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = uid,
                                allowed_company_ids = new int[] { 1 }
                            }
                        }
                    }
                };

                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/hr.employee.category/search_read", content);
                var responseString = await response.Content.ReadAsStringAsync();

                var json = JObject.Parse(responseString);

                var resultArray = (JArray)json["result"];
                foreach (var item in resultArray)
                {
                    var dictionary = ((JObject)item)
                     .Properties()
                     .ToDictionary(p => p.Name, p => p.Value.ToString());
                    dictionaries.Add(dictionary);
                }

                return dictionaries;
            }
        }

        /// <summary>
        /// Lấy ra danh sách nhân viên
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public async Task<List<Dictionary<string, string>>> GetEmployeeInfomation(int uid, string sessionId)
        {
            List<Dictionary<string, string>> dictionaries = new List<Dictionary<string, string>>();
            using (var client = new HttpClient())
            {
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");
                var payload = new
                {
                    id = 11,
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        model = "hr.employee",
                        method = "web_search_read",
                        args = new object[] { },
                        kwargs = new
                        {
                            limit = 0,
                            offset = 0,
                            order = "",
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = 2,
                                allowed_company_ids = new int[] { 1 },
                                bin_size = true,
                                chat_icon = true
                            },
                            count_limit = 10001,
                            domain = new object[] { },
                            fields = new string[]
                            {
                                "id", "name", "work_phone", "work_email", "job_title", "category_ids"
                            }
                        }
                    }
                };

                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );

                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/hr.employee.category/search_read", content);
                var responseString = await response.Content.ReadAsStringAsync();

                var json = JObject.Parse(responseString);

                var resultArray = (JArray)json["result"]["records"];
                foreach (var item in resultArray)
                {
                    var dictionary = ((JObject)item)
                     .Properties()
                     .ToDictionary(p => p.Name, p => p.Value.ToString());
                    dictionaries.Add(dictionary);
                }

                return dictionaries;
            }
        }

        /// <summary>
        /// Hàm API để đọc thông tin sản phẩm bằng mã sản phẩm từ Odoo.
        /// </summary>
        /// <param name="defaultCode"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public async Task<Dictionary<string, string>> GetProductItemByCode(string defaultCode, int uid, string sessionId)
        {
            using (var client = new HttpClient())
            {
                // Gửi request đọc dữ liệu
                client.DefaultRequestHeaders.Add("Cookie", $"session_id={sessionId}");
                var payload = new
                {
                    id = 1,
                    jsonrpc = "2.0",
                    method = "call",
                    @params = new
                    {
                        model = "product.template",
                        method = "search_read",
                        args = new object[] { },
                        kwargs = new
                        {
                            domain = new object[]
                            {
                                new object[] { "default_code", "=", defaultCode }  // tìm theo default_code
                            },
                            fields = new string[]
                            {
                                "id","name","default_code","x_quantity_per_code"
                            },
                            limit = 1,
                            context = new
                            {
                                lang = "vi_VN",
                                tz = "Asia/Ho_Chi_Minh",
                                uid = 2,
                                allowed_company_ids = new int[] { 1 },
                                bin_size = true
                            }
                        }
                    }
                };
                var content = new StringContent(
                    Newtonsoft.Json.JsonConvert.SerializeObject(payload),
                    Encoding.UTF8,
                    "application/json"
                );
                var response = await client.PostAsync($"{dbConfig.ServerUrl}/web/dataset/call_kw/product.template/read", content);
                var responseString = await response.Content.ReadAsStringAsync();
                var json = JObject.Parse(responseString);
                if (json["error"] != null)
                {
                    throw new Exception(json["error"]["message"].ToString());
                }
                var resultArray = (JArray)json["result"];
                var result = resultArray[0].ToObject<Dictionary<string, string>>();
                return result;
            }
        }

        #endregion
    }
}
