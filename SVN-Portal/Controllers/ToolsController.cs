using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Lextm.SharpSnmpLib.Messaging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Crmf;
using Org.BouncyCastle.Asn1.Ocsp;
using PrinterServices.Objects;
using SVN_Portal.DAL.DataPortal;
using SVN_Portal.DAL.DTO;
using SVN_Portal.Models;
using SVN_Portal.Services.Configurations;
using SVN_Portal.Services.Helpers;
using SVNShareLib;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
using SVNShareLib.Request;
using SVNShareLib.Utils;
using System.Drawing;
using System.Text;
using System.Threading.Tasks;
using static SVNShareLib.Utils.LogService;

namespace SVN_Portal.Controllers
{
    public class ToolsController : Controller
    {
        private readonly ILogger<ToolsController> _logger;
        DBConfiguration dBConfiguration;
        string connectionString;
        ToolsHelper toolsHelper;
        APIConfiguration aPIConfiguration;
        TOASTLabelConfiguration labelConfiguration;
        OperInfoConfig operInfoConfig;
        public ToolsController(DBConfiguration dBConfiguration,
            ILogger<ToolsController> logger,
            ToolsHelper toolsHelper, 
            APIConfiguration aPIConfiguration,
            OperInfoConfig operInfoConfig,
            TOASTLabelConfiguration labelConfiguration)
        {
            this.dBConfiguration = dBConfiguration;
            connectionString = dBConfiguration.GetConnectionString();
            this.toolsHelper = toolsHelper;
            this.aPIConfiguration = aPIConfiguration;
            this.labelConfiguration = labelConfiguration;
            this.operInfoConfig = operInfoConfig;
            _logger = logger;
        }
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Màn hình quản lý máy in
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> PrinterManager()
        {
            SVN_Printer_InfoDataPortal printerDataPortal = new SVN_Printer_InfoDataPortal(connectionString);
            List<PrinterConfigData> printerConfigData = new List<PrinterConfigData>();
            try
            {
                printerConfigData = await printerDataPortal.ReadList();
                if (printerConfigData == null)
                {
                    printerConfigData = new List<PrinterConfigData>();
                }
            }
            catch
            {
                printerConfigData = new List<PrinterConfigData>();
            }
            ViewBag.oper = "Quản lý máy in";
            return View(printerConfigData);
        }

        /// <summary>
        /// Hàm lấy thông tin máy in theo ID
        /// </summary>
        /// <param name="printerID"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> GetPrinterInfoByID(string printerID)
        {
            SVN_Printer_InfoDataPortal printerDataPortal = new SVN_Printer_InfoDataPortal(connectionString);
            PrinterConfigData printerConfigData = new PrinterConfigData();
            try
            {
                printerConfigData = await printerDataPortal.ReadByID(printerID);
                if(printerConfigData == null)
                {
                    return Json(new {ok = false, message = "Không tìm thấy máy in." });
                }
                return Json(new {ok = true, 
                    message = "Lấy dữ liệu thành công",
                    name_Printer = printerConfigData.Name_Printer,
                    mac_Address = printerConfigData.MAC_Printer,
                    ip_Address = printerConfigData.IP_Printer,
                    port = printerConfigData.Port_Printer,
                    size = printerConfigData.Size,
                    type = printerConfigData.Type,
                    zpl_Template = printerConfigData.ZPL_Temp,
                    dpl_Template = printerConfigData.DPL_Temp });
            }
            catch(Exception ex)
            {
                string message = ex.Message;
                return Json(new {message = message });
            }
        }

        /// <summary>
        /// Hàm thêm mới thông tin máy in
        /// </summary>
        /// <param name="insertData"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> InsertPrinterInfo([FromBody] PrinterConfigData insertData)
        {
            SVN_Printer_InfoDataPortal printerDataPortal = new SVN_Printer_InfoDataPortal(connectionString);
            try
            {
                var result = await printerDataPortal.Insert(insertData);
                if (result <= 0)
                {
                    return Json(new { ok = false, message = "Thêm thông tin máy in không thành công." });
                }
                return Json(new { ok = true, message = "Thêm thông tin máy in thành công." });
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                return Json(new { message = message });
            }
        }

        /// <summary>
        /// Cập nhật thông tin máy in
        /// </summary>
        /// <param name="updateData"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> UpdatePrinterInfo([FromBody]PrinterConfigData updateData)
        {
            SVN_Printer_InfoDataPortal printerDataPortal = new SVN_Printer_InfoDataPortal(connectionString);
            try
            {
                var result = await printerDataPortal.Update(updateData);
                if (result <= 0)
                {
                    return Json(new { ok = false, message = "Cập nhật thông tin máy in không thành công." });
                }
                return Json(new { ok = true, message = "Cập nhật thông tin máy in thành công." });
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                return Json(new { message = message });
            }
        }

        /// <summary>
        /// Màn hình in tem Astro 0001
        /// </summary>
        /// <param name="selectedProductID"></param>
        /// <param name="selectedPrinterID"></param>
        /// <param name="countRows"></param>
        /// <returns></returns>
        public async Task<IActionResult> PrintTem(int selectedProductID, string selectedPrinterID, int countRows = 1) 
        {
            SVN_product_productDataPortal productDataPortal = new SVN_product_productDataPortal(connectionString);
            SVN_Printer_InfoDataPortal printerDataPortal = new SVN_Printer_InfoDataPortal(connectionString);
            List<PrintTemViewModel> viewModels = new List<PrintTemViewModel>();
            List<SVN_product_productUI> products = new List<SVN_product_productUI>();
            List<PrinterConfigData> printerConfigData = new List<PrinterConfigData>();
            try
            {
                printerConfigData = await printerDataPortal.ReadList();
                if (printerConfigData == null)
                {
                    printerConfigData = new List<PrinterConfigData>();
                }
                SelectList printerList = new SelectList(printerConfigData, "ID_Printer", "Name_Printer");
                if (!string.IsNullOrWhiteSpace(selectedPrinterID))
                {
                    printerList = new SelectList(printerConfigData, "ID_Printer", "Name_Printer", selectedPrinterID);
                }


                products = await productDataPortal.ReadList();
                if(products == null)
                {
                    products = new List<SVN_product_productUI>();
                }
                products = products.Select(product =>
                {
                    string item_code = string.Empty;
                    if(!string.IsNullOrWhiteSpace(product.default_code))
                    {
                        item_code ="[" + product.default_code + "] ";
                    }    

                    if(!string.IsNullOrWhiteSpace(product.product_name) && product.product_name.Contains("vi_VN"))
                    {
                        Dictionary<string, string> dictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(product.product_name);
                        product.product_name = item_code + dictionary["vi_VN"];
                    }
                    else
                    {
                        product.product_name = item_code + product.product_name;
                    }
                        return product;
                }).ToList();
                SelectList productList = new SelectList(products, "id", "product_name");
                if (selectedProductID != 0)
                {
                    productList = new SelectList(products, "id", "product_name", selectedProductID);

                    HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);

                    ProductDataRequest dataRequest = new ProductDataRequest()
                    {
                        product_id = selectedProductID,
                        lotNumber = "",
                        count = countRows,
                        seriNumber = ""
                    };

                    var result = await httpClientHelper.PostRequest(aPIConfiguration.GetLotByMODoneURL, dataRequest, new CancellationToken(false));
                    if(result != null)
                    {
                        if(result.OK)
                        {
                            var dataUI = JsonConvert.DeserializeObject<List<svn_lot_infoUI>>(result.Content.ToString());
                            if (dataUI != null)
                            {
                                dataUI = dataUI.Select(item =>
                                {
                                    PrintTemViewModel viewModel = new PrintTemViewModel();
                                    if (item.item_name.Contains("vi_VN"))
                                    {
                                        Dictionary<string, string> dictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(item.item_name);
                                        item.item_name = dictionary["vi_VN"];
                                    }
                                    else
                                    {
                                        item.item_name = item.item_name;
                                    }
                                    viewModel.item_name = item.item_name;
                                    viewModel.lot_code = item.lot_code;
                                    viewModel.product_qty = item.product_qty;
                                    viewModels.Add(viewModel);
                                    return item;
                                }).ToList();
                            }
                        }
                    }

                }
                ViewBag.ProductList = productList;
                ViewBag.PrinterList = printerList;
                ViewBag.CountRows = countRows;
                ViewBag.oper = "Print";
            }
            catch
            {
                
            }
            return View(viewModels);
        }

