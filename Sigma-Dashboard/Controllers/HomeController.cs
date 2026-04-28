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
