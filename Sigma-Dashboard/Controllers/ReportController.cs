using Microsoft.AspNetCore.Mvc;
using Sigma_Dashboard.Models;
using Sigma_Dashboard.Services.Helpers;
using System.Threading.Tasks;

namespace Sigma_Dashboard.Controllers
{
    public class ReportController : Controller
    {
        ReportControllerHelper controllerHelper;
        public ReportController(ReportControllerHelper controllerHelper)
        {
            this.controllerHelper = controllerHelper;
        }
        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> DailyProductionResultReport(DateTime fromDate, DateTime toDate, string itemType, string companyCode)
        {
            List<PDResultDailyViewModel> viewModels = new List<PDResultDailyViewModel>();
            try
            {
                if (fromDate == DateTime.MinValue)
                {
                    fromDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                }
                if (toDate == DateTime.MinValue)
                {
                    toDate = DateTime.Now.Date.AddDays(-1).AddHours(23).AddMinutes(59);
                }

                if (fromDate.Date > DateTime.Now.Date)
                {
                    fromDate = DateTime.Now.Date;
                }

                if (toDate.Date > DateTime.Now.Date)
                {
                    toDate = DateTime.Now.Date.AddHours(23).AddMinutes(59);
                }

                if (fromDate.Date > toDate.Date)
                {
                    fromDate = toDate;
                    toDate = toDate.Date.AddHours(23).AddMinutes(59);
                }
                else if (fromDate.Date == toDate.Date)
                {
                    toDate = toDate.Date.AddHours(23).AddMinutes(59);
                }

                ViewBag.FromDate = fromDate;
                ViewBag.ToDate = toDate;
                ViewBag.ItemType = itemType;

                ViewBag.date = DateTime.Now;
                ViewBag.CompanyCode = companyCode;
                ViewBag.TitleName = "Daily Production Result Report";

                ViewBag.ControllerName = "Report";
                ViewBag.IndexPage = "DailyProductionResultReport";
                ViewBag.AccessMode = "PMC";

                viewModels = await controllerHelper.GetDataForReport(fromDate, toDate, itemType);
            }
            catch
            {
                viewModels = new List<PDResultDailyViewModel>();
            }
            return View(viewModels);
        }
    }
}
