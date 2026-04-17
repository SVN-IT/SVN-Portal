using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Irony.Parsing;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SVN_Portal.DAL.DataPortal;
using SVN_Portal.Services.Configurations;
using SVNShareLib;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
using SVNShareLib.Request;
using System.Text;
using ZXing;

namespace SVN_Portal.Controllers
{
    public class ProductionController : Controller
    {
        APIConfiguration aPIConfiguration;
        DBConfiguration dBConfiguration;
        public ProductionController(APIConfiguration aPIConfiguration, DBConfiguration dBConfiguration)
        {
            this.aPIConfiguration = aPIConfiguration;
            this.dBConfiguration = dBConfiguration;
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
                //string previousWorkOrderName = TempData.Peek("WorkOrderName") as string;
                string previousWorkOrderName = string.Empty;

                //Nếu chưa có MasterWO trc đó thì thực hiện lấy dữ liệu WO từ Viindoo
                if (string.IsNullOrWhiteSpace(currentMasterWorkOrderName))
                {
                    isGetDataFromViindoo = true;
                }

                //Nếu đang nhập 1 WO mới thì lưu WO mới vào tiến hành lấy dữ liệu từ Viindoo
                if (!string.IsNullOrWhiteSpace(currentMasterWorkOrderName) && workOrderCode != currentMasterWorkOrderName)
                {
                    TempData.Remove("MasterWorkOrderName");
                    TempData["MasterWorkOrderName"] = workOrderCode;
                    TempData.Keep("MasterWorkOrderName");
                    currentMasterWorkOrderName = workOrderCode;
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

                            TempData.Remove("MasterWorkOrderName");
                            TempData["MasterWorkOrderName"] = workOrderCode;
                            TempData.Keep("MasterWorkOrderName");
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
                
                string stringContent = BuildWorkOrderInfo(workOrderInfo, previousWorkOrderName, masterWorkOrder, totalQty, remainQty);
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
        /// Hàm dùng để xây dựng giao diện nhập kết quả sản xuất dựa trên thông tin WO lấy được từ Viindoo và thông tin WO đã được nhập trc đó (nếu có)
        /// </summary>
        /// <param name="workOrderInfo"></param>
        /// <param name="previousWorkOrderName"></param>
        /// <param name="masterWorkOrderLog"></param>
        /// <param name="totalQty"></param>
        /// <param name="remainQty"></param>
        /// <returns></returns>
        private string BuildWorkOrderInfo(WorkOrderInfo workOrderInfo, string previousWorkOrderName, string masterWorkOrderLog, decimal totalQty, decimal remainQty)
        {
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
            StringBuilder sb = new StringBuilder();
            //sb.Append("<div class=\"col-12 col-md-3\">");
            //sb.Append("<div id=\"divResultLight\" class=\"box-square bg-light\">");
            //sb.Append("</div>");
            //sb.Append("</div>");
            sb.Append("<div class=\"col-12 col-md-12 row\">");
            sb.Append("<div class=\"form-group\" style=\"width: 100%;\">");
            sb.Append("<h1 class=\"control-label\">Work order: " + curWorkOrder + "</h1>");
            sb.Append("<input type=\"hidden\" name=\"Name\" class=\"form-control\" value=\"" + masterWorkOrder + "\" />");
            sb.Append("<input type=\"hidden\" name=\"SubName\" class=\"form-control\" value=\"" + curWorkOrder + "\" />");
            sb.Append("<input type=\"hidden\" name=\"ProductID\" class=\"form-control\" value=\"" + workOrderInfo.OrderInfo["product_id"] + "\" />");
            sb.Append("<input type=\"hidden\" name=\"ProductTracking\" class=\"form-control\" value=\"" + workOrderInfo.OrderInfo["product_tracking"] + "\" />");
            sb.Append("<input type=\"hidden\" name=\"TotalQuantity\" class=\"form-control\" value=\"" + curTotalQty + "\" />");
            sb.Append("</div>");
            sb.Append("<div class=\"col-12\">");
            sb.Append("<div class=\"form-group\">");
            sb.Append("<h2 class=\"control-label\">Product: " + workOrderInfo.OrderInfo["product_name"] + " / Total Qty: " + curTotalQty + "</h2>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("<div class=\"col-12 col-md-3\">");
            sb.Append("<div class=\"form-group\">");
            sb.Append("<div class=\"row\">");
            sb.Append("<div class=\"col-3\">");
            sb.Append("<label class=\"control-label\">Quantity:</label>");
            sb.Append("</div>");
            sb.Append("<div class=\"col-4\">");
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
            sb.Append("<label class=\"control-label\">Serial number:</label>");
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
                sb.Append("<label class=\"control-label\">Or Upload file serial:</label>");
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
                        processResult.Message = $"Serial/Lot {serial} has been used for the WO {existingLog.wo_code}. Please double-check.";
                        //return Json(new { result = processResult.OK, message = processResult.Message });
                    }
                    //else if (existingLog.state == "Consumed")
                    //{
                    //    processResult.OK = false;
                    //    processResult.Message = $"Serial/Lot {serial} has been consumed for WO {existingLog.consumed_wo_code}. Please double-check.";
                    //    //return Json(new { result = processResult.OK, message = processResult.Message });
                    //}
                    else
                    {
                        processResult.OK = true;
                        processResult.Message = $"Serial/Lot {serial} is valid for use.";
                        //return Json(new { result = processResult.OK, message = processResult.Message });
                    }
                }
                else
                {
                    processResult.OK = true;
                    processResult.Message = $"Serial/Lot {serial} is valid for input.";
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
        public async Task<IActionResult> CheckLotSerialComponemt(string serial, string productId, string masterMOName, string hasTracking)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_ProductionInputLogDataPortal dataPortal = new SVN_ProductionInputLogDataPortal(dBConfiguration.GetConnectionString());
            try
            {
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
                        processResult.Message = $"Serial/Lot {serial} has been consumed for WO {existingLog.consumed_wo_code}. Please double-check.";
                        //return Json(new { result = processResult.OK, message = processResult.Message });
                    }
                    else
                    {
                        processResult.OK = true;
                        processResult.Message = $"Serial/Lot {serial} is valid for use.";
                        //return Json(new { result = processResult.OK, message = processResult.Message });
                    }
                }
                else
                {
                    processResult.OK = false;
                    processResult.Message = "Không có dữ liệu";
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
                    return Json(new { result = true, message = "Tìm thấy mã serial", quantity = dataUI.SelectedQuantity });
                }
                else
                {
                    return Json(new { result = false, message = "Không tìm thấy mã serial" });
                }
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
                //productDataUI.remain_qty = decimal.Parse(data.TotalQuantity) - decimal.Parse(data.Quantity);
                var insertResult = await dataPortal.InsertAsync(productDataUI);

                // Thực hiện cập nhật tiêu hao thành phần
                if (lotScaneds != null && lotScaneds.Count > 0)
                {
                    foreach (var item in lotScaneds)
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
                                    processResult.Message = processResult.Message + Environment.NewLine + $"Failed to update component log with serial {item.lotNumber} as consumed.";
                                }
                            }
                        }
                        else if (item.tracking == "lot")
                        {

                        }
                    }
                    if (processResult.OK)
                    {
                        processResult.Message = "Input production result successfully";
                    }
                }
                else
                {
                    if (insertResult > 0)
                    {
                        processResult.OK = true;
                        processResult.Message = "Input production result successfully";
                    }
                    else
                    {
                        processResult.OK = false;
                        processResult.Message = "Failed to input production result into local database.";
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
                                processResult.Message = "Không có dữ liệu";
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
}
