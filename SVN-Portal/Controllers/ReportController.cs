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
        public IActionResult WOAnalisisReport(DateTime fromDate, DateTime toDate)
        {
            List<workOrderInfoUI> workOrderModels = new List<workOrderInfoUI>();

            var dataPortal = new mrp_productionDataPortal(connectionString);
            workOrderModels = dataPortal.GetWorkOrderInfo();
            return View();
        }
        #endregion
    }
}
