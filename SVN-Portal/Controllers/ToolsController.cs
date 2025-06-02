using Lextm.SharpSnmpLib.Messaging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
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
using System.Threading.Tasks;

namespace SVN_Portal.Controllers
{
    public class ToolsController : Controller
    {
        DBConfiguration dBConfiguration;
        string connectionString;
        ToolsHelper toolsHelper;
        APIConfiguration aPIConfiguration;
        TOASTLabelConfiguration labelConfiguration;
        public ToolsController(DBConfiguration dBConfiguration, 
            ToolsHelper toolsHelper, 
            APIConfiguration aPIConfiguration, 
            TOASTLabelConfiguration labelConfiguration)
        {
            this.dBConfiguration = dBConfiguration;
            connectionString = dBConfiguration.GetConnectionString();
            this.toolsHelper = toolsHelper;
            this.aPIConfiguration = aPIConfiguration;
            this.labelConfiguration = labelConfiguration;
        }
        public IActionResult Index()
        {
            return View();
        }

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

                    if(product.product_name.Contains("vi_VN"))
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
                        seriNumber = seriNumber
                    };
                    var result = await httpClientHelper.PostRequest(aPIConfiguration.GetPackageBySeriURL, dataRequest, new CancellationToken(false));
                    if (result != null)
                    {
                        if (result.OK)
                        {
                            var dataUI = JsonConvert.DeserializeObject<List<stock_lotUI>>(result.Content.ToString());
                            if (dataUI != null)
                            {
                                dataUI = dataUI.Select(item =>
                                {
                                    PrintShippingViewModel viewModel = new PrintShippingViewModel();
                                    viewModel.lot_code = item.name;
                                    viewModel.package_code = result.Message;
                                    viewModels.Add(viewModel);
                                    return item;
                                }).ToList();
                            }
                            ViewBag.PackageCode = result.Message;
                        }
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

        [HttpPost]
        public async Task<IActionResult> PrintToastLabelAJAX([FromBody] PrintToastLabelRequest requestPayload)
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

                        top50Item1 = dataUI.Take(50).ToList();
                        if (top50Item1 != null && top50Item1.Count > 0)
                        {
                            remainItems = dataUI.Except(top50Item1).ToList();
                            if (remainItems != null && remainItems.Count > 0)
                            {
                                top50Item2 = remainItems.Take(50).ToList();
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
        public IActionResult DeleteSerialHistory(string allSerial)
        {
            SVN_Label_InfoDataPortal dataPortal = new SVN_Label_InfoDataPortal(connectionString);
            SVN_Label_InfoUI data = new SVN_Label_InfoUI();
            try
            {
                data = dataPortal.ReadListBySerialNumbers(allSerial);
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
        public IActionResult ProductionUpdateQty(string workOrderCode)
        {
            ViewBag.WorkOrder = workOrderCode;
            return View();
            
        }

        public IActionResult Test()
        {
            return View();
        }
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
