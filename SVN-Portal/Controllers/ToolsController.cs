using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Newtonsoft.Json;
using SVN_Portal.DAL.DataPortal;
using SVN_Portal.Models;
using SVN_Portal.Services.Configurations;
using System.Threading.Tasks;

namespace SVN_Portal.Controllers
{
    public class ToolsController : Controller
    {
        DBConfiguration dBConfiguration;
        string connectionString;
        public ToolsController(DBConfiguration dBConfiguration)
        {
            this.dBConfiguration = dBConfiguration;
            connectionString = dBConfiguration.GetConnectionString();
        }
        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> PrintTem(int selectedProductID, int countRows) 
        {
            SVN_product_productDataPortal productDataPortal = new SVN_product_productDataPortal(connectionString);
            List<PrintTemViewModel> viewModels = new List<PrintTemViewModel>();
            var products = await productDataPortal.ReadList();
            products = products.Select(product =>
            {
                Dictionary<string, string> dictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(product.product_name);
                product.product_name = dictionary["vi_VN"];
                return product;
            }).ToList();
            SelectList productList = new SelectList(products, "id", "product_name");
            if(selectedProductID != 0)
            {
                productList = new SelectList(products, "id", "product_name", selectedProductID);

                SVN_stock_lotDataPortal lotDataPortal = new SVN_stock_lotDataPortal(connectionString);
                var dataUI = await lotDataPortal.ReadListByProductID(selectedProductID, countRows);
                if (dataUI != null) 
                {
                    dataUI = dataUI.Select(item => 
                    {
                        PrintTemViewModel viewModel = new PrintTemViewModel();
                        Dictionary<string, string> dictionary = JsonConvert.DeserializeObject<Dictionary<string, string>>(item.item_name);
                        viewModel.item_name = dictionary["vi_VN"];
                        viewModel.lot_code = item.lot_code;
                        viewModel.product_qty = item.product_qty;
                        viewModels.Add(viewModel);
                        return item;
                    }).ToList();
                }

            }
            ViewBag.ProductList = productList;
            ViewBag.CountRows = countRows;
            return View(viewModels);
        }
    }
}