        /// <summary>
        /// Hàm in shipping label theo seri number
        /// Đọc dữ liệu từ Viindoo API để lấy thông tin lô hàng theo seri number
        /// Sử dụng cho Astro 0004
        /// </summary>
        /// <param name="selectedPrinterID"></param>
        /// <param name="seriNumber"></param>
        /// <param name="dateCode"></param>
        /// <param name="productID"></param>
        /// <returns></returns>
        public async Task<IActionResult> PrintShippingLabelBySeriNumber(string selectedPrinterID, string seriNumber, string dateCode, int productID = 177)
        {
            SVN_Printer_InfoDataPortal printerDataPortal = new SVN_Printer_InfoDataPortal(connectionString);
            List<PrintShippingViewModel> viewModels = new List<PrintShippingViewModel>();
            List<PrinterConfigData> printerConfigData = new List<PrinterConfigData>();
            try
            {
                printerConfigData = await printerDataPortal.ReadList();
                if (printerConfigData == null)
                {
                    printerConfigData = new List<PrinterConfigData>();
                }
                SelectList printerList = new SelectList(printerConfigData, "ID_Printer", "Name_Printer");
                if (!string.IsNullOrWhiteSpace(selectedPrinterID))
                {
                    printerList = new SelectList(printerConfigData, "ID_Printer", "Name_Printer", selectedPrinterID);
                }

                if (!string.IsNullOrWhiteSpace(seriNumber))
                {
                    HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
                    ProductDataRequest dataRequest = new ProductDataRequest()
                    {
                        product_id = productID,
                        count = 0,
                        lotNumber = "",
                        seriNumber = seriNumber
                    };


                    // Lấy dữ liệu từ vindoo API
                    //var result = await httpClientHelper.PostRequest(aPIConfiguration.GetPackageBySeriURL, dataRequest, new CancellationToken(false));
                    //if (result != null)
                    //{
                    //    if (result.OK)
                    //    {
                    //        var dataUI = JsonConvert.DeserializeObject<List<stock_lotUI>>(result.Content.ToString());
                    //        if (dataUI != null)
                    //        {
                    //            dataUI = dataUI.Select(item =>
                    //            {
                    //                PrintShippingViewModel viewModel = new PrintShippingViewModel();
                    //                viewModel.lot_code = item.name;
                    //                viewModel.package_code = result.Message;
                    //                viewModels.Add(viewModel);
                    //                return item;
                    //            }).ToList();
                    //        }
                    //        ViewBag.PackageCode = result.Message;
                    //    }
                    //}

                    SVN_Label_InfoDataPortal sVN_Label_InfoDataPortal = new SVN_Label_InfoDataPortal(connectionString);
                    var labelInfoUI = sVN_Label_InfoDataPortal.ReadByScannedSerialNumber(seriNumber);
                    if (labelInfoUI != null)
                    {
                        var seriList = labelInfoUI.SerialNumbers.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                        if(seriList != null && seriList.Count > 0)
                        {
                            foreach (var seri in seriList)
                            {
                                PrintShippingViewModel viewModel = new PrintShippingViewModel();
                                viewModel.lot_code = seri;
                                viewModel.package_code = labelInfoUI.LotID;
                                viewModel.pallet_id = labelInfoUI.PalletID;
                                viewModels.Add(viewModel);
                            }
                        }
                        ViewBag.PackageCode = labelInfoUI.LotID;
                    }
                }

                ViewBag.PrinterList = printerList;
                ViewBag.oper = "Print Shipping Label";
                ViewBag.SeriNumber = seriNumber;
                ViewBag.ProductID = productID;
                ViewBag.DateCode = dateCode;
            }
            catch
            {
            }
            return View(viewModels);
        }

