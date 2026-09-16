using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Irony.Parsing;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SVN_Portal.DAL.DataPortal;
using SVN_Portal.DAL.DTO;
using SVN_Portal.Services.Configurations;
using SVNShareLib;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
using SVNShareLib.Request;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ZXing;

namespace SVN_Portal.Controllers
{
    public class ProductionController : Controller
    {
        APIConfiguration aPIConfiguration;
        DBConfiguration dBConfiguration;
        private readonly IHttpClientFactory _httpClientFactory;
        public ProductionController(APIConfiguration aPIConfiguration, DBConfiguration dBConfiguration, IHttpClientFactory httpClientFactory)
        {
            this.aPIConfiguration = aPIConfiguration;
            this.dBConfiguration = dBConfiguration;
            _httpClientFactory = httpClientFactory;
        }

        public IActionResult Index(string workOrder)
        {
            if (!string.IsNullOrWhiteSpace(workOrder))
            {
                workOrder = workOrder.Replace("%2f", "/");
            }
            ViewBag.MasterWorkOrder = workOrder;
            return View();
        }

        public IActionResult InputLotToProduct(string workOrder)
        {
            if (!string.IsNullOrWhiteSpace(workOrder))
            {
                workOrder = workOrder.Replace("%2f", "/");
            }
            ViewBag.MasterWorkOrder = workOrder;
            return View();
        }

        public IActionResult InputLotToProductBT(string workOrder)
        {
            if (!string.IsNullOrWhiteSpace(workOrder))
            {
                workOrder = workOrder.Replace("%2f", "/");
            }
            ViewBag.MasterWorkOrder = workOrder;
            return View();
        }

