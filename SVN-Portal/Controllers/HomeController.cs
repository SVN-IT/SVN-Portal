using Microsoft.AspNetCore.Mvc;
using SVN_Portal.DAL.DataPortal;
using SVN_Portal.DAL.DTO;
using SVN_Portal.Models;
using SVN_Portal.Services.Configurations;
using System.Diagnostics;

namespace SVN_Portal.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        AppConfig appConfig;
        DBConfiguration dBConfiguration;
        string connectionString;
        QCInfoConfig qCInfoConfig;
        OperInfoConfig operInfoConfig;

        public HomeController(ILogger<HomeController> logger, 
            AppConfig appConfig, 
            DBConfiguration dBConfiguration,
            OperInfoConfig operInfoConfig,
            QCInfoConfig qCInfoConfig)
        {
            _logger = logger;
            this.appConfig = appConfig;
            this.dBConfiguration = dBConfiguration;
            connectionString = dBConfiguration.GetConnectionString();
            this.qCInfoConfig = qCInfoConfig;
            this.operInfoConfig = operInfoConfig;
        }

        public async Task<IActionResult> Index(DateTime date)
        {
            List<QtyProdResultByOperViewModel> models = new List<QtyProdResultByOperViewModel>();
            try
            {
                string storedProceduce = "SVN_Pro_CalTarget";
                string strdate = "20241220";
                string tableName = "SVN_Production_result";
                if (date == DateTime.MinValue) 
                { 
                    date = DateTime.Now;
                }
                ViewBag.date = date;
                strdate = date.ToString("yyyyMMdd");
                List<string> opers = appConfig.OperList.Split(",").ToList();
                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryData(strdate, opers, storedProceduce, tableName);
                if (models.Count > 0) 
                {
                    foreach (var model in models) 
                    { 
                        var userInfo = qCInfoConfig.UserInfo.FirstOrDefault(x => x.Operation == model.Operation);
                        if (userInfo != null) 
                        {
                            model.PDName = userInfo.PDName;
                            model.QCName = userInfo.QCName;
                        }
                    }
                }
                return View(models);
            }
            catch (Exception ex) 
            {
                QtyProdResultByOperViewModel model = new QtyProdResultByOperViewModel();
                var data1 = model.GetData("POP");
                var data2 = model.GetData("eKIT");
                var data3 = model.GetData("Solar");
                var data4 = model.GetData("Injection");
                models = new List<QtyProdResultByOperViewModel> { data1, data2, data3, data4 };
                return View(models);

            }
        }

        public async Task<IActionResult> ProductionResult(DateTime date)
        {
            List<QtyProdResultByOperViewModel> models = new List<QtyProdResultByOperViewModel>();
            try
            {
                string storedProceduce = "SVN_Pro_CalTarget_Viindoo";
                string strdate = "20241220";
                string tableName = "SVN_Production_result_Viindoo";
                if (date == DateTime.MinValue)
                {
                    date = DateTime.Now;
                }
                ViewBag.date = date;
                strdate = date.ToString("yyyyMMdd");
                List<string> opers = appConfig.OperList.Split(",").ToList();
                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryData(strdate, opers, storedProceduce, tableName);
                if (models.Count > 0)
                {
                    foreach (var model in models)
                    {
                        var userInfo = qCInfoConfig.UserInfo.FirstOrDefault(x => x.Operation == model.Operation);
                        if (userInfo != null)
                        {
                            model.PDName = userInfo.PDName;
                            model.QCName = userInfo.QCName;
                        }
                    }
                }
                return View(models);
            }
            catch (Exception ex)
            {
                QtyProdResultByOperViewModel model = new QtyProdResultByOperViewModel();
                var data1 = model.GetData("POP");
                var data2 = model.GetData("eKIT");
                var data3 = model.GetData("Solar");
                var data4 = model.GetData("Injection");
                models = new List<QtyProdResultByOperViewModel> { data1, data2, data3, data4 };
                return View(models);

            }
        }

        // Defect rate 20250103

        public async Task<IActionResult> Defect_Rate(DateTime date)
        {
            List<SVN_Defect_record> models = new List<SVN_Defect_record>();
            try
            {
                string strdate = "20241220";
                if (date == DateTime.MinValue)
                {
                    date = DateTime.Now;
                }
                ViewBag.date = date;
                strdate = date.ToString("yyyyMMdd");
                List<string> opers = appConfig.OperList.Split(",").ToList();
                var dataPortal = new SVN_Defect_recordDataPortal(connectionString);
                models = await dataPortal.ReadList(strdate);
                return View(models);
            }
            catch (Exception ex)
            {
                
                return View(models);

            }
        }



        public async Task<IActionResult> ChartPerOper(DateTime date, string oper)
        {
            List<QtyProdResultByOperViewModel> models = new List<QtyProdResultByOperViewModel>();
            try
            {
                string strdate = "20241220";
                string storedProceduce = "SVN_Pro_CalTarget";
                string tableName = "SVN_Production_result";
                if (date == DateTime.MinValue)
                {
                    date = DateTime.Now;
                }
                ViewBag.date = date;
                ViewBag.oper = oper;
                strdate = date.ToString("yyyyMMdd");
                List<string> opers = appConfig.OperList.Split(",").ToList();
                opers = opers.Where(x => x == oper).ToList();
                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryData(strdate, opers, storedProceduce, tableName);
                if (models.Count > 0)
                {
                    foreach (var model in models)
                    {
                        var userInfo = qCInfoConfig.UserInfo.FirstOrDefault(x => x.Operation == model.Operation);
                        if (userInfo != null)
                        {
                            model.PDName = userInfo.PDName;
                            model.QCName = userInfo.QCName;
                        }
                    }
                }
                return View(models);
            }
            catch (Exception ex)
            {
                QtyProdResultByOperViewModel model = new QtyProdResultByOperViewModel();
                var data = model.GetData(oper);
                models = new List<QtyProdResultByOperViewModel> { data };
                return View(models);

            }
        }

        public async Task<IActionResult> ProductionResultNew(DateTime date)
        {
            List<QtyProdResultByOperViewModel> models = new List<QtyProdResultByOperViewModel>();
            try
            {
                string storedProceduce = "SVN_Pro_CalTarget_Viindoo";
                string strdate = "20241220";
                string tableName = "SVN_Production_result_Viindoo";
                if (date == DateTime.MinValue)
                {
                    date = DateTime.Now;
                }
                ViewBag.date = date;
                strdate = date.ToString("yyyyMMdd");

                List<OperInfo> opers = new List<OperInfo>();

                foreach (var item in operInfoConfig.OperInfo)
                {
                    if(!string.IsNullOrWhiteSpace(item.WC))
                    {
                        List<string> WCs = item.WC.Split(",").ToList();
                        foreach (var wc in WCs)
                        {
                            opers.Add(new OperInfo { Operation = item.Operation, WC = wc, ColWidth = item.ColWidth });
                        }
                    }
                    else
                    {
                        opers.Add(new OperInfo { Operation = item.Operation, WC = "", ColWidth = item.ColWidth });
                    }
                }

                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryData_Viindoo(strdate, opers, storedProceduce, tableName);
                if (models.Count > 0)
                {
                    foreach (var model in models)
                    {
                        var userInfo = qCInfoConfig.UserInfo.FirstOrDefault(x => x.Operation == model.Operation);
                        var operInfo = operInfoConfig.OperInfo.FirstOrDefault(x => x.Operation == model.Operation);
                        if (userInfo != null)
                        {
                            model.PDName = userInfo.PDName;
                            model.QCName = userInfo.QCName;
                        }
                        if (operInfo != null)
                        {
                            model.ColWidth = operInfo.ColWidth;
                        }
                    }
                }
                return View(models);
            }
            catch (Exception ex)
            {
                QtyProdResultByOperViewModel model = new QtyProdResultByOperViewModel();
                var data1 = model.GetData("POP");
                var data2 = model.GetData("eKIT");
                var data3 = model.GetData("Solar");
                var data4 = model.GetData("Injection");
                models = new List<QtyProdResultByOperViewModel> { data1, data2, data3, data4 };
                return View(models);

            }
        }

        public async Task<IActionResult> ChartPerOperNew(DateTime date, string oper)
        {
            List<QtyProdResultByOperViewModel> models = new List<QtyProdResultByOperViewModel>();
            try
            {
                string storedProceduce = "SVN_Pro_CalTarget_Viindoo";
                string strdate = "20241220";
                string tableName = "SVN_Production_result_Viindoo";
                if (date == DateTime.MinValue)
                {
                    date = DateTime.Now;
                }
                ViewBag.date = date;
                ViewBag.oper = oper;
                strdate = date.ToString("yyyyMMdd");

                List<OperInfo> opers = new List<OperInfo>();
                var singleOper = operInfoConfig.OperInfo.Where(x => x.Operation == oper).ToList();
                foreach (var item in singleOper)
                {
                    if (!string.IsNullOrWhiteSpace(item.WC))
                    {
                        List<string> WCs = item.WC.Split(",").ToList();
                        foreach (var wc in WCs)
                        {
                            opers.Add(new OperInfo { Operation = item.Operation, WC = wc, ColWidth = item.ColWidth });
                        }
                    }
                    else
                    {
                        opers.Add(new OperInfo { Operation = item.Operation, WC = "", ColWidth = item.ColWidth });
                    }
                }

                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryData_Viindoo(strdate, opers, storedProceduce, tableName);
                if (models.Count > 0)
                {
                    foreach (var model in models)
                    {
                        var userInfo = qCInfoConfig.UserInfo.FirstOrDefault(x => x.Operation == model.Operation);
                        var operInfo = operInfoConfig.OperInfo.FirstOrDefault(x => x.Operation == model.Operation);
                        if (userInfo != null)
                        {
                            model.PDName = userInfo.PDName;
                            model.QCName = userInfo.QCName;
                        }
                        if (operInfo != null)
                        {
                            model.ColWidth = operInfo.ColWidth;
                        }
                    }
                }
                return View(models);
            }
            catch (Exception ex)
            {
                QtyProdResultByOperViewModel model = new QtyProdResultByOperViewModel();
                var data = model.GetData(oper);
                models = new List<QtyProdResultByOperViewModel> { data };
                return View(models);

            }
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
}
