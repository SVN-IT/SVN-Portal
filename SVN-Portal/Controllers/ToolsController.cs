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
        public ToolsController(DBConfiguration dBConfiguration, ToolsHelper toolsHelper, APIConfiguration aPIConfiguration)
        {
            this.dBConfiguration = dBConfiguration;
            connectionString = dBConfiguration.GetConnectionString();
            this.toolsHelper = toolsHelper;
            this.aPIConfiguration = aPIConfiguration;
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
                    if(product.product_name.Contains("vi_VN"))
                    {
                        Dictionary<string, string> dictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(product.product_name);
                        product.product_name = dictionary["vi_VN"];
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
                        count = countRows
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
}