        #region AJAX functions
        /// <summary>
        /// Hàm nhập kết quả sản xuất theo Work Order
        /// </summary>
        /// <param name="workOrderCode"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> GetProductByWorkOrder(string workOrderCode)
        {
            bool isGetDataFromViindoo = false;
            BODataProcessResult processResult = new BODataProcessResult();
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
            WO_ManagementUIDataPortal woDataPortal = new WO_ManagementUIDataPortal(dBConfiguration.GetConnectionString());
            try
            {
                if(!string.IsNullOrWhiteSpace(workOrderCode) && !workOrderCode.Contains("NM/MO/"))
                {
                    workOrderCode = $"NM/MO/{workOrderCode}";
                }
                string previousWorkOrderName = string.Empty;
                string currentMasterWorkOrderName = string.Empty;
                processResult = await GetWorkOrderInfo(workOrderCode, isGetDataFromViindoo);
                if(!processResult.OK)
                {
                    return Json(new { result = processResult.OK, message = processResult.Message });
                }
                currentMasterWorkOrderName = workOrderCode;
                string woJsonContent = processResult.Content != null ? processResult.Content.ToString() : null;

                if (string.IsNullOrWhiteSpace(woJsonContent))
                {
                    processResult.OK = false;
                    processResult.Message = "Can't get Work order infomation./ 无法获取工单信息。";
                    return Json(new { result = processResult.OK, message = processResult.Message });
                }

                //Lấy thông tin WO đã được nhập trc đó để xây dự giao diện
                SVN_ProductionInputLogDataPortal dataPortal = new SVN_ProductionInputLogDataPortal(dBConfiguration.GetConnectionString());
                var lastLog = await dataPortal.GetByMasterWOCodeAsync(currentMasterWorkOrderName);
                var productedQty = await dataPortal.GetProducedQtyByMasterWOCodeAsync(currentMasterWorkOrderName);


                WorkOrderInfo workOrderInfo = JsonConvert.DeserializeObject<WorkOrderInfo>(woJsonContent);
                string masterWorkOrder = string.Empty;
                decimal totalQty = 0;
                decimal remainQty = 0;
                if (lastLog != null)
                {
                    previousWorkOrderName = lastLog.wo_code;
                    masterWorkOrder = lastLog.master_wo_code;
                    totalQty = lastLog.total_qty;
                    remainQty = lastLog.total_qty - productedQty;
                }
                else
                {
                    totalQty = decimal.Parse(workOrderInfo.OrderInfo["product_qty"]);
                    remainQty = totalQty - productedQty;
                }

                //Lấy thông tin lệnh sản xuất cha
                WorkOrderInfo nextWorkOrderInfo = null;
                if (workOrderInfo.OrderInfo.ContainsKey("origin") && workOrderInfo.OrderInfo["origin"] != "False")
                {
                    BODataProcessResult nextWOBODataProcessResult = await GetWorkOrderInfo(workOrderInfo.OrderInfo["origin"], false);
                    if (nextWOBODataProcessResult.OK)
                    {
                        string nextWOJsonContent = nextWOBODataProcessResult.Content != null ? nextWOBODataProcessResult.Content.ToString() : null;
                        if (!string.IsNullOrWhiteSpace(nextWOJsonContent))
                        {
                            nextWorkOrderInfo = JsonConvert.DeserializeObject<WorkOrderInfo>(nextWOJsonContent);
                        }
                    }
                }


                string stringContent = BuildWorkOrderInfo(workOrderInfo, nextWorkOrderInfo, previousWorkOrderName, masterWorkOrder, totalQty, remainQty);
                processResult.OK = true;
                processResult.Message = stringContent;
                return Json(new { result = processResult.OK, message = processResult.Message, product_tracking = workOrderInfo.OrderInfo["product_tracking"] });
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return Json(new { result = processResult.OK, message = processResult.Message, content = processResult.Content });
        }

        /// <summary>
        /// Hàm lấy thông tin Work Order từ Viindoo và lưu vào cơ sở dữ liệu cục bộ nếu chưa có
        /// </summary>
        /// <param name="workOrderCode"></param>
        /// <param name="isGetDataFromViindoo"></param>
        /// <returns></returns>
        private async Task<BODataProcessResult> GetWorkOrderInfo(string workOrderCode, bool isGetDataFromViindoo)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
            WO_ManagementUIDataPortal woDataPortal = new WO_ManagementUIDataPortal(dBConfiguration.GetConnectionString());
            try
            {
                string currentMasterWorkOrderName = string.Empty;
                
                var woInfor = await woDataPortal.GetByWONameAsync(workOrderCode);
                if (woInfor != null)
                {
                    currentMasterWorkOrderName = woInfor.WO_Name;
                }
                else
                {
                    //Nếu chưa có MasterWO trc đó thì thực hiện lấy dữ liệu WO từ Viindoo
                    isGetDataFromViindoo = true;
                }

                string woJsonContent = string.Empty;

                if (isGetDataFromViindoo)
                {
                    InputProductDataRequest dataRequest = new InputProductDataRequest()
                    {
                        WorkOrderNumber = workOrderCode,
                        LotNumber = ""
                    };
                    var result = await httpClientHelper.PostRequest("api/ViindooConnect/GetWorkOrder", dataRequest, new CancellationToken(false));
                    if (result != null)
                    {
                        if (result.OK)
                        {
                            woJsonContent = result.Content.ToString();

                            WorkOrderInfo workOrderInfo1 = JsonConvert.DeserializeObject<WorkOrderInfo>(woJsonContent);
                            workOrderCode = workOrderInfo1.OrderInfo["name"];
                            if (!string.IsNullOrWhiteSpace(workOrderCode))
                            {
                                workOrderCode = workOrderCode.Split("-")[0];
                            }

                            WO_ManagementUI woManagementUI = new WO_ManagementUI()
                            {
                                WO_Name = workOrderCode,
                                WO_Content = woJsonContent,
                                Created_Date = DateTime.Now
                            };
                            var insertResult = await woDataPortal.CreateAsync(woManagementUI);
                            if (insertResult > 0)
                            {
                                processResult.OK = true;
                                processResult.Content = woJsonContent;
                                processResult.Message = "Get Work order information successfully./ 成功获取工单信息";
                            }
                            else
                            {
                                processResult.OK = false;
                                processResult.Message = "Failed to save Work order information into local database./ 未能将工单信息保存到本地数据库。";
                            }

                            currentMasterWorkOrderName = workOrderCode;
                        }
                        else
                        {
                            processResult.OK = false;
                            processResult.Message = result.Message;
                        }
                    }
                    else
                    {
                        processResult.OK = false;
                        processResult.Message = "Can't get Work order infomation./ 无法获取工单信息。";
                    }
                }
                else
                {
                    woJsonContent = woInfor.WO_Content;
                    processResult.OK = true;
                    processResult.Content = woJsonContent;
                    processResult.Message = "Get Work order information successfully./ 成功获取工单信息";
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
        /// Hàm dùng để xây dựng giao diện nhập kết quả sản xuất dựa trên thông tin WO lấy được từ Viindoo và thông tin WO đã được nhập trc đó (nếu có)
        /// </summary>
        /// <param name="workOrderInfo"></param>
        /// <param name="previousWorkOrderName"></param>
        /// <param name="masterWorkOrderLog"></param>
        /// <param name="totalQty"></param>
        /// <param name="remainQty"></param>
        /// <returns></returns>
        private string BuildWorkOrderInfo(WorkOrderInfo workOrderInfo, WorkOrderInfo nextWorkOrderInfo, string previousWorkOrderName, string masterWorkOrderLog, decimal totalQty, decimal remainQty)
        {
            SVN_product_id_componentDataPortal dataPortal = new SVN_product_id_componentDataPortal(dBConfiguration.GetConnectionString());
            List<string> componentIds = new List<string>();
            var existingLog = dataPortal.GetByProductId(workOrderInfo.OrderInfo["product_id"]);
            if (existingLog != null)
            {
                if (!string.IsNullOrWhiteSpace(existingLog.component_product_ids))
                {
                    componentIds = existingLog.component_product_ids.Split(',').Select(id => id.Trim()).ToList();
                }
            }

            string masterWorkOrder = workOrderInfo.OrderInfo["name"].Split("-")[0];
            string curWorkOrder = workOrderInfo.OrderInfo["name"];
            string curRemainQty = workOrderInfo.OrderInfo["product_qty"];
            string curTotalQty = workOrderInfo.OrderInfo["product_qty"];
            if (!string.IsNullOrWhiteSpace(masterWorkOrderLog))
            {
                masterWorkOrder = masterWorkOrderLog;
            }
            if (!string.IsNullOrWhiteSpace(previousWorkOrderName) && remainQty > 0)
            {
                string[] parts = previousWorkOrderName.Split('-');

                if (parts.Length == 2)
                {
                    string prefix = parts[0]; // "NM/MO/02638"
                    string suffix = parts[1]; // "001"

                    // 2. Chuyển phần hậu tố sang số và cộng thêm 1
                    if (int.TryParse(suffix, out int number))
                    {
                        number++;

                        // 3. Ghép lại với định dạng 3 chữ số (001, 002,...)
                        curWorkOrder = $"{prefix}-{number.ToString("D3")}";
                    }
                }
            }
            curRemainQty = remainQty.ToString();
            if (totalQty > 0)
            {
                curTotalQty = totalQty.ToString();
            }
            string isInputStatus = string.Empty;
            if(remainQty <= 0)
            {
                isInputStatus = "d-none pe-none";
            }
            string isInputQtyStatus = string.Empty;
            if(workOrderInfo.OrderInfo["product_tracking"] == "serial")
            {
                isInputQtyStatus = "pe-none";
            }
            StringBuilder sb = new StringBuilder();
            //sb.Append("<div class=\"col-12 col-md-3\">");
            //sb.Append("<div id=\"divResultLight\" class=\"box-square bg-light\">");
            //sb.Append("</div>");
            //sb.Append("</div>");
            sb.Append("<div class=\"col-12 col-md-12 row\">");

            sb.Append("<div class=\"col-12 col-md-6\">");
            sb.Append($"<label class=\"control-label d-none\">Work order/ 工作单: {curWorkOrder}</label>");
            sb.Append("</div>");
            sb.Append("<div class=\"col-12 col-md-6\">");
            if (nextWorkOrderInfo != null)
            {
                sb.Append($"<label class=\"control-label\">Next Work order/ 下一工单: {nextWorkOrderInfo.OrderInfo["name"]} | </label>");
                sb.Append($"<label class=\"control-label\">Next Product/ 下一款产品: {nextWorkOrderInfo.OrderInfo["product_name"]}</label>");
            }
            sb.Append("</div>");

            sb.Append("<div class=\"form-group\" style=\"width: 100%;\">");
            sb.Append("<h1 class=\"control-label d-none\">Work order/ 工作单: " + curWorkOrder + "</h1>");
            sb.Append("<input type=\"hidden\" name=\"Name\" class=\"form-control\" value=\"" + masterWorkOrder + "\" />");
            sb.Append("<input type=\"hidden\" name=\"SubName\" class=\"form-control\" value=\"" + curWorkOrder + "\" />");
            sb.Append("<input type=\"hidden\" name=\"ProductID\" class=\"form-control\" value=\"" + workOrderInfo.OrderInfo["product_id"] + "\" />");
            sb.Append("<input type=\"hidden\" name=\"ProductTracking\" class=\"form-control\" value=\"" + workOrderInfo.OrderInfo["product_tracking"] + "\" />");
            sb.Append("<input type=\"hidden\" name=\"TotalQuantity\" class=\"form-control\" value=\"" + curTotalQty + "\" />");
            sb.Append("</div>");
            sb.Append("<div class=\"col-12\">");
            sb.Append("<div class=\"form-group\">");
            sb.Append("<h1 class=\"control-label text-primary\"><strong>Product/ 产品: " + workOrderInfo.OrderInfo["product_name"] + " / WO Qty/ WO 数量: " + curTotalQty + "</strong></h1>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("<div class=\"col-12 col-md-3\">");
            sb.Append("<div class=\"form-group\">");
            sb.Append("<div class=\"row\">");
            sb.Append("<div class=\"col-3\">");
            sb.Append("<label class=\"control-label\">Quantity/ 数量:</label>");
            sb.Append("</div>");
            sb.Append($"<div class=\"col-4 {isInputQtyStatus}\">");
            sb.Append($"<input type=\"text\" name=\"Quantity\" class=\"form-control {isInputStatus}\" />");
            sb.Append("</div>");
            sb.Append("<div class=\"col-5\">");
            sb.Append("/" + curRemainQty);
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");
            if (workOrderInfo.OrderInfo["product_tracking"] == "serial" || workOrderInfo.OrderInfo["product_tracking"] == "lot")
            {
                sb.Append("<div class=\"col-12 col-md-3\">");
            }
            else
            {
                sb.Append("<div class=\"col-12 col-md-3 d-none\">");
            }
            sb.Append("<div class=\"form-group\">");
            sb.Append("<div class=\"row\">");
            sb.Append("<div class=\"col-2\">");
            if(workOrderInfo.OrderInfo["product_tracking"] == "serial")
            {
                sb.Append("<label class=\"control-label\">Serial/ 序列号:</label>");
            }
            else if (workOrderInfo.OrderInfo["product_tracking"] == "lot")
            {
                sb.Append("<label class=\"control-label\">Lot/ 很多:</label>");
            }
            sb.Append("</div>");
            sb.Append("<div class=\"col-10\">");
            sb.Append($"<input type=\"text\" name=\"Serial\" class=\"form-control {isInputStatus}\" />");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");

            // Khi component có tracking là serial hoặc lot thì sẽ không cho phép Upload list serial nữa mà phải scan từng cái một để tránh sai sót
            var componentsHasTracking = workOrderInfo.StockMoveInfo.Where(x => x["has_tracking"] == "serial" || x["has_tracking"] == "lot").ToList();
            if ((componentsHasTracking == null || componentsHasTracking.Count == 0) && workOrderInfo.OrderInfo["product_tracking"] != "none")
            {
                sb.Append("<div class=\"col-12 col-md-4\">");
                sb.Append("<div class=\"form-group\">");
                sb.Append("<div class=\"row\">");
                sb.Append("<div class=\"col-4\">");
                sb.Append("<label class=\"control-label\">Or Upload file serial/ 或者上传文件序列号:</label>");
                sb.Append("</div>");
                sb.Append("<div class=\"col-8\">");
                sb.Append($"<input type=\"file\" id=\"serialFile\" name=\"serialFile\" onchange=\"InputProductionResultWithSearialList()\" class=\"form-control {isInputStatus}\" accept=\".xlsx, .xls\" />");
                sb.Append("</div>");
                sb.Append("</div>");
                sb.Append("</div>");
                sb.Append("</div>");
            }

            sb.Append("<div class=\"col-12\">");
            sb.Append("<table class=\"table\">");
            sb.Append("<thead>");
            sb.Append("<tr>");
            sb.Append("<th scope=\"col\">Product/ 产品</th>");
            sb.Append("<th scope=\"col\">From/ 从</th>");
            sb.Append("<th scope=\"col\">Serial/Lot / 序列号/批次</th>");
            sb.Append("</tr>");
            sb.Append("</thead>");
            sb.Append("<tbody>");

            workOrderInfo.StockMoveInfo = workOrderInfo.StockMoveInfo.OrderByDescending(x => x["has_tracking"]).ToList();
            foreach (var item in workOrderInfo.StockMoveInfo)
            {
                sb.Append("<tr>");
                sb.Append("<th scope=\"row\">" + item["product_name"] + "</th>");
                sb.Append("<td>" + item["location_name"] + "</td>");

                string isAutoFillSerial = string.Empty;
                if(componentIds.Contains(item["product_id"]))
                {
                    isAutoFillSerial = "pe-none";
                }

                if (item["has_tracking"] == "serial")
                {
                    sb.Append($"<td class=\"{isAutoFillSerial}\"><input type=\"hidden\" class=\"form-control product-id\" value=\"" + item["product_id"] + "\" /><input type=\"hidden\" class=\"form-control has-tracking\" value=\"" + item["has_tracking"] + "\" /><input type=\"text\" placeholder=\"Scan Serial code\" class=\"form-control serial-input\" /><input type=\"hidden\" class=\"form-control product_name\" value=\"" + item["product_name"] + "\" /></td>");
                }
                else if (item["has_tracking"] == "lot")
                {
                    sb.Append($"<td class=\"{isAutoFillSerial}\"><input type=\"hidden\" class=\"form-control product-id\" value=\"" + item["product_id"] + "\" /><input type=\"hidden\" class=\"form-control has-tracking\" value=\"" + item["has_tracking"] + "\" /><input type=\"text\" placeholder=\"Scan Lot code\" class=\"form-control serial-input\" /><input type=\"hidden\" class=\"form-control product_name\" value=\"" + item["product_name"] + "\" /></td>");
                }
                else
                {
                    sb.Append("<td><input type=\"hidden\" class=\"form-control product-id\" value=\"" + item["product_id"] + "\" /><input type=\"hidden\" class=\"form-control  has-tracking\" value=\"" + item["has_tracking"] + "\" /><input type=\"hidden\" class=\"form-control\" /><input type=\"hidden\" class=\"form-control product_name\" value=\"" + item["product_name"] + "\" />Not Available</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</tbody>");
            sb.Append("</table>");
            sb.Append("<div class=\"col-12\">");
            sb.Append("<div class=\"form-group\" style=\"margin-top:33px\">");
            sb.Append("<button type=\"button\" class=\"btn btn-primary\" onclick=\"InputProductionResult()\">Confirm/ 确认</button>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");

            return sb.ToString();
        }

        /// <summary>
        /// Hàm kiểm tra số seri đã được dùng cho lệnh sản xuất khác chưa
        /// </summary>
        /// <param name="workOrderCode"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CheckLotSerialFG(string serial, string masterMOName, string productID)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_ProductionInputLogDataPortal dataPortal = new SVN_ProductionInputLogDataPortal(dBConfiguration.GetConnectionString());
            try
            {
                var existingLog = await dataPortal.GetByProductIDAndSerialCodeAsync(int.Parse(productID), serial);
                if (existingLog != null)
                {
                    if (existingLog.state == "Used")
                    {
                        processResult.OK = false;
                        processResult.Message = $"Serial/Lot {serial} has been used for the WO {existingLog.wo_code}. Please double-check./ 工单 {existingLog.wo_code} 已使用序列号/批号 {serial}。请仔细核对。";
                        //return Json(new { result = processResult.OK, message = processResult.Message });
                    }
                    else if (existingLog.state == "Consumed")
                    {
                        processResult.OK = false;
                        processResult.Message = $"Serial/Lot {serial} has been consumed for WO {existingLog.consumed_wo_code}. Please double-check./ 序列号/批号 {serial} 已用于工单 {existingLog.consumed_wo_code}。请仔细核对。";
                        //return Json(new { result = processResult.OK, message = processResult.Message });
                    }
                    else
                    {
                        processResult.OK = true;
                        processResult.Message = $"Serial/Lot {serial} is valid for use./ 序列号/批号 {serial} 可用。";
                        //return Json(new { result = processResult.OK, message = processResult.Message });
                    }
                }
                else
                {
                    processResult.OK = true;
                    processResult.Message = $"Serial/Lot {serial} is valid for input./ 序列号/批号 {serial} 可作为有效输入。";
                }
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return Json(new { result = processResult.OK, message = processResult.Message });
        }

        /// <summary>
        /// Hàm kiểm tra số seri đã dùng để tiêu hao cho lệnh sản xuất khác chưa
        /// </summary>
        /// <param name="workOrderCode"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CheckLotSerialComponemt(string serial, string productId, string masterMOName, string hasTracking, string productName)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_ProductionInputLogDataPortal dataPortal = new SVN_ProductionInputLogDataPortal(dBConfiguration.GetConnectionString());
            try
            {
                //chỉ đùng nếu là WIP
                var existingLog = await dataPortal.GetByProductIDAndSerialCodeAsync(int.Parse(productId), serial);
                if (existingLog != null)
                {
                    //if (existingLog.state == "Used")
                    //{
                    //    processResult.OK = false;
                    //    processResult.Message = $"Serial/Lot {serial} has been used for the WO {existingLog.wo_code}. Please double-check.";
                    //    //return Json(new { result = processResult.OK, message = processResult.Message });
                    //}
                    if (existingLog.state == "Consumed")
                    {
                        processResult.OK = false;
                        processResult.Message = $"Serial/Lot {serial} has been consumed for WO {existingLog.consumed_wo_code}, current remain qty: {existingLog.remain_qty}. Please double-check./ 序列号/批号 {serial} 已用于工单 {existingLog.consumed_wo_code}。请仔细核对。";
                        //return Json(new { result = processResult.OK, message = processResult.Message });
                    }
                    else
                    {
                        if (existingLog.product_type == "lot" && existingLog.remain_qty <= 0)
                        {
                            processResult.OK = false;
                            processResult.Message = $"Lot {serial} has been consumed for WO {existingLog.consumed_wo_code}, current remain qty: {existingLog.remain_qty}. Please double-check./ 批次 {serial} 已用于工单 {existingLog.consumed_wo_code}，当前剩余数量：{existingLog.remain_qty}。请再次核对。";
                        }
                        else
                        {
                            processResult.OK = true;
                            processResult.Message = $"Serial/Lot {serial} is valid for use./ 序列号/批号 {serial} 可用。";
                        }
                        //return Json(new { result = processResult.OK, message = processResult.Message });
                    }
                }
                else
                {
                    // Dùng cho trường hợp các con nvl quản lý theo serial hoặc lot cần nhập kho thì sẽ check trên Viindoo
                    HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
                    ProductDataRequest dataRequest = new ProductDataRequest()
                    {
                        lotNumber = serial,
                        seriNumber = masterMOName,
                        product_id = int.Parse(productId),
                        hasTracking = hasTracking
                    };
                    var result = await httpClientHelper.PostRequest("api/ViindooConnect/GetUsedLotForComponemt", dataRequest, new CancellationToken(false));
                    if (result != null)
                    {
                        processResult.OK = result.OK;
                        processResult.Message = result.Message;

                        //Nếu tìm dc lot trên Viindoo thì sẽ insert vào SVNDB để lần sau ko cần check nữa
                        if(processResult.OK == true)
                        {
                            decimal totalQty = 0;

                            if (!string.IsNullOrWhiteSpace(processResult.Message))
                            {
                                var messageParts = processResult.Message.Split(':');
                                if (messageParts.Length == 3 && decimal.TryParse(messageParts[2].Trim(), out decimal parsedQty))
                                {
                                    totalQty = parsedQty;
                                }
                            }


                            SVN_ProductionInputLogUI productDataUI = new SVN_ProductionInputLogUI();
                            productDataUI.wo_code = "Non WO";
                            productDataUI.serial_code = serial;
                            productDataUI.master_wo_code = "Non WO";
                            productDataUI.product_id = int.Parse(productId);
                            productDataUI.product_qty = 0;
                            productDataUI.product_type = hasTracking;
                            productDataUI.date_finished = DateTime.Now;
                            productDataUI.state = "Used";
                            productDataUI.component_list = string.Empty;
                            productDataUI.API_function = string.Empty;
                            productDataUI.API_parameters = string.Empty;
                            productDataUI.status = "Stock";
                            productDataUI.total_qty = totalQty;
                            productDataUI.remain_qty = totalQty;
                            var insertResult = await dataPortal.InsertAsync(productDataUI);
                        }
                    }
                    else
                    {
                        processResult.OK = false;
                        processResult.Message = $"Serial/Lot {serial} not yet input./ 序列号/批号 {serial} 尚未输入。";
                    }

                }
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return Json(new { result = processResult.OK, message = processResult.Message });
        }

        /// <summary>
        /// Kiểm tra để nhập số lượng sản phẩm theo mã seri
        /// </summary>
        /// <param name="workOrderCode"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CheckScanQuantitySerial(string serial)
        {
            SVN_Scan_Code_InfoDataPortal dataPortal = new SVN_Scan_Code_InfoDataPortal(dBConfiguration.GetConnectionString());
            try
            {
                var dataUI = await dataPortal.ReadByCode(serial);
                if (dataUI != null)
                {
                    return Json(new { result = true, message = "Serial has found./ 序列号已找到。", quantity = dataUI.SelectedQuantity });
                }
                else
                {
                    return Json(new { result = false, message = "Serial not found./ 未找到序列号。" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { result = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Luồng riêng, độc lập với luồng nhập KQSX chính thức: chỉ để ghi lại (log) mỗi giá trị được scan vào ô Serial/Lot
        /// (FG hoặc component) ngay khi scan, để đối chiếu sau này nếu luồng chính bị miss không lưu được.
        /// Không ảnh hưởng, không thay thế cho InputProductionResult/CheckLotSerialFG/CheckLotSerialComponemt.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> LogScanInput(string masterWoCode, string woCode, string productId, string productName, string fieldType, string hasTracking, string scannedValue)
        {
            try
            {
                SVN_ProductEnterScanInputLogDataPortal dataPortal = new SVN_ProductEnterScanInputLogDataPortal(dBConfiguration.GetConnectionString());
                SVN_ProductEnterScanInputLogEntryUI entry = new SVN_ProductEnterScanInputLogEntryUI
                {
                    product_id = int.TryParse(productId, out int parsedProductId) ? parsedProductId : 0,
                    product_name = productName,
                    field_type = fieldType,
                    has_tracking = hasTracking,
                    scanned_value = scannedValue,
                    scan_time = DateTime.Now
                };
                await dataPortal.UpsertAsync(masterWoCode, woCode, entry);
                return Json(new { result = true });
            }
            catch (Exception ex)
            {
                return Json(new { result = false, message = ex.Message });
            }
        }

        public async Task<IActionResult> InputProductionResult([FromBody] ProductionDataV1 data)
        {
            bool isInputToViindoo = false;
            BODataProcessResult processResult = new BODataProcessResult();
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
            SVN_ProductionInputLogDataPortal dataPortal = new SVN_ProductionInputLogDataPortal(dBConfiguration.GetConnectionString());

            mrp_bom_line_newDataPortal mrp_Bom_Line_DataPortal = new mrp_bom_line_newDataPortal(dBConfiguration.GetConnectionString());
            
            try
            {
                var bomLine = mrp_Bom_Line_DataPortal.GetDataByProductCode(int.Parse(data.ProductID));
                if (bomLine == null || bomLine.Count == 0)
                {
                    bomLine = new List<mrp_bom_lineUI>();
                    //processResult.OK = false;
                    //processResult.Message = $"No BOM found for product ID {data.ProductID}. Please check the BOM configuration.";
                    //return Json(new { result = processResult.OK, message = processResult.Message });
                }

                List<LotScanedRequest> lotScaneds = new List<LotScanedRequest>();
                var dataSearial = data.Products.Where(x => x.Has_tracking == "serial").Select(y =>
                {
                    LotScanedRequest lotScaned = new LotScanedRequest
                    {
                        product_id = y.Product_id,
                        lotNumber = y.Serial_code,
                        quantity = 1,
                        tracking = "serial"
                    };
                    lotScaneds.Add(lotScaned);
                    return y;
                }).ToList();
                var dataLot = data.Products.Where(x => x.Has_tracking == "lot").Select(y =>
                {
                    decimal consumedQty = 0;
                    var bomLineForProduct = bomLine.FirstOrDefault(b => b.product_id == y.Product_id);
                    if (bomLineForProduct != null)
                    {
                        consumedQty = bomLineForProduct.product_qty * decimal.Parse(data.Quantity);
                    }

                    LotScanedRequest lotScaned = new LotScanedRequest
                    {
                        product_id = y.Product_id,
                        lotNumber = y.Serial_code,
                        quantity = consumedQty,
                        tracking = "lot"
                    };
                    lotScaneds.Add(lotScaned);
                    return y;
                }).ToList();

                //Lưu cả dữ liệu không truy vết
                var dataNoneTracking = data.Products.Where(x => x.Has_tracking == "none").Select(y =>
                {
                    decimal consumedQty = 0;
                    var bomLineForProduct = bomLine.FirstOrDefault(b => b.product_id == y.Product_id);
                    if (bomLineForProduct != null)
                    {
                        consumedQty = bomLineForProduct.product_qty * decimal.Parse(data.Quantity);
                    }

                    LotScanedRequest lotScaned = new LotScanedRequest
                    {
                        product_id = y.Product_id,
                        lotNumber = "",
                        quantity = consumedQty,
                        tracking = "none"
                    };
                    lotScaneds.Add(lotScaned);
                    return y;
                }).ToList();

                //nếu lotScaneds mà có giá trị Quantity = 0 thì hiển thị thông báo lỗi
                var lotWithZeroQuantity = lotScaneds.FirstOrDefault(l => l.tracking == "lot" && l.quantity <= 0);
                if (lotWithZeroQuantity != null)
                {
                    processResult.OK = false;
                    processResult.Message = $"The consumed quantity for lot {lotWithZeroQuantity.lotNumber} is zero or negative. Please check the BOM configuration./ 批次 {lotWithZeroQuantity.lotNumber} 的消耗数量为零或负数。请检查物料清单配置。";
                    return Json(new { result = processResult.OK, message = processResult.Message });
                }

                InputProductDataRequest dataRequest = new InputProductDataRequest()
                {
                    WorkOrderNumber = data.Name,
                    LotNumber = data.Serial,
                    Quality = int.Parse(data.Quantity),
                    LotScaneds = lotScaneds
                };

                string[] parts = data.SubName.Split('-');

                if (parts.Length == 1)
                {
                    string prefix = parts[0]; // "NM/MO/02638"

                    data.SubName = $"{prefix}-001";
                }

                //Xử lý nhập KQSX vào SVNDB
                SVN_ProductionInputLogUI productDataUI = new SVN_ProductionInputLogUI();
                productDataUI.wo_code = data.SubName;
                productDataUI.serial_code = data.Serial;
                productDataUI.master_wo_code = data.Name;
                productDataUI.product_id = int.Parse(data.ProductID);
                productDataUI.product_qty = decimal.Parse(data.Quantity);
                productDataUI.product_type = data.ProductTracking;
                productDataUI.date_finished = DateTime.Now;
                productDataUI.state = "Used";
                productDataUI.component_list = JsonConvert.SerializeObject(lotScaneds);
                productDataUI.API_function = $"{aPIConfiguration.BaseURL}{aPIConfiguration.InputProductionByWorkOrderv1URL}";
                productDataUI.API_parameters = JsonConvert.SerializeObject(dataRequest);
                productDataUI.status = "Not synchronized";
                productDataUI.total_qty = decimal.Parse(data.TotalQuantity);
                if(productDataUI.product_type == "lot")
                {
                    productDataUI.remain_qty = productDataUI.product_qty;
                }
                //productDataUI.remain_qty = decimal.Parse(data.TotalQuantity) - decimal.Parse(data.Quantity);
                var insertResult = await dataPortal.InsertAsync(productDataUI);

                // Thực hiện cập nhật tiêu hao thành phần
                var lotSeriScaneds = lotScaneds.Where(x => x.tracking == "serial" || x.tracking == "lot").ToList();
                if (lotSeriScaneds != null && lotSeriScaneds.Count > 0)
                {
                    foreach (var item in lotSeriScaneds)
                    {
                        if (item.tracking == "serial")
                        {
                            var existComponentLog = await dataPortal.GetByProductIDAndSerialCodeAsync(item.product_id, item.lotNumber);
                            if (existComponentLog != null)
                            {
                                existComponentLog.state = "Consumed";
                                existComponentLog.consumed_wo_code = data.SubName;
                                var updateResult = await dataPortal.UpdateAsync(existComponentLog);
                                if (updateResult)
                                {
                                    processResult.OK = true;
                                }
                                else
                                {
                                    processResult.OK = false;
                                    processResult.Message = processResult.Message + Environment.NewLine + $"Failed to update component log with serial {item.lotNumber} as consumed./ 未能使用序列号 {item.lotNumber} 更新组件日志，该序列号已被消耗。";
                                }
                            }
                            else
                            {
                                //trong trường hợp là nvl nhập kho thì ko cần check nữa vì bên trên đã check rồi
                                processResult.OK = true;
                            }
                        }
                        else if (item.tracking == "lot")
                        {
                            var existComponentLog = await dataPortal.GetByProductIDAndSerialCodeAsync(item.product_id, item.lotNumber);
                            if(existComponentLog != null)
                            {
                                //existComponentLog.state = "Consuming";
                                existComponentLog.consumed_wo_code = existComponentLog.consumed_wo_code + "," + data.SubName;
                                if(existComponentLog.remain_qty > 0)
                                {
                                    existComponentLog.remain_qty = existComponentLog.remain_qty - item.quantity;
                                    existComponentLog.state = "Consuming";
                                    if (existComponentLog.remain_qty < 0)
                                    {
                                        existComponentLog.remain_qty = 0;
                                        existComponentLog.state = "Consumed";
                                    }
                                    if (existComponentLog.remain_qty == 0)
                                    {
                                        existComponentLog.state = "Consumed";
                                    }
                                }
                                
                                var updateResult = await dataPortal.UpdateAsync(existComponentLog);
                                if (updateResult)
                                {
                                    processResult.OK = true;
                                }
                                else
                                {
                                    processResult.OK = false;
                                    processResult.Message = processResult.Message + Environment.NewLine + $"Failed to update component log with lot {item.lotNumber} as consumed./ 未能将批次 {item.lotNumber} 更新为已消耗的组件日志。";
                                }
                            }
                            else
                            {
                                //trong trường hợp là nvl nhập kho thì ko cần check nữa vì bên trên đã check rồi
                                processResult.OK = false;
                                processResult.Message = processResult.Message + Environment.NewLine + $"Component log with lot {item.lotNumber} not found in the database./ 数据库中找不到批号为 {item.lotNumber} 的组件日志。";
                            }
                        }
                    }
                    if (processResult.OK)
                    {
                        processResult.Message = "Input production result successfully./ 输入生产结果成功。";
                    }
                }
                else
                {
                    if (insertResult > 0)
                    {
                        processResult.OK = true;
                        processResult.Message = "Input production result successfully./ 输入生产结果成功。";
                    }
                    else
                    {
                        processResult.OK = false;
                        processResult.Message = "Failed to input production result into local database./ 无法将生产结果输入本地数据库。";
                    }
                }

                TempData.Remove("MasterWorkOrderName");
                TempData["MasterWorkOrderName"] = data.Name;
                TempData.Keep("MasterWorkOrderName");


                if (isInputToViindoo)
                {
                    var result = await httpClientHelper.PostRequest(aPIConfiguration.InputProductionByWorkOrderv1URL, dataRequest, new CancellationToken(false));
                    if (result != null)
                    {
                        TempData.Remove("WorkOrderName");
                        TempData["WorkOrderName"] = data.SubName;
                        TempData.Keep("WorkOrderName");
                        if (result.OK)
                        {
                            string operation = "";

                            processResult.OK = true;
                            return Json(new { result = processResult.OK, message = processResult.Message, operation = operation, workorder = data.Name.Split("-")[0].Replace("/", "%2f") });
                        }
                        else
                        {
                            processResult.OK = false;
                            if (!string.IsNullOrWhiteSpace(result.Message))
                            {
                                processResult.Message = result.Message;
                            }
                            else
                            {
                                processResult.Message = "No data./ 无数据。";
                            }

                        }

                    }
                }
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return Json(new { result = processResult.OK, message = processResult.Message });
        }

        public async Task<IActionResult> InputProductionResultWithSerialList(ProductionDataWithSerialListV1 data)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
            SVN_ProductionInputLogDataPortal dataPortal = new SVN_ProductionInputLogDataPortal(dBConfiguration.GetConnectionString());
            try
            {
                List<LotScanedRequest> lotScaneds = new List<LotScanedRequest>();
                var dataSearial = data.Products.Where(x => x.Has_tracking == "serial").Select(y =>
                {
                    LotScanedRequest lotScaned = new LotScanedRequest
                    {
                        product_id = y.Product_id,
                        lotNumber = y.Serial_code,
                        tracking = "serial"
                    };
                    lotScaneds.Add(lotScaned);
                    return y;
                }).ToList();
                var dataLot = data.Products.Where(x => x.Has_tracking == "lot").Select(y =>
                {
                    LotScanedRequest lotScaned = new LotScanedRequest
                    {
                        product_id = y.Product_id,
                        lotNumber = y.Serial_code,
                        tracking = "lot"
                    };
                    lotScaneds.Add(lotScaned);
                    return y;
                }).ToList();

                List<string> serialCodes = GetSerialsFromExcel(data.serialFile);

                string inputSerialsSuccessMessage = "Inpputed Serial List Success: ";
                string inputSerialsFailMessage = "Inpputed Serial List Fail: ";
                string inputSerialsExistMessage = "Inpputed Serial List Exist: ";
                List<string> successSerials = new List<string>();
                List<string> failSerials = new List<string>();
                List<string> existSerials = new List<string>();

                string[] parts = data.SubName.Split('-');

                if (parts.Length == 1)
                {
                    string prefix = parts[0]; // "NM/MO/02638"

                    data.SubName = $"{prefix}-001";
                }
                bool isFirstInput = true;

                if (serialCodes != null && serialCodes.Count > 0)
                {
                    foreach (var serial in serialCodes)
                    {
                        InputProductDataRequest dataRequest = new InputProductDataRequest()
                        {
                            WorkOrderNumber = data.Name,
                            LotNumber = serial,
                            Quality = 1,
                            LotScaneds = lotScaneds
                        };
                        //Xử lý nhập KQSX vào SVNDB
                        SVN_ProductionInputLogUI productDataUI = new SVN_ProductionInputLogUI();
                        if (!isFirstInput)
                        {
                            data.SubName = GetNewWorkOrderName(data.SubName);
                        }
                        isFirstInput = false;

                        productDataUI.wo_code = data.SubName;
                        productDataUI.master_wo_code = data.Name;
                        productDataUI.serial_code = serial;
                        productDataUI.product_id = int.Parse(data.ProductID);
                        productDataUI.product_qty = 1;
                        productDataUI.product_type = data.ProductTracking;
                        productDataUI.date_finished = DateTime.Now;
                        productDataUI.state = "Used";
                        productDataUI.component_list = JsonConvert.SerializeObject(lotScaneds);
                        productDataUI.API_function = $"{aPIConfiguration.BaseURL}{aPIConfiguration.InputProductionByWorkOrderv1URL}";
                        productDataUI.API_parameters = JsonConvert.SerializeObject(dataRequest);
                        productDataUI.status = "Not synchronized";
                        productDataUI.total_qty = decimal.Parse(data.TotalQuantity);
                        //productDataUI.remain_qty = decimal.Parse(data.TotalQuantity) - decimal.Parse(data.Quantity);
                        var existingLog = await dataPortal.GetByProductIDAndSerialCodeAsync(int.Parse(data.ProductID), serial);
                        if(existingLog != null)
                        {
                            existSerials.Add(serial);
                            isFirstInput = true;
                        }
                        else
                        {
                            var insertResult = await dataPortal.InsertAsync(productDataUI);
                            if (insertResult > 0)
                            {
                                successSerials.Add(serial);
                            }
                            else
                            {
                                failSerials.Add(serial);
                                isFirstInput = true;
                            }
                        }
                        
                    }

                    TempData.Remove("MasterWorkOrderName");
                    TempData["MasterWorkOrderName"] = data.Name;
                    TempData.Keep("MasterWorkOrderName");

                    inputSerialsSuccessMessage = inputSerialsSuccessMessage + string.Join(", ", successSerials) + " Count: " + successSerials.Count;
                    inputSerialsFailMessage = inputSerialsFailMessage + string.Join(", ", failSerials) + " Count: " + failSerials.Count;
                    inputSerialsExistMessage = inputSerialsExistMessage + string.Join(", ", existSerials) + " Count: " + existSerials.Count;

                    processResult.OK = true;
                    processResult.Message = $"{inputSerialsSuccessMessage}{Environment.NewLine}{inputSerialsFailMessage}{Environment.NewLine}{inputSerialsExistMessage}";

                    //return Json(new { result = processResult.OK, message = processResult.Message, operation = operation, workorder = data.Name.Replace("/", "%2f") });
                }
                else
                {
                    processResult.OK = false;
                    processResult.Message = "File serial không có dữ liệu hoặc sai định dạng";
                }


            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return Json(new { result = false, message = processResult.Message });
        }

        [HttpPost]
        public async Task<IActionResult> PrintSerial([FromBody] PrintSerialRequestDto model)
        {
            // Validate dữ liệu đầu vào
            if (model == null || string.IsNullOrWhiteSpace(model.Serial))
            {
                return Json(new { success = false, message = "Serial is empty./ 序列号为空。" });
            }

            try
            {
                var client = _httpClientFactory.CreateClient();

                // 1. Chuẩn bị URL & Request Body
                string url = "https://ds.sigmaworldwide.io/print/api/external/print/serial/queue";
                string jsonContent = System.Text.Json.JsonSerializer.Serialize(new { serial = model.Serial });
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                // 2. Gọi API External
                HttpResponseMessage response = await client.PostAsync(url, content);
                string responseString = await response.Content.ReadAsStringAsync();

                // 3. Deserialize kết quả từ API ngoài
                var apiResult = System.Text.Json.JsonSerializer.Deserialize<ExternalPrintResponse>(responseString, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // 4. Trả về JSON cho Client AJAX
                if (apiResult != null && apiResult.Ok)
                {
                    return Json(new { success = true, message = "Print Serial Success./ 打印序列号成功。" });
                }
                else
                {
                    return Json(new
                    {
                        success = false,
                        message = apiResult?.Error ?? "Can't send print order to device./ 无法将打印订单发送到设备。"
                    });
                }
            }
            catch (HttpRequestException ex)
            {
                return Json(new { success = false, message = $"Server error/ 服务器错误: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"System error/ 系统错误: {ex.Message}" });
            }
        }

        [HttpPost]
        public IActionResult CheckAutoFillComponentByFGLotSerial(string product_id, string component_product_id)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_product_id_componentDataPortal dataPortal = new SVN_product_id_componentDataPortal(dBConfiguration.GetConnectionString());
            try
            {
                var existingLog = dataPortal.GetByProductId(product_id);
                if (existingLog != null)
                {
                    if(!string.IsNullOrWhiteSpace(existingLog.component_product_ids))
                    {
                        
                        var componentIds = existingLog.component_product_ids.Split(',').Select(id => id.Trim()).ToList();
                        if (componentIds.Contains(component_product_id))
                        {
                            processResult.OK = true;
                            processResult.Message = $"Component product ID {component_product_id} is valid for auto-fill./ 组件产品ID {component_product_id} 可用于自动填充。";
                        }
                        else
                        {
                            processResult.OK = false;
                            processResult.Message = $"Component product ID {component_product_id} is NOT valid for auto-fill./ 组件产品ID {component_product_id} 不可用于自动填充。";
                        }
                    }
                }
                else
                {
                    processResult.OK = false;
                    processResult.Message = "Không có product_id trong đây thì không có fill thôi";
                }
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return Json(new { result = processResult.OK, message = processResult.Message });
        }
        #endregion

        #region hàm xử lý input excel danh sách serial
        private List<string> GetSerialsFromExcel(IFormFile file)
        {
            List<string> serials = new List<string>();

            using (var stream = file.OpenReadStream())
            {
                using (SpreadsheetDocument doc = SpreadsheetDocument.Open(stream, false))
                {
                    // Lấy Sheet đầu tiên
                    WorkbookPart workbookPart = doc.WorkbookPart;
                    SharedStringTablePart sstpart = workbookPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault();
                    SharedStringTable sst = sstpart?.SharedStringTable;

                    WorksheetPart worksheetPart = workbookPart.WorksheetParts.First();
                    Worksheet sheet = worksheetPart.Worksheet;

                    // Lấy tất cả các dòng (Rows)
                    var rows = sheet.Descendants<Row>();

                    foreach (Row row in rows)
                    {
                        // Lấy cell đầu tiên của mỗi dòng (Cột A)
                        Cell cell = row.Elements<Cell>().FirstOrDefault();
                        if (cell != null)
                        {
                            string value = GetCellValue(cell, sst);
                            if (!string.IsNullOrWhiteSpace(value) && value != "serial_code") // Bỏ qua tiêu đề nếu có
                            {
                                serials.Add(value.Trim());
                            }
                        }
                    }
                }
            }
            return serials;
        }

        // Hàm bổ trợ để đọc giá trị thực tế của Cell (Xử lý trường hợp SharedString)
        private string GetCellValue(Cell cell, SharedStringTable sst)
        {
            if (cell.CellValue == null) return string.Empty;

            string value = cell.CellValue.InnerText;

            // Nếu cell là kiểu SharedString (chuỗi dùng chung), phải tra cứu trong bảng sst
            if (cell.DataType != null && cell.DataType == CellValues.SharedString && sst != null)
            {
                return sst.ElementAt(int.Parse(value)).InnerText;
            }

            return value;
        }
        #endregion

        private string GetNewWorkOrderName(string previousWorkOrderName)
        {
            if (string.IsNullOrWhiteSpace(previousWorkOrderName))
            {
                return string.Empty;
            }
            string[] parts = previousWorkOrderName.Split('-');
            if (parts.Length == 2)
            {
                string prefix = parts[0]; // "NM/MO/02638"
                string suffix = parts[1]; // "001"
                // 2. Chuyển phần hậu tố sang số và cộng thêm 1
                if (int.TryParse(suffix, out int number))
                {
                    number++;
                    // 3. Ghép lại với định dạng 3 chữ số (001, 002,...)
                    return $"{prefix}-{number.ToString("D3")}";
                }
            }
            return string.Empty; // Trả về chuỗi rỗng nếu định dạng không đúng
        }
    }

    public class PrintSerialRequestDto
    {
        public string Serial { get; set; }
    }

    // Response nhận từ API external
    public class ExternalPrintResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }
    }
}
