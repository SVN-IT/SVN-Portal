using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Sigma_Dashboard.Models;
using Sigma_Dashboard.Services.Configurations;
using Sigma_Dashboard.Services.Helpers;

namespace Sigma_Dashboard.Controllers;

public class HomeController : Controller
{
    AppConfig appConfig;
    HomeControllerHelper homeControllerHelper;
    public HomeController(HomeControllerHelper homeControllerHelper, AppConfig appConfig)
    {
        this.homeControllerHelper = homeControllerHelper;
        this.appConfig = appConfig;
    }

    public async Task<IActionResult> Index(DateTime date, string shift = "Day", string companyCode = "SVN", bool isManualLoad = false)
    {
        DashboardViewModel dashboardData = new DashboardViewModel();
        try
        {
            if (string.IsNullOrWhiteSpace(companyCode))
            {
                companyCode = appConfig.DefaultCompany;
            }
            string storedProceduce = "SVN_Pro_CalTarget_Viindoo";
            string strdate = "20241220";
            string tableName = "SVN_Production_result_Viindoo";
            if (date == DateTime.MinValue)
            {
                date = DateTime.Now;
            }

            if (!isManualLoad)
            {
                if (date.Hour >= 20)
                {
                    shift = "Night";
                }
                else
                {
                    shift = "Day";
                }
            }

            ViewBag.date = date;
            ViewBag.shift = shift;
            ViewBag.IndexPage = "Index";
            strdate = date.ToString("yyyyMMdd");

            int hours = 7;
            ViewBag.CompanyCode = companyCode;
            TempData.Remove("Hours");
            TempData["Hours"] = hours.ToString();
            TempData.Keep("Hours");

            dashboardData = await homeControllerHelper.SummaryData(strdate, storedProceduce, tableName, 3, shift, companyCode, hours);

        }
        catch 
        {
            dashboardData = new DashboardViewModel();
        }
        return View(dashboardData);
    }

    public async Task<IActionResult> ChartPageByOperation(string masterOperation, DateTime date, string shift = "Day", string companyCode = "SVN", bool isManualLoad = false)
    {
        DashboardViewModel dashboardData = new DashboardViewModel();
        try
        {
            if (string.IsNullOrWhiteSpace(companyCode))
            {
                companyCode = appConfig.DefaultCompany;
            }
            string storedProceduce = "SVN_Pro_CalTarget_Viindoo";
            string strdate = "20241220";
            string tableName = "SVN_Production_result_Viindoo";
            if (date == DateTime.MinValue)
            {
                date = DateTime.Now;
            }
            if (!isManualLoad)
            {
                if (date.Hour >= 20)
                {
                    shift = "Night";
                }
                else
                {
                    shift = "Day";
                }
            }
            ViewBag.date = date;
            ViewBag.shift = shift;
            ViewBag.IndexPage = "ChartPageByOperation";
            strdate = date.ToString("yyyyMMdd");
            int hours = 7;
            ViewBag.CompanyCode = companyCode;
            ViewBag.SelectedMasterOperation = masterOperation;
            TempData.Remove("Hours");
            TempData["Hours"] = hours.ToString();
            TempData.Keep("Hours");
            dashboardData = await homeControllerHelper.SummaryData(strdate, storedProceduce, tableName, 3, shift, companyCode, hours, masterOperation);
        }
        catch
        {
            dashboardData = new DashboardViewModel();
        }
        return View(dashboardData);
    }

    [HttpGet]
    public async Task<IActionResult> GetFullDashboardData(DateTime? date, string shift = "Day", string companyCode = "SVN", bool isManualLoad = true, string masterOperation = "All")
    {
        // 1. Xử lý logic mặc định giống hệt trong ảnh
        DateTime searchDate = date ?? DateTime.Now;

        if (string.IsNullOrWhiteSpace(companyCode))
            companyCode = "SVN";

        if (!isManualLoad)
        {
            shift = (searchDate.Hour >= 20) ? "Night" : "Day";
        }

        string strdate = searchDate.ToString("yyyyMMdd");
        string storedProcedure = "SVN_Pro_CalTarget_Viindoo";
        string tableName = "SVN_Production_result_Viindoo";
        int hours = 7;

        // 2. Gọi Helper để lấy dữ liệu cho ViewModel
        DashboardViewModel model = await homeControllerHelper.SummaryData(strdate, storedProcedure, tableName, 3, shift, companyCode, hours, masterOperation);

        // dùng để test
        //foreach (var item in model.BarChartData)
        //{
        //    if (item.Operation == "Injection_POP")
        //    {
        //        item.ActualData = [100, 0, 0, 0, 0];
        //    }
        //    if(item.Operation == "Astro-WSS04-60001")
        //    {
        //        item.ActualData = [400, 0, 0, 0, 0];
        //    }
        //}

        // 3. Trả về JSON
        return Json(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
