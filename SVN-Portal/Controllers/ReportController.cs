using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SVN_Portal.Services.Configurations;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
using System.Threading.Tasks;

namespace SVN_Portal.Controllers
{
    public class ReportController : Controller
    {
        DBConfiguration dBConfiguration;
        string connectionString;
        public ReportController(DBConfiguration dBConfiguration)
        {
            this.dBConfiguration = dBConfiguration;
            connectionString = dBConfiguration.GetConnectionString();
        }
        public IActionResult Index()
        {
            return View();
        }

        #region FN
        public async Task<IActionResult> WOAnalisisReport(DateTime fromDate, DateTime toDate)
        {
            List<WorkOrderModel> workOrderModels = new List<WorkOrderModel>();

            var dataPortal = new SVN_SearchedData_ERPDataPortal(connectionString);
            string firstDayOfMonth = string.Empty;
            string lastDayOfMonth = string.Empty;

            List<SVN_SearchedData_ERPUI> customsearch799DataCountNotNull = new List<SVN_SearchedData_ERPUI>();
            List<SVN_SearchedData_ERPUI> customsearch803DataCountNotNull = new List<SVN_SearchedData_ERPUI>();
            if (fromDate == DateTime.MinValue || toDate == DateTime.MinValue)
            {
                DateTime now = DateTime.Now;
                firstDayOfMonth = new DateTime(now.Year, now.Month, 1).ToString("yyyyMMdd");
                lastDayOfMonth = new DateTime(now.Year, now.Month, 1).AddMonths(1).AddDays(-1).ToString("yyyyMMdd");
            }
            else
            {
                firstDayOfMonth = fromDate.ToString("yyyyMMdd");
                lastDayOfMonth = toDate.ToString("yyyyMMdd");
            }

            var customsearch799Data = await dataPortal.GetERPSearchedData("customsearch799", firstDayOfMonth, lastDayOfMonth);
            foreach(var item in customsearch799Data)
            {
                if (!string.IsNullOrWhiteSpace(item.Data))
                {
                    try
                    {
                        var obj = JsonConvert.DeserializeObject<dynamic>(item.Data);
                        int count = obj.count;
                        if (count > 0)
                        {
                            customsearch799DataCountNotNull.Add(item);
                        }
                    }
                    catch
                    {
                    }
                }
            }

            var customsearch803Data = await dataPortal.GetERPSearchedData("customsearch803", firstDayOfMonth, lastDayOfMonth);
            foreach (var item in customsearch803Data)
            {
                if (!string.IsNullOrWhiteSpace(item.Data))
                {
                    try
                    {
                        var obj = JsonConvert.DeserializeObject<dynamic>(item.Data);
                        int count = obj.count;
                        if (count > 0)
                        {
                            customsearch803DataCountNotNull.Add(item);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            return View();
        }
        #endregion
    }
}
