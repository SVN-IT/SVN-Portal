using Newtonsoft.Json.Linq;
using SVNShareLib;
using System.Text;
using System;
using System.Net;

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
        /// Hàm API để đọc thông tin sản xuất bằng mã productID từ Odoo.
        /// </summary>
        /// <param name="productionId"></param>
        /// <param name="uid"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        public async Task<Dictionary<string, string>> ReadProductionByProductIDAsync(int productID, int uid, string sessionId)
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
                            domain = new object[] {new object[] { "product_id", "=", productID }},
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
    }
}
