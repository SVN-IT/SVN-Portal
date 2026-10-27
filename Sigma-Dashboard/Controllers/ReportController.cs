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

                if(toDate.Date == DateTime.Now.Date)
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
                var viewModelDetails = await controllerHelper.GetDailyResultDetails(fromDate, toDate);
                if(viewModelDetails != null)
                {
                    viewModelDetails = viewModelDetails.OrderBy(x => x.Project).ThenByDescending(x => x.Date_time).ToList();
                }

                //Xử lý chia dữ liệu theo công ty
                if (!string.IsNullOrEmpty(companyCode))
                {
                    if (companyCode.Equals("SVN", StringComparison.OrdinalIgnoreCase))
                    {
                        // Với SVN: Lọc các dòng KHÔNG chứa ngoặc tròn (nghĩa là không thuộc các cty SM, BT, ITA...)
                        viewModels = viewModels
                            .Where(x => !string.IsNullOrEmpty(x.OperationActive) && !x.OperationActive.Contains("("))
                            .ToList();

                        if (viewModelDetails != null)
                        {
                            viewModelDetails = viewModelDetails
                            .Where(x => !string.IsNullOrEmpty(x.PartNumber) && !x.PartNumber.Contains("("))
                            .ToList();
                        }
                    }
                    else
                    {
                        // Các công ty bình thường: Lọc theo Contains mã companyCode
                        viewModels = viewModels
                            .Where(x => !string.IsNullOrEmpty(x.OperationActive) && x.OperationActive.Contains(companyCode, StringComparison.OrdinalIgnoreCase))
                            .ToList();

                        if (viewModelDetails != null)
                        {
                            viewModelDetails = viewModelDetails
                                .Where(x => !string.IsNullOrEmpty(x.PartNumber) && x.PartNumber.Contains(companyCode, StringComparison.OrdinalIgnoreCase))
                                .ToList();
                        }
                    }
                }

                ViewBag.DailyResultDetails = viewModelDetails;
            }
            catch
            {
                viewModels = new List<PDResultDailyViewModel>();
            }
            return View(viewModels);
        }
    }
}
