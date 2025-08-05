using Newtonsoft.Json.Linq;
using SVNShareLib;
using System.Text;
using System;
using System.Net;
using Newtonsoft.Json;

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
                            domain = new object[] { new object[] { "product_id", "=", productID } },
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
                                overview_progress = int.Parse(productionOrderInfo["overview_progress"]),
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
    }
}
