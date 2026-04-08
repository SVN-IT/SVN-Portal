using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SVN_Portal.Services.Configurations;
using SVNShareLib;
using SVNShareLib.Request;
using System.Text;
using ZXing;

namespace SVN_Portal.Controllers
{
    public class ProductionController : Controller
    {
        APIConfiguration aPIConfiguration;
        public ProductionController(APIConfiguration aPIConfiguration)
        {
            this.aPIConfiguration = aPIConfiguration;
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
            try
            {
                string currentMasterWorkOrderName = TempData.Peek("MasterWorkOrderName") as string;
                string previousWorkOrderName = TempData.Peek("WorkOrderName") as string;

                //Nếu chưa có MasterWO trc đó thì thực hiện lấy dữ liệu WO từ Viindoo
                if(string.IsNullOrWhiteSpace(currentMasterWorkOrderName))
                {
                    isGetDataFromViindoo = true;
                }

                //Nếu đang nhập 1 WO mới thì lưu WO mới vào tiến hành lấy dữ liệu từ Viindoo
                if (!string.IsNullOrWhiteSpace(currentMasterWorkOrderName) && workOrderCode != currentMasterWorkOrderName)
                {
                    TempData.Remove("MasterWorkOrderName");
                    TempData["MasterWorkOrderName"] = workOrderCode;
                    TempData.Keep("MasterWorkOrderName");
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
                            TempData.Remove("WOContent");
                            TempData["WOContent"] = woJsonContent;
                            TempData.Keep("WOContent");
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
                        processResult.Message = "Can't get Work order infomation";
                    }
                }
                else
                {
                    woJsonContent = TempData.Peek("WOContent") as string;
                }

                if (string.IsNullOrWhiteSpace(woJsonContent))
                {
                    processResult.OK = false;
                    processResult.Message = "Can't get Work order infomation";
                    return Json(new { result = processResult.OK, message = processResult.Message });
                }

                WorkOrderInfo workOrderInfo = JsonConvert.DeserializeObject<WorkOrderInfo>(woJsonContent);
                string stringContent = BuildWorkOrderInfo(workOrderInfo, previousWorkOrderName);
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

        private string BuildWorkOrderInfo(WorkOrderInfo workOrderInfo, string previousWorkOrderName)
        {
            string masterWorkOrder = workOrderInfo.OrderInfo["name"].Split("-")[0];
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"col-12 col-md-3\">");
            if (!string.IsNullOrWhiteSpace(previousWorkOrderName))
            {
                if (workOrderInfo.OrderInfo["name"] != previousWorkOrderName)
                {
                    sb.Append("<div id=\"divResultLight\" class=\"box-square bg-success\">");
                }
                else
                {
                    sb.Append("<div id=\"divResultLight\" class=\"box-square bg-warning\">");
                }
            }
            else
            {
                sb.Append("<div id=\"divResultLight\" class=\"box-square bg-light\">");
            }
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("<div class=\"col-12 col-md-9 row\">");
            sb.Append("<div class=\"form-group\" style=\"width: 100%;\">");
            if (!string.IsNullOrWhiteSpace(previousWorkOrderName))
            {
                if (workOrderInfo.OrderInfo["name"] != previousWorkOrderName)
                {
                    sb.Append("<div class=\"alert alert-success\" role=\"alert\">");
                    sb.Append("Lệnh " + previousWorkOrderName + " input result success");
                    sb.Append("</div>");
                }
                else
                {
                    sb.Append("<div class=\"alert alert-warning\" role=\"alert\">");
                    sb.Append("Lệnh " + previousWorkOrderName + " input result fail, please contact to Admin");
                    sb.Append("</div>");
                }
            }
            sb.Append("<h1 class=\"control-label\">Work order: " + workOrderInfo.OrderInfo["name"] + "</h1>");
            sb.Append("<input type=\"hidden\" name=\"Name\" class=\"form-control\" value=\"" + masterWorkOrder + "\" />");
            sb.Append("<input type=\"hidden\" name=\"SubName\" class=\"form-control\" value=\"" + workOrderInfo.OrderInfo["name"] + "\" />");
            sb.Append("<input type=\"hidden\" name=\"ProductID\" class=\"form-control\" value=\"" + workOrderInfo.OrderInfo["product_id"] + "\" />");
            sb.Append("<input type=\"hidden\" name=\"ProductTracking\" class=\"form-control\" value=\"" + workOrderInfo.OrderInfo["product_tracking"] + "\" />");
            sb.Append("</div>");
            sb.Append("<div class=\"col-12\">");
            sb.Append("<div class=\"form-group\">");
            sb.Append("<h2 class=\"control-label\">Product: " + workOrderInfo.OrderInfo["product_name"] + "</h2>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("<div class=\"col-12 col-md-3\">");
            sb.Append("<div class=\"form-group\">");
            sb.Append("<div class=\"row\">");
            sb.Append("<div class=\"col-3\">");
            sb.Append("<label class=\"control-label\">Quantity:</label>");
            sb.Append("</div>");
            sb.Append("<div class=\"col-4\">");
            sb.Append("<input type=\"text\" name=\"Quantity\" class=\"form-control\" />");
            sb.Append("</div>");
            sb.Append("<div class=\"col-5\">");
            sb.Append("/" + workOrderInfo.OrderInfo["product_qty"]);
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
            sb.Append("<label class=\"control-label\">Serial number:</label>");
            sb.Append("</div>");
            sb.Append("<div class=\"col-10\">");
            sb.Append("<input type=\"text\" name=\"Serial\" class=\"form-control\" />");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");

            // Khi component có tracking là serial hoặc lot thì sẽ không cho phép Upload list serial nữa mà phải scan từng cái một để tránh sai sót
            var componentsHasTracking = workOrderInfo.StockMoveInfo.Where(x => x["has_tracking"] == "serial" || x["has_tracking"] == "lot").ToList();
            if (componentsHasTracking == null || componentsHasTracking.Count == 0)
            {
                sb.Append("<div class=\"col-12 col-md-4\">");
                sb.Append("<div class=\"form-group\">");
                sb.Append("<div class=\"row\">");
                sb.Append("<div class=\"col-4\">");
                sb.Append("<label class=\"control-label\">Or Upload file serial:</label>");
                sb.Append("</div>");
                sb.Append("<div class=\"col-8\">");
                sb.Append("<input type=\"file\" id=\"serialFile\" name=\"serialFile\" onchange=\"InputProductionResultWithSearialList()\" class=\"form-control\" accept=\".xlsx, .xls\" />");
                sb.Append("</div>");
                sb.Append("</div>");
                sb.Append("</div>");
                sb.Append("</div>");
            }

            sb.Append("<div class=\"col-12\">");
            sb.Append("<table class=\"table\">");
            sb.Append("<thead>");
            sb.Append("<tr>");
            sb.Append("<th scope=\"col\">Product</th>");
            sb.Append("<th scope=\"col\">From</th>");
            sb.Append("<th scope=\"col\">Serial number</th>");
            sb.Append("</tr>");
            sb.Append("</thead>");
            sb.Append("<tbody>");

            workOrderInfo.StockMoveInfo = workOrderInfo.StockMoveInfo.OrderByDescending(x => x["has_tracking"]).ToList();
            foreach (var item in workOrderInfo.StockMoveInfo)
            {
                sb.Append("<tr>");
                sb.Append("<th scope=\"row\">" + item["product_name"] + "</th>");
                sb.Append("<td>" + item["location_name"] + "</td>");
                if (item["has_tracking"] == "serial")
                {
                    sb.Append("<td><input type=\"hidden\" class=\"form-control product-id\" value=\"" + item["product_id"] + "\" /><input type=\"hidden\" class=\"form-control has-tracking\" value=\"" + item["has_tracking"] + "\" /><input type=\"text\" placeholder=\"Scan Serial code\" class=\"form-control serial-input\" /></td>");
                }
                else if (item["has_tracking"] == "lot")
                {
                    sb.Append("<td><input type=\"hidden\" class=\"form-control product-id\" value=\"" + item["product_id"] + "\" /><input type=\"hidden\" class=\"form-control has-tracking\" value=\"" + item["has_tracking"] + "\" /><input type=\"text\" placeholder=\"Scan Lot code\" class=\"form-control serial-input\" /></td>");
                }
                else
                {
                    sb.Append("<td><input type=\"hidden\" class=\"form-control product-id\" value=\"" + item["product_id"] + "\" /><input type=\"hidden\" class=\"form-control  has-tracking\" value=\"" + item["has_tracking"] + "\" /><input type=\"hidden\" class=\"form-control\" />Not Available</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</tbody>");
            sb.Append("</table>");
            sb.Append("<div class=\"col-12\">");
            sb.Append("<div class=\"form-group\" style=\"margin-top:33px\">");
            sb.Append("<button type=\"button\" class=\"btn btn-primary\" onclick=\"InputProductionResult()\">Confirm</button>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");

            return sb.ToString();
        }
        #endregion
    }
}