        /// <summary>
        /// Hàm in tem cho màn hình PrintTem
        /// </summary>
        /// <param name="requestPayload"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> Print([FromBody] PrintRequest requestPayload)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_Printer_InfoDataPortal printerDataPortal = new SVN_Printer_InfoDataPortal(connectionString);
            try
            {
                PrinterConfigData printerConfigData = new PrinterConfigData();
                printerConfigData = await printerDataPortal.ReadByID(requestPayload.PrinterID);
                processResult = toolsHelper.PrintByTCP(requestPayload.ViewModels, printerConfigData, requestPayload.Copies);
            }
            catch (Exception ex) 
            {
                processResult.Message = ex.Message;
            }
            return Json(new { message = processResult.Message });
        }

        /// <summary>
        /// Hàm in tem cho Astro 0004
        /// </summary>
        /// <param name="requestPayload"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> PrintShippingLabel([FromBody] PrintShippingRequest requestPayload)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_Printer_InfoDataPortal printerDataPortal = new SVN_Printer_InfoDataPortal(connectionString);
            try
            {
                PrinterConfigData printerConfigData = new PrinterConfigData();
                printerConfigData = await printerDataPortal.ReadByID(requestPayload.PrinterID);
                processResult = toolsHelper.PrintShippingByTCP(requestPayload.ViewModels, printerConfigData, requestPayload.Copies, requestPayload.DateCode);
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return Json(new { message = processResult.Message });
        }

        /// <summary>
        /// Hàm in nhãn Toast Label Thùng
        /// </summary>
        /// <param name="requestPayload"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> PrintToastLabelAJAX([FromBody] PrintToastLabelRequest requestPayload)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_Printer_InfoDataPortal printerDataPortal = new SVN_Printer_InfoDataPortal(connectionString);
            SVN_Label_InfoDataPortal sVN_Label_InfoDataPortal = new SVN_Label_InfoDataPortal(connectionString);

            //int countSerialNumbers = 0;
            //countSerialNumbers = requestPayload.AllSeri1.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).Count();
            //if(countSerialNumbers < 5)
            //{
            //    return Json(new { result = false, message = "Số serial ít hơn 5" });
            //}

            try
            {
                requestPayload.PartNumber = labelConfiguration.PartNumber;
                requestPayload.ModelNumber = labelConfiguration.ModelNumber;
                requestPayload.ToastPONumber = labelConfiguration.PONumber;
                requestPayload.PartDesc = labelConfiguration.PartDesc;
                requestPayload.Quantity = labelConfiguration.Quantity.ToString();
                requestPayload.LotID = labelConfiguration.LotID;
                

                List<SVN_Label_InfoUI> existingLabel = new List<SVN_Label_InfoUI>();
                int countExistingLabel = 0;
                if (!string.IsNullOrWhiteSpace(requestPayload.PalletID))
                {
                    existingLabel = sVN_Label_InfoDataPortal.ReadListByPalletID(requestPayload.PalletID);
                    if (existingLabel != null && existingLabel.Count > 0)
                    {
                        countExistingLabel = existingLabel.Sum(x => x.SerialCount);
                    }
                }

                if(countExistingLabel < 150) 
                {
                    PrinterConfigData printerConfigData = new PrinterConfigData();
                    printerConfigData = await printerDataPortal.ReadByID(requestPayload.PrinterID);
                    processResult = toolsHelper.PrintToastLabelByTCP(requestPayload, printerConfigData);
                    if (processResult.OK)
                    {
                        var existItem = sVN_Label_InfoDataPortal.ReadListBySerialNumbers(requestPayload.AllSeri1);
                        if (existItem == null)
                        {
                            if (string.IsNullOrWhiteSpace(requestPayload.PalletID))
                            {
                                requestPayload.PalletID = Guid.NewGuid().ToString();
                            }
                            // Lưu thông tin nhãn đã in vào cơ sở dữ liệu
                            SVN_Label_InfoUI labelInfo = new SVN_Label_InfoUI
                            {
                                Date = DateTime.Today.ToString("yyyyMMdd"),
                                LotID = requestPayload.LotID,
                                SerialNumbers = requestPayload.AllSeri1,
                                ScanDateTime = DateTime.Now,
                                Status = "Printed",
                                Operation = "TOAST",
                                EmployerID = "SVN0418",
                                PalletID = requestPayload.PalletID,
                                SerialCount = requestPayload.AllSeri1.Split(',').Where(x => x != "").Count(),
                                IsDelete = false
                            };

                            var labelInfos = new List<SVN_Label_InfoUI>();
                            labelInfos.Add(labelInfo);

                            var result = sVN_Label_InfoDataPortal.InsertBulk(labelInfos);
                            if (result <= 0)
                            {
                                processResult.Message = "Lưu thông tin nhãn in không thành công.";
                            }
                            else
                            {
                                processResult.OK = true;
                                processResult.Message = "In nhãn thành công và đã lưu thông tin vào cơ sở dữ liệu.";
                            }
                        }
                    }
                }
                else
                {
                    processResult.OK = false;
                    processResult.Message = "Số lượng nhãn đã in cho pallet này đã đạt giới hạn tối đa (150). Không thể in thêm.";
                }
                
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return Json(new { result = processResult.OK, palletID = requestPayload.PalletID, message = processResult.Message });
        }

        /// <summary>
        /// Hàm in nhãn Toast Label Pallet AJAX
        /// </summary>
        /// <param name="requestPayload"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> PrintPalletLabelAJAX([FromBody] PrintToastLabelRequest requestPayload)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_Printer_InfoDataPortal printerDataPortal = new SVN_Printer_InfoDataPortal(connectionString);
            SVN_Label_InfoDataPortal sVN_Label_InfoDataPortal = new SVN_Label_InfoDataPortal(connectionString);
            try
            {
                requestPayload.PartNumber = labelConfiguration.PartNumber;
                requestPayload.ModelNumber = labelConfiguration.ModelNumber;
                requestPayload.ToastPONumber = labelConfiguration.PONumber;
                requestPayload.PartDesc = labelConfiguration.PartDesc;
                requestPayload.Quantity = labelConfiguration.Quantity.ToString();
                requestPayload.LotID = labelConfiguration.LotID;
                requestPayload.PrinterID = "ZebraZT411_Toast_lastline";

                if (!string.IsNullOrWhiteSpace(requestPayload.PalletID))
                {
                    PrinterConfigData printerConfigData = new PrinterConfigData();
                    printerConfigData = await printerDataPortal.ReadByID(requestPayload.PrinterID);

                    List<SVN_Label_InfoUI> dataUI = new List<SVN_Label_InfoUI>();
                    dataUI = sVN_Label_InfoDataPortal.ReadListByPalletID(requestPayload.PalletID);
                    if (dataUI != null && dataUI.Count > 0)
                    {
                        List<SVN_Label_InfoUI> top50Item1 = new List<SVN_Label_InfoUI>();
                        List<SVN_Label_InfoUI> top50Item2 = new List<SVN_Label_InfoUI>();
                        List<SVN_Label_InfoUI> top50Item3 = new List<SVN_Label_InfoUI>();

                        List<SVN_Label_InfoUI> remainItems = new List<SVN_Label_InfoUI>();

                        top50Item1 = dataUI.Take(10).ToList();
                        if (top50Item1 != null && top50Item1.Count > 0)
                        {
                            remainItems = dataUI.Except(top50Item1).ToList();
                            if (remainItems != null && remainItems.Count > 0)
                            {
                                top50Item2 = remainItems.Take(10).ToList();
                                top50Item3 = remainItems.Except(top50Item2).ToList();
                            }
                        }

                        requestPayload.Print150Seri = true;
                        requestPayload.AllSeri1 = "";
                        requestPayload.AllSeri2 = "";
                        requestPayload.AllSeri3 = "";
                        requestPayload.Quantity = dataUI.Count.ToString();

                        if (top50Item1 != null && top50Item1.Count > 0)
                        {
                            requestPayload.AllSeri1 = string.Join("", top50Item1.Select(item => item.SerialNumbers));
                        }
                        if (top50Item2 != null && top50Item2.Count > 0)
                        {
                            requestPayload.AllSeri2 = string.Join("", top50Item2.Select(item => item.SerialNumbers));
                        }
                        if (top50Item3 != null && top50Item3.Count > 0)
                        {
                            requestPayload.AllSeri3 = string.Join("", top50Item3.Select(item => item.SerialNumbers));
                        }
                    }

                    processResult = toolsHelper.PrintToastLabelByTCP(requestPayload, printerConfigData);
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
        /// Lấy số lượng thùng trong Pallet 
        /// </summary>
        /// <param name="PalletID"></param>
        /// <returns></returns>
        [HttpPost]
        public IActionResult GetCountBoxInPallet(string PalletID)
        {
            SVN_Label_InfoDataPortal sVN_Label_InfoDataPortal = new SVN_Label_InfoDataPortal(connectionString);
            try
            {
                List<SVN_Label_InfoUI> dataUI = new List<SVN_Label_InfoUI>();
                dataUI = sVN_Label_InfoDataPortal.ReadListByPalletID(PalletID);
                if(dataUI != null && dataUI.Count > 0)
                {
                    return Json(new { result = true, boxCount = dataUI.Count });
                }
                else
                {
                    return Json(new { result = true, boxCount = 0 });
                }
            }
            catch (Exception ex)
            {
                return Json(new { result = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Kiểm tra mã seri đã tồn tại
        /// </summary>
        /// <param name="newItem"></param>
        /// <returns></returns>
        [HttpPost]
        public IActionResult GetRecordBySerialID(string newItem)
        {
            SVN_Label_InfoDataPortal dataPortal = new SVN_Label_InfoDataPortal(connectionString);
            SVN_Label_InfoUI data = new SVN_Label_InfoUI();
            try
            {
                data =  dataPortal.ReadListBySerialNumbers(newItem);
                if (data == null)
                {
                    return Json(new { ok = true, message = " Chưa tồn tại" });
                }
                return Json(new
                {
                    ok = false,
                    message = " Đã tồn tại"
                });
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                return Json(new { message = message });
            }
        }

        [HttpPost]
        public IActionResult DeleteSerialHistory(string serialList, string operation, string serachSerialID)
        {
            SVN_Label_InfoDataPortal dataPortal = new SVN_Label_InfoDataPortal(connectionString);
            SVN_Label_InfoUI data = new SVN_Label_InfoUI();
            try
            {
                if (!string.IsNullOrWhiteSpace(serachSerialID))
                {
                    data = dataPortal.ReadListBySerialNumbers(serachSerialID);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(serialList))
                    {
                        data = dataPortal.ReadListBySerialNumbers(serialList);
                    }
                    else
                    {
                        data = dataPortal.ReadFirstItem(operation);
                    }
                }


                if (data == null)
                {

                    return Json(new { ok = true, message = "Chưa tồn tại" });
                }
                else
                {
                    data.IsDelete = true;
                    List<SVN_Label_InfoUI> datas = new List<SVN_Label_InfoUI>();
                    datas.Add(data);
                    var result = dataPortal.UpdateBulk(datas);
                    if (result > 0)
                    {
                        return Json(new { ok = true, message = "Xóa thành công" });
                    }
                    else
                    {
                        return Json(new { ok = false, message = "Xóa không thành công" });
                    }
                }
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                return Json(new { message = message });
            }
        }

        [HttpPost]
        public IActionResult DeleteAstroSerialHistory(string serialList, string operation)
        {
            SVN_Label_InfoDataPortal dataPortal = new SVN_Label_InfoDataPortal(connectionString);
            SVN_Label_InfoUI data = new SVN_Label_InfoUI();
            try
            {
                if (!string.IsNullOrWhiteSpace(serialList))
                {
                    data = dataPortal.GetTop1LablebyPackageID(serialList, operation);
                }
                else
                {
                    data = dataPortal.GetTop1LableToday(operation);
                }

                if (data == null)
                {

                    return Json(new { ok = true, message = "Chưa tồn tại" });
                }
                else
                {
                    data.IsDelete = true;
                    List<SVN_Label_InfoUI> datas = new List<SVN_Label_InfoUI>();
                    datas.Add(data);
                    var result = dataPortal.UpdateBulk(datas);
                    if (result > 0)
                    {
                        return Json(new { ok = true, message = "Xóa thành công" });
                    }
                    else
                    {
                        return Json(new { ok = false, message = "Xóa không thành công" });
                    }
                }
            }
            catch (Exception ex)
            {
                string message = ex.Message;
                return Json(new { message = message });
            }
        }

        /// <summary>
        /// Màn hình in nhãn Toast Label
        /// </summary>
        /// <param name="selectedPrinterID"></param>
        /// <returns></returns>
        public async Task<IActionResult> PrintToastLabel(string selectedPrinterID)
        {
            SVN_Printer_InfoDataPortal printerDataPortal = new SVN_Printer_InfoDataPortal(connectionString);
            List<PrinterConfigData> printerConfigData = new List<PrinterConfigData>();
            try
            {
                printerConfigData = await printerDataPortal.ReadList();
                if (printerConfigData == null)
                {
                    printerConfigData = new List<PrinterConfigData>();
                }
                SelectList printerList = new SelectList(printerConfigData, "ID_Printer", "Name_Printer");
                if (!string.IsNullOrWhiteSpace(selectedPrinterID))
                {
                    printerList = new SelectList(printerConfigData, "ID_Printer", "Name_Printer", selectedPrinterID);
                }
                ViewBag.PrinterList = printerList;
            }
            catch
            {

            }
            return View();
        }

        /// <summary>
        /// Màn hình in nhãn Astro Label
        /// </summary>
        /// <param name="selectedPrinterID"></param>
        /// <returns></returns>
        public async Task<IActionResult> PrintAstroLabel(string selectedPrinterID)
        {
            SVN_Printer_InfoDataPortal printerDataPortal = new SVN_Printer_InfoDataPortal(connectionString);
            List<PrinterConfigData> printerConfigData = new List<PrinterConfigData>();
            try
            {
                printerConfigData = await printerDataPortal.ReadList();
                if (printerConfigData == null)
                {
                    printerConfigData = new List<PrinterConfigData>();
                }
                SelectList printerList = new SelectList(printerConfigData, "ID_Printer", "Name_Printer");
                if (!string.IsNullOrWhiteSpace(selectedPrinterID))
                {
                    printerList = new SelectList(printerConfigData, "ID_Printer", "Name_Printer", selectedPrinterID);
                }
                ViewBag.PrinterList = printerList;

                SVN_Label_InfoDataPortal sVN_Label_InfoDataPortal = new SVN_Label_InfoDataPortal(connectionString);
                var dataUI = sVN_Label_InfoDataPortal.GetTop1LableToday("Astro");
                if (dataUI != null)
                {
                    string packageID = (Int128.Parse(dataUI.LotID) + 1).ToString();
                    ViewBag.LotID = packageID;
                }
                else
                {
                    string packageID = DateTime.Now.ToString("yyyyMMdd") + "00001";
                    ViewBag.LotID = packageID;
                }

            }
            catch
            {

            }
            return View();
        }

        /// <summary>
        /// Hàm in nhãn Toast Label Thùng
        /// </summary>
        /// <param name="requestPayload"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> PrintAstroLabelAJAX([FromBody] PrintToastLabelRequest requestPayload)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_Printer_InfoDataPortal printerDataPortal = new SVN_Printer_InfoDataPortal(connectionString);
            SVN_Label_InfoDataPortal sVN_Label_InfoDataPortal = new SVN_Label_InfoDataPortal(connectionString);
            try
            {
                //requestPayload.PartNumber = labelConfiguration.PartNumber;
                //requestPayload.ModelNumber = labelConfiguration.ModelNumber;
                //requestPayload.ToastPONumber = labelConfiguration.PONumber;
                //requestPayload.PartDesc = labelConfiguration.PartDesc;
                //requestPayload.Quantity = labelConfiguration.Quantity.ToString();
                //requestPayload.LotID = labelConfiguration.LotID;


                List<SVN_Label_InfoUI> existingLabel = new List<SVN_Label_InfoUI>();
                int countExistingLabel = 0;
                PrinterConfigData printerConfigData = new PrinterConfigData();
                printerConfigData = await printerDataPortal.ReadByID(requestPayload.PrinterID);

                List<PrintShippingViewModel> viewModels = new List<PrintShippingViewModel>();
                if(!string.IsNullOrWhiteSpace(requestPayload.LotID) && !string.IsNullOrWhiteSpace(requestPayload.AllSeri1))
                {
                    var listSeries = requestPayload.AllSeri1.Split(',').Where(x => !string.IsNullOrWhiteSpace(x)).Select(x =>
                    {
                        PrintShippingViewModel viewModel = new PrintShippingViewModel
                        {
                            lot_code = x,
                            package_code = requestPayload.LotID
                        };
                        viewModels.Add(viewModel);
                        return x;
                    }).ToList();
                }

                if(viewModels.Count > 0)
                {
                    processResult = toolsHelper.PrintShippingByTCP(viewModels, printerConfigData, requestPayload.Copies, requestPayload.PartDesc);
                    if (processResult.OK)
                    {
                        var existItem = sVN_Label_InfoDataPortal.ReadListBySerialNumbers(requestPayload.AllSeri1);
                        if (existItem == null)
                        {
                            if (string.IsNullOrWhiteSpace(requestPayload.PalletID))
                            {
                                requestPayload.PalletID = Guid.NewGuid().ToString();
                            }
                            // Lưu thông tin nhãn đã in vào cơ sở dữ liệu
                            SVN_Label_InfoUI labelInfo = new SVN_Label_InfoUI
                            {
                                Date = DateTime.Today.ToString("yyyyMMdd"),
                                LotID = requestPayload.LotID,
                                SerialNumbers = requestPayload.AllSeri1,
                                ScanDateTime = DateTime.Now,
                                Status = "Printed",
                                Operation = "Astro",
                                EmployerID = "SVN0418",
                                PalletID = requestPayload.PalletID,
                                SerialCount = requestPayload.AllSeri1.Split(',').Where(x => x != "").Count(),
                                IsDelete = false
                            };

                            var labelInfos = new List<SVN_Label_InfoUI>();
                            labelInfos.Add(labelInfo);

                            var result = sVN_Label_InfoDataPortal.InsertBulk(labelInfos);
                            if (result <= 0)
                            {
                                processResult.Message = "Lưu thông tin nhãn in không thành công.";
                            }
                            else
                            {
                                processResult.OK = true;
                                processResult.Message = "In nhãn thành công và đã lưu thông tin vào cơ sở dữ liệu.";
                            }
                        }
                    }
                }
                else
                {
                    processResult.OK = false;
                    processResult.Message = "Chưa nhập package id hoặc chưa có số seri để in";
                }


            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return Json(new { result = processResult.OK, palletID = requestPayload.PalletID, message = processResult.Message });
        }

        /// <summary>
        /// Kiểm tra và cập nhật Package ID cho Astro Label
        /// </summary>
        /// <param name="packageID"></param>
        /// <returns></returns>
        [HttpPost]
        public IActionResult UpdatePackageID(string packageID)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_Label_InfoDataPortal sVN_Label_InfoDataPortal = new SVN_Label_InfoDataPortal(connectionString);
            try
            {
                var dataUI = sVN_Label_InfoDataPortal.GetTop1LablebyPackageID(packageID, "Astro");
                if(dataUI != null)
                {
                    packageID = (Int128.Parse(dataUI.LotID) + 1).ToString();
                    processResult.OK = true;
                    return Json(new { result = processResult.OK, packageID = packageID, message = "Package ID đã được cập nhật thành công." });
                }
                else
                {
                    processResult.OK = false;
                    processResult.Message = "Package ID chưa được in";
                    return Json(new { result = processResult.OK, message = processResult.Message });
                }
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
                return Json(new { result = processResult.OK, message = processResult.Message });
            }
            
        }

        public async Task<IActionResult> GetImageBySelectedLot(string itemName, string itemCode, decimal qty, string printID)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            SVN_Printer_InfoDataPortal printerDataPortal = new SVN_Printer_InfoDataPortal(connectionString);
            try
            {
                PrinterConfigData printerConfigData = new PrinterConfigData();
                printerConfigData = await printerDataPortal.ReadByID(printID);

                PrintTemViewModel viewModel = new PrintTemViewModel();
                viewModel.item_name = itemName;
                viewModel.lot_code = itemCode;
                viewModel.product_qty = qty;

                processResult = await toolsHelper.GetImageFromImage(viewModel, printerConfigData.Size, printerConfigData.ZPL_Temp);
            }
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return Json(new { message = processResult.Message });
        }

        public IActionResult ProductionUpdateQty()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ProductionUpdateQty(string productionOrderCode, int productionOrderQuantity)
        {
            ViewBag.WorkOrder = productionOrderCode;
            ViewBag.Quantity = productionOrderQuantity;
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);

            ProductDataRequest dataRequest = new ProductDataRequest()
            {
                product_id = 0,
                lotNumber = "",
                count = productionOrderQuantity,
                seriNumber = productionOrderCode
            };

            var result = await httpClientHelper.PostRequest(aPIConfiguration.InputProductionByWorkOrderURL, dataRequest, new CancellationToken(false));
            if (result != null)
            {
                if(result.OK)
                {
                    ViewBag.IsSuccess = "OK";
                }
                else
                {
                    ViewBag.IsSuccess = "NOTOK";
                }
                ViewBag.Message = result.Message;
            }
            else
            {
                ViewBag.IsSuccess = "NOTOK";
                ViewBag.Message = "Lỗi API không phản hồi";
            }
                return View();
            
        }

        #region Nhập kết quả sản xuất
        public IActionResult WorkOrderInfo(string workOrder)
        {
            if(!string.IsNullOrWhiteSpace(workOrder))
            {
                workOrder = workOrder.Replace("%2f", "/");
            }
            ViewBag.MasterWorkOrder = workOrder;
            return View();
        }

        public IActionResult WorkOrderInfoV1(string workOrder, string operation)
        {
            if (!string.IsNullOrWhiteSpace(workOrder))
            {
                workOrder = workOrder.Replace("%2f", "/");
            }
            ViewBag.MasterWorkOrder = workOrder;
            ViewBag.Operation = operation;
            return View();
        }

        public IActionResult WorkOrderInfoWIPWalter(string workOrder)
        {
            if (!string.IsNullOrWhiteSpace(workOrder))
            {
                workOrder = workOrder.Replace("%2f", "/");
            }
            ViewBag.MasterWorkOrder = workOrder;
            return View();
        }

        /// <summary>
        /// Hàm nhập kết quả sản xuất theo Work Order
        /// </summary>
        /// <param name="workOrderCode"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> GetProductByWorkOrder(string workOrderCode)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
            try
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
                        string previousWorkOrderName = TempData.Peek("WorkOrderName") as string;
                        WorkOrderInfo workOrderInfo = JsonConvert.DeserializeObject<WorkOrderInfo>(result.Content.ToString());
                        string stringContent = BuildWorkOrderInfo(workOrderInfo, previousWorkOrderName);
                        processResult.OK = true;
                        processResult.Message = stringContent;
                        return Json(new { result = processResult.OK, message = processResult.Message, product_tracking = workOrderInfo.OrderInfo["product_tracking"] });
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
                    processResult.Message = "Không có dữ liệu";
                }
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return Json(new { result = processResult.OK, message = processResult.Message, content = processResult.Content });
        }

        /// <summary>
        /// Hàm nhập kết quả sản xuất theo Work Order
        /// </summary>
        /// <param name="workOrderCode"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> GetProductByWorkOrderV2(string workOrderCode)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
            try
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
                        if (result.Content == null)
                        {
                            processResult.OK = false;
                            processResult.Message = "Lệnh sản xuất nhập thất bại";
                            return Json(new { result = processResult.OK, message = processResult.Message });
                        }
                        string previousWorkOrderName = TempData.Peek("WorkOrderName") as string;
                        string itemCode = "";
                        WorkOrderInfo workOrderInfo = JsonConvert.DeserializeObject<WorkOrderInfo>(result.Content.ToString());

                        //Lưu số lượng sản xuất còn lại
                        TempData.Remove("RemainQty");
                        TempData["RemainQty"] = workOrderInfo.OrderInfo["product_qty"] as string;
                        TempData.Keep("RemainQty");

                        var appSettingDataPortal = new SVN_AppSettingDataPortal(connectionString);
                        var operInfo1 = await appSettingDataPortal.GetOperInfoConfig();
                        List<OperInfo> opers = operInfo1.OperInfo;

                        //List<OperInfo> opers = operInfoConfig.OperInfo;
                        var currentOper = opers.Where(x => x.Produce_id != null && x.Produce_id.Contains(int.Parse(workOrderInfo.OrderInfo["product_id"]))).FirstOrDefault();
                        if (currentOper != null)
                        {
                            itemCode = currentOper.Operation.Split("-").Count() > 1 ? currentOper.Operation.Split("-")[1] : "";
                        }

                        string stringContent = BuildWorkOrderInfo(workOrderInfo, previousWorkOrderName);
                        processResult.OK = true;
                        processResult.Message = stringContent;
                        return Json(new { result = processResult.OK, message = processResult.Message, product_tracking = workOrderInfo.OrderInfo["product_tracking"], itemCode = itemCode });
                    }
                    else
                    {
                        processResult.OK = false;
                        processResult.Message = "Không có dữ liệu";
                    }

                }
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return Json(new { result = processResult.OK, message = processResult.Message, content = processResult.Content });
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
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
            try
            {
                ProductDataRequest dataRequest = new ProductDataRequest()
                {
                    lotNumber = serial,
                    seriNumber = "",
                    product_id = int.Parse(productID)
                };
                var result = await httpClientHelper.PostRequest("api/ViindooConnect/SearchSerialLotFG", dataRequest, new CancellationToken(false));
                if (result != null)
                {
                    processResult.OK = result.OK;
                    processResult.Message = result.Message;
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
        /// Hàm kiểm tra số seri đã dùng để tiêu hao cho lệnh sản xuất khác chưa
        /// </summary>
        /// <param name="workOrderCode"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> CheckLotSerialComponemt(string serial, string productId, string masterMOName)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
            try
            {
                ProductDataRequest dataRequest = new ProductDataRequest()
                {
                    lotNumber = serial,
                    seriNumber = masterMOName,
                    product_id = int.Parse(productId)
                };
                var result = await httpClientHelper.PostRequest("api/ViindooConnect/GetUsedLotForComponemt", dataRequest, new CancellationToken(false));
                if (result != null)
                {
                    processResult.OK = result.OK;
                    processResult.Message = result.Message;
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
            SVN_Scan_Code_InfoDataPortal dataPortal = new SVN_Scan_Code_InfoDataPortal(connectionString);
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

        private string BuildWorkOrderInfo(WorkOrderInfo workOrderInfo, string previousWorkOrderName)
        {
            string masterWorkOrder = workOrderInfo.OrderInfo["name"].Split("-")[0];
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"col-12\">");
            sb.Append("<div class=\"form-group\">");
            if (!string.IsNullOrWhiteSpace(previousWorkOrderName))
            {
                if(workOrderInfo.OrderInfo["name"] != previousWorkOrderName)
                {
                    sb.Append("<div class=\"alert alert-success\" role=\"alert\">");
                    sb.Append("Lệnh " + previousWorkOrderName + " nhập kết quả sản xuất thành công");
                    sb.Append("</div>");
                }
                else
                {
                    sb.Append("<div class=\"alert alert-warning\" role=\"alert\">");
                    sb.Append("Lệnh " + previousWorkOrderName + " nhập kết quả không thành công, yêu cầu check lại hệ thống MES");
                    sb.Append("</div>");
                } 
            }
            sb.Append("<h1 class=\"control-label\">Lệnh sản xuất: " + workOrderInfo.OrderInfo["name"] + "</h1>");
            sb.Append("<input type=\"hidden\" name=\"Name\" class=\"form-control\" value=\"" + masterWorkOrder + "\" />");
            sb.Append("<input type=\"hidden\" name=\"SubName\" class=\"form-control\" value=\"" + workOrderInfo.OrderInfo["name"] + "\" />");
            sb.Append("<input type=\"hidden\" name=\"ProductID\" class=\"form-control\" value=\"" + workOrderInfo.OrderInfo["product_id"] + "\" />");
            sb.Append("<input type=\"hidden\" name=\"ProductTracking\" class=\"form-control\" value=\"" + workOrderInfo.OrderInfo["product_tracking"] + "\" />");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("<div class=\"col-12\">");
            sb.Append("<div class=\"form-group\">");
            sb.Append("<h2 class=\"control-label\">Sản phẩm: " + workOrderInfo.OrderInfo["product_name"] + "</h2>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("<div class=\"col-12 col-md-3\">");
            sb.Append("<div class=\"form-group\">");
            sb.Append("<div class=\"row\">");
            sb.Append("<div class=\"col-3\">");
            sb.Append("<label class=\"control-label\">Số lượng:</label>");
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
            if (workOrderInfo.OrderInfo["product_tracking"] == "serial")
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
            sb.Append("<label class=\"control-label\">Số seri:</label>");
            sb.Append("</div>");
            sb.Append("<div class=\"col-10\">");
            sb.Append("<input type=\"text\" name=\"Serial\" class=\"form-control\" />");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");
            
            sb.Append("<div class=\"col-12\">");
            sb.Append("<table class=\"table\">");
            sb.Append("<thead>");
            sb.Append("<tr>");
            sb.Append("<th scope=\"col\">Sản phẩm</th>");
            sb.Append("<th scope=\"col\">Từ</th>");
            sb.Append("<th scope=\"col\">Số seri</th>");
            sb.Append("</tr>");
            sb.Append("</thead>");
            sb.Append("<tbody>");

            workOrderInfo.StockMoveInfo = workOrderInfo.StockMoveInfo.OrderByDescending(x => x["has_tracking"]).ToList();
            foreach (var item in workOrderInfo.StockMoveInfo)
            {
                sb.Append("<tr>");
                sb.Append("<th scope=\"row\">" + item["product_name"] + "</th>");
                sb.Append("<td>" + item["location_name"] + "</td>");
                if(item["has_tracking"] == "serial")
                {
                    sb.Append("<td><input type=\"hidden\" class=\"form-control product-id\" value=\"" + item["product_id"] + "\" /><input type=\"hidden\" class=\"form-control\" value=\"" + item["has_tracking"] + "\" /><input type=\"text\" class=\"form-control serial-input\" /></td>");
                }
                else
                {
                    sb.Append("<td><input type=\"hidden\" class=\"form-control product-id\" value=\"" + item["product_id"] + "\" /><input type=\"hidden\" class=\"form-control\" value=\"" + item["has_tracking"] + "\" /><input type=\"hidden\" class=\"form-control\" />Không áp dụng</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</tbody>");
            sb.Append("</table>");
            sb.Append("<div class=\"col-12\">");
            sb.Append("<div class=\"form-group\" style=\"margin-top:33px\">");
            sb.Append("<button type=\"button\" class=\"btn btn-primary\" onclick=\"InputProductionResult()\">Xác nhận</button>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");
            return sb.ToString();
        }

        public IActionResult Test()
        {
            return View();
        }

        public async Task<IActionResult> InputProductionResult([FromBody] ProductionData data)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
            try
            {
                List<LotScanedRequest> lotScaneds = new List<LotScanedRequest>();
                data.Products = data.Products.Where(x => x.Has_tracking == "serial").Select(y =>
                {
                    LotScanedRequest lotScaned = new LotScanedRequest
                    {
                        product_id = y.Product_id,
                        lotNumber = y.Serial_code
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
                var result = await httpClientHelper.PostRequest(aPIConfiguration.InputProductionByWorkOrderv1URL, dataRequest, new CancellationToken(false));
                if (result != null)
                {
                    TempData.Remove("WorkOrderName");
                    TempData["WorkOrderName"] = data.SubName;
                    TempData.Keep("WorkOrderName");
                    if (result.OK)
                    {
                        string operation = "";
                        InputProductDataRequest dataWORequest = new InputProductDataRequest()
                        {
                            WorkOrderNumber = data.Name.Split("-")[0],
                            LotNumber = ""
                        };
                        //var woResult = await httpClientHelper.PostRequest("api/ViindooConnect/GetWorkOrder", dataRequest, new CancellationToken(false));
                        //if (woResult != null) 
                        //{
                        //    if (woResult.OK) 
                        //    {
                        //        WorkOrderInfo workOrderInfo = JsonConvert.DeserializeObject<WorkOrderInfo>(woResult.Content.ToString());
                        //        if(workOrderInfo.OrderInfo["name"] == data.Name)
                        //        {
                        //            processResult.OK = false;
                        //            processResult.Message = "Lệnh sản xuất nhập thất bại";
                        //            return Json(new { result = processResult.OK, message = processResult.Message });
                        //        }
                        //        List<OperInfo> opers = operInfoConfig.OperInfo;
                        //        var currentOper = opers.Where(x => x.Produce_id.Contains(int.Parse(workOrderInfo.OrderInfo["product_id"]))).FirstOrDefault();
                        //        if (currentOper != null)
                        //        {
                        //            operation = currentOper.MasterOperation;
                        //        }
                        //    }
                        //    else
                        //    {
                        //        return Json(new { result = woResult.OK, message = woResult.Message });
                        //    }
                        //}
                        //else
                        //{
                        //    processResult.OK = false;
                        //    processResult.Message = "Lỗi mạng, không lấy được thông tin lệnh sản xuất";
                        //    return Json(new { result = processResult.OK, message = processResult.Message });
                        //}

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
            catch(Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return Json(new { success = false, message = processResult.Message });
        }

        public async Task<IActionResult> InputProductionResultV1([FromBody] ProductionData data)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            LogService logger = new LogService(connectionString);
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
            try
            {
                List<LotScanedRequest> lotScaneds = new List<LotScanedRequest>();
                data.Products = data.Products.Where(x => x.Has_tracking == "serial").Select(y =>
                {
                    LotScanedRequest lotScaned = new LotScanedRequest
                    {
                        product_id = y.Product_id,
                        lotNumber = y.Serial_code
                    };
                    lotScaneds.Add(lotScaned);
                    return y;
                }).ToList();

                InputProductDataRequest dataRequest = new InputProductDataRequest();

                int quatity = int.Parse(data.Quantity);

                //Lấy ra số lượng còn lại
                var strRemainQty = TempData.Peek("RemainQty") as string;
                if (!string.IsNullOrWhiteSpace(strRemainQty))
                {
                    int remainQty = 0;
                    try
                    {
                        remainQty = int.Parse(strRemainQty);
                    }
                    catch
                    {

                    }
                    if(quatity >= remainQty)
                    {
                        quatity = remainQty;
                        dataRequest.IsLastOrder = true;
                    }
                }

                dataRequest.WorkOrderNumber = data.Name;
                dataRequest.LotNumber = data.Serial;
                dataRequest.Quality = quatity;
                dataRequest.LotScaneds = lotScaneds;


                var result = await httpClientHelper.PostRequest(aPIConfiguration.InputProductionByWorkOrderv1URL, dataRequest, new CancellationToken(false));
                if (result != null)
                {
                    TempData.Remove("WorkOrderName");
                    TempData["WorkOrderName"] = data.SubName;
                    TempData.Keep("WorkOrderName");
                    if (result.OK)
                    {
                        string operation = "";
                        InputProductDataRequest dataWORequest = new InputProductDataRequest()
                        {
                            WorkOrderNumber = data.Name.Split("-")[0],
                            LotNumber = ""
                        };
                        if(dataRequest.IsLastOrder == false)
                        {
                            var woResult = await httpClientHelper.PostRequest("api/ViindooConnect/GetWorkOrder", dataRequest, new CancellationToken(false));
                            if (woResult != null)
                            {
                                if (woResult.OK)
                                {
                                    if (woResult.Content == null)
                                    {
                                        processResult.OK = false;
                                        processResult.Message = "Lệnh sản xuất nhập thất bại, kiểm tra MES";
                                        logger.Log(LogApp.SVNPortal, LogAction.AutoInputProduction, LogType.Error, "Lệnh sản xuất " + data.SubName + " nhập thất bại: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm") + " | Error detail: " + processResult.Message);

                                        //_logger.LogError("Lệnh sản xuất " + data.Name + " nhập thất bại: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm") + " | Error detail: " + processResult.Message);

                                        return Json(new { result = processResult.OK, message = processResult.Message });
                                    }
                                    WorkOrderInfo workOrderInfo = JsonConvert.DeserializeObject<WorkOrderInfo>(woResult.Content.ToString());
                                    if (workOrderInfo.OrderInfo["name"] == data.SubName && dataRequest.IsLastOrder == false)
                                    {
                                        processResult.OK = false;
                                        processResult.Message = "Lệnh đã được nhập trước đó";
                                        logger.Log(LogApp.SVNPortal, LogAction.AutoInputProduction, LogType.Error, "Lệnh sản xuất " + data.SubName + " nhập thất bại: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm") + " | Error detail: " + processResult.Message);
                                        //_logger.LogError("Lệnh sản xuất " + data.Name + " nhập thất bại: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm") + " | Error detail: " + processResult.Message);

                                        return Json(new { result = processResult.OK, message = processResult.Message });
                                    }

                                    var appSettingDataPortal = new SVN_AppSettingDataPortal(connectionString);
                                    var operInfo1 = await appSettingDataPortal.GetOperInfoConfig();
                                    List<OperInfo> opers = operInfo1.OperInfo;

                                    //List<OperInfo> opers = operInfoConfig.OperInfo;
                                    var currentOper = opers.Where(x => x.Produce_id != null && x.Produce_id.Contains(int.Parse(workOrderInfo.OrderInfo["product_id"]))).FirstOrDefault();
                                    if (currentOper != null)
                                    {
                                        operation = currentOper.MasterOperation;
                                    }
                                }
                                else
                                {
                                    return Json(new { result = woResult.OK, message = woResult.Message });
                                }
                            }
                            else
                            {
                                processResult.OK = false;
                                processResult.Message = "Lỗi mạng, không lấy được thông tin lệnh sản xuất";
                                logger.Log(LogApp.SVNPortal, LogAction.AutoInputProduction, LogType.Error, "Lệnh sản xuất " + data.SubName + " nhập thất bại: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm") + " | Error detail: " + processResult.Message);
                                //_logger.LogError("Lệnh sản xuất " + data.Name + " nhập thất bại: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm") + " | Error detail: " + processResult.Message);

                                return Json(new { result = processResult.OK, message = processResult.Message });
                            }
                        }

                        if (string.IsNullOrWhiteSpace(operation))
                        {
                            operation = "Walter";
                        }
                        

                        processResult.OK = true;

                        logger.Log(LogApp.SVNPortal, LogAction.AutoInputProduction, LogType.Info, "Lệnh sản xuất " + data.SubName + " nhập thành công: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm"));
                        //_logger.LogInformation("Lệnh sản xuất " + data.Name + " nhập thành công: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm"));

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
            catch (Exception ex)
            {
                processResult.Message = ex.Message;

                logger.Log(LogApp.SVNPortal, LogAction.AutoInputProduction, LogType.Error, "Lệnh sản xuất " + data.SubName + " nhập thất bại: " + DateTime.Now.ToString("dd/MM/yyyy hh:mm") + " | Error detail: " + processResult.Message);
            }
            return Json(new { success = false, message = processResult.Message });
        }

        /// <summary>
        /// Nhập WIP cho Walter
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task<IActionResult> InputProductionResultWIPWalter([FromBody] ProductionData data)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
            try
            {
                List<LotScanedRequest> lotScaneds = new List<LotScanedRequest>();
                data.Products = data.Products.Where(x => x.Has_tracking == "serial").Select(y =>
                {
                    LotScanedRequest lotScaned = new LotScanedRequest
                    {
                        product_id = y.Product_id,
                        lotNumber = y.Serial_code
                    };
                    lotScaneds.Add(lotScaned);
                    return y;
                }).ToList();

                if(int.Parse(data.Quantity) > 20)
                {
                    data.Quantity = 20.ToString();
                }

                InputProductDataRequest dataRequest = new InputProductDataRequest()
                {
                    WorkOrderNumber = data.Name,
                    LotNumber = data.Serial,
                    Quality = int.Parse(data.Quantity),
                    LotScaneds = lotScaneds
                };
                var result = await httpClientHelper.PostRequest(aPIConfiguration.InputProductionByWorkOrderv1URL, dataRequest, new CancellationToken(false));
                if (result != null)
                {
                    TempData.Remove("WorkOrderName");
                    TempData["WorkOrderName"] = data.SubName;
                    TempData.Keep("WorkOrderName");
                    if (result.OK)
                    {
                        string operation = "";
                        InputProductDataRequest dataWORequest = new InputProductDataRequest()
                        {
                            WorkOrderNumber = data.Name.Split("-")[0],
                            LotNumber = ""
                        };
                        //var woResult = await httpClientHelper.PostRequest("api/ViindooConnect/GetWorkOrder", dataRequest, new CancellationToken(false));
                        //if (woResult != null) 
                        //{
                        //    if (woResult.OK) 
                        //    {
                        //        WorkOrderInfo workOrderInfo = JsonConvert.DeserializeObject<WorkOrderInfo>(woResult.Content.ToString());
                        //        if(workOrderInfo.OrderInfo["name"] == data.Name)
                        //        {
                        //            processResult.OK = false;
                        //            processResult.Message = "Lệnh sản xuất nhập thất bại";
                        //            return Json(new { result = processResult.OK, message = processResult.Message });
                        //        }
                        //        List<OperInfo> opers = operInfoConfig.OperInfo;
                        //        var currentOper = opers.Where(x => x.Produce_id.Contains(int.Parse(workOrderInfo.OrderInfo["product_id"]))).FirstOrDefault();
                        //        if (currentOper != null)
                        //        {
                        //            operation = currentOper.MasterOperation;
                        //        }
                        //    }
                        //    else
                        //    {
                        //        return Json(new { result = woResult.OK, message = woResult.Message });
                        //    }
                        //}
                        //else
                        //{
                        //    processResult.OK = false;
                        //    processResult.Message = "Lỗi mạng, không lấy được thông tin lệnh sản xuất";
                        //    return Json(new { result = processResult.OK, message = processResult.Message });
                        //}

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
            catch (Exception ex)
            {
                processResult.Message = ex.Message;
            }
            return Json(new { success = false, message = processResult.Message });
        }

        #endregion

        #region Check Item tồn tại trong Bom

        public IActionResult CheckBOMFollowOrder()
        {
            return View();
        }

        /// <summary>
        /// Hàm nhập kết quả sản xuất theo Work Order
        /// </summary>
        /// <param name="workOrderCode"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> GetProductByWorkOrderV1(string workOrderCode)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
            try
            {
                TempData.Remove("CorrectItems");

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
                        WorkOrderInfo workOrderInfo = JsonConvert.DeserializeObject<WorkOrderInfo>(result.Content.ToString());
                        string stringContent = BuildWorkOrderInfoV1(workOrderInfo);
                        processResult.OK = true;
                        processResult.Message = stringContent;
                        return Json(new { result = processResult.OK, message = processResult.Message, product_tracking = workOrderInfo.OrderInfo["product_tracking"] });
                    }
                    else
                    {
                        processResult.OK = false;
                        processResult.Message = "Không có dữ liệu";
                    }

                }
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return Json(new { result = processResult.OK, message = processResult.Message, content = processResult.Content });
        }

        private string BuildWorkOrderInfoV1(WorkOrderInfo workOrderInfo)
        {
            List<string> correctItems = new List<string>();

            string masterWorkOrder = workOrderInfo.OrderInfo["name"].Split("-")[0];
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class=\"col-3\">");
            sb.Append("<div class=\"card svn-card\">");
            sb.Append("<div class=\"card-title\">");
            sb.Append("</div>");
            sb.Append("<div class=\"card-body\">");
            sb.Append("<div id=\"divResultLight\" class=\"box-square bg-light\">");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("<div class=\"col-12 col-md-9\">");
            sb.Append("<div class=\"card svn-card\">");
            sb.Append("<div class=\"card-title\">");
            sb.Append("</div>");
            sb.Append("<div class=\"card-body\">");
            sb.Append("<div class=\"col-12\">");
            sb.Append("<div class=\"form-group\">");
            sb.Append("<h3 class=\"control-label\">Lệnh sản xuất: " + masterWorkOrder + "</h3>");
            sb.Append("<input type=\"hidden\" name=\"Name\" class=\"form-control\" value=\"" + masterWorkOrder + "\" />");
            sb.Append("<input type=\"hidden\" name=\"SubName\" class=\"form-control\" value=\"" + workOrderInfo.OrderInfo["name"] + "\" />");
            sb.Append("<input type=\"hidden\" name=\"ProductID\" class=\"form-control\" value=\"" + workOrderInfo.OrderInfo["product_id"] + "\" />");
            sb.Append("<input type=\"hidden\" name=\"ProductTracking\" class=\"form-control\" value=\"" + workOrderInfo.OrderInfo["product_tracking"] + "\" />");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("<div class=\"col-12\">");
            sb.Append("<div class=\"form-group\">");
            sb.Append("<h4 class=\"control-label\">Sản phẩm: " + workOrderInfo.OrderInfo["product_name"] + "</h4>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("<div class=\"col-12 col-md-3\">");
            sb.Append("<div class=\"form-group\">");
            sb.Append("<div class=\"row\">");
            sb.Append("<div class=\"col-3 d-none\">");
            sb.Append("<label class=\"control-label\">Số lượng:</label>");
            sb.Append("</div>");
            sb.Append("<div class=\"col-4 d-none\">");
            sb.Append("<input type=\"text\" name=\"Quantity\" class=\"form-control\" />");
            sb.Append("</div>");
            sb.Append("<div class=\"col-5 d-none\">");
            sb.Append("/" + workOrderInfo.OrderInfo["product_qty"]);
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("<div class=\"col-12 col-md-3 d-none\">");
            sb.Append("<div class=\"form-group\">");
            sb.Append("<div class=\"row\">");
            sb.Append("<div class=\"col-2\">");
            sb.Append("<label class=\"control-label\">Số seri:</label>");
            sb.Append("</div>");
            sb.Append("<div class=\"col-10\">");
            sb.Append("<input type=\"text\" name=\"Serial\" class=\"form-control\" />");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");

            sb.Append("<div class=\"col-12\">");
            sb.Append("<table class=\"table\">");
            sb.Append("<thead>");
            sb.Append("<tr>");
            sb.Append("<th scope=\"col\">Thành phần</th>");
            sb.Append("<th scope=\"col\">Trạng thái</th>");
            sb.Append("</tr>");
            sb.Append("</thead>");
            sb.Append("<tbody>");

            workOrderInfo.StockMoveInfo = workOrderInfo.StockMoveInfo.OrderByDescending(x => x["has_tracking"]).ToList();
            foreach (var item in workOrderInfo.StockMoveInfo)
            {
                correctItems.Add(item["product_name"]);
                sb.Append("<tr>");
                sb.Append("<th scope=\"row\">" + item["product_name"] + "</th>");
                sb.Append("<td></td>");
                sb.Append("</tr>");
            }
            TempData["CorrectItems"] = string.Join("|", correctItems);
            TempData.Keep("CorrectItems");

            sb.Append("</tbody>");
            sb.Append("</table>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");
            
            return sb.ToString();
        }

        /// <summary>
        /// Kiểm tra Mã có tồn tại trong BOM của workorder không
        /// </summary>
        /// <param name="workOrderCode"></param>
        /// <param name="wipcode"></param>
        /// <returns></returns>
        public async Task<IActionResult> CheckItemFollowBOM(string workOrderCode, string wipcode)
        {
            BODataProcessResult processResult = new BODataProcessResult();
            HttpClientHelper<BODataProcessResult> httpClientHelper = new HttpClientHelper<BODataProcessResult>(aPIConfiguration.BaseURL, 1000);
            SetLightRequest setLightRequest = new SetLightRequest();
            setLightRequest.IP = "192.168.2.203";
            setLightRequest.Port = 55443;
            try
            {
                string correctItems = TempData.Peek("CorrectItems") as string;
                List<string> correctItemList = new List<string>();
                if (!string.IsNullOrWhiteSpace(correctItems))
                {
                    correctItemList = correctItems.Split('|').ToList();
                }
                else
                {
                    correctItemList = new List<string>();
                }
                var exitsData = correctItemList.FirstOrDefault(x => x.Contains(wipcode));
                if (exitsData != null)
                {
                    //setLightRequest.Color = Color.Green.Name;
                    //await httpClientHelper.PostRequest("api/YeelightService/SetColor", setLightRequest, new CancellationToken(false));

                    processResult.OK = true;
                    processResult.Message = "Khớp";
                }
                else
                {
                    //setLightRequest.Color = "Red";
                    //await httpClientHelper.PostRequest("api/YeelightService/SetBlinkColor", setLightRequest, new CancellationToken(false));
                    processResult.OK = false;
                    processResult.Message = "Không khớp";
                }
            }
            catch (Exception ex)
            {
                //setLightRequest.Color = "Red";
                //await httpClientHelper.PostRequest("api/YeelightService/SetColor", setLightRequest, new CancellationToken(false));
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return Json(new { result = processResult.OK, message = processResult.Message, content = processResult.Content });
        }

        #endregion
    }

    public class PrintRequest
    {
        public List<PrintTemViewModel> ViewModels { get; set; }
        public int Copies { get; set; }
        public string PrinterID { get; set; }
    }

    public class PrintShippingRequest
    {
        public List<PrintShippingViewModel> ViewModels { get; set; }
        public int Copies { get; set; }
        public string PrinterID { get; set; }
        public string DateCode { get; set; }
    }

    public class PrintToastLabelRequest 
    {
        public string PartNumber { get; set; }
        public string ModelNumber { get; set; }
        public string ToastPONumber { get; set; }
        public string Quantity { get; set; }
        public string LotID { get; set; }
        public string PartDesc { get; set; }
        public bool Print150Seri { get; set; }
        public string AllSeri1 { get; set; }
        public string AllSeri2 { get; set; }
        public string AllSeri3 { get; set; }
        public int Copies { get; set; }
        public string PrinterID { get; set; }
        public string PalletID { get; set; }
    }
}
