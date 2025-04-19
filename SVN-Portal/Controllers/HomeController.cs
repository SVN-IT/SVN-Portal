using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SVN_Portal.DAL.DataPortal;
using SVN_Portal.DAL.DTO;
using SVN_Portal.Models;
using SVN_Portal.Services.Configurations;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
using System.Diagnostics;
using System.Reflection;
using System.Text;

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
                    if(item.WC != null && item.WC.Count > 0)
                    {
                        foreach (var wc in item.WC)
                        {
                            opers.Add(new OperInfo { Operation = item.Operation, WCName = wc.WCName, Produce_id = wc.Produce_id, Top_row = wc.Top_row, ColWidth = item.ColWidth });
                        }
                    }
                    else
                    {
                        opers.Add(new OperInfo { Operation = item.Operation, WCName = "", Produce_id = new List<int>(), ColWidth = item.ColWidth });
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
                    if (item.WC != null && item.WC.Count > 0)
                    {
                        foreach (var wc in item.WC)
                        {
                            opers.Add(new OperInfo { Operation = item.Operation, WCName = wc.WCName, Produce_id = wc.Produce_id, Top_row = wc.Top_row, ColWidth = item.ColWidth });
                        }
                    }
                    else
                    {
                        opers.Add(new OperInfo { Operation = item.Operation, WCName = "", Produce_id = new List<int>(), ColWidth = item.ColWidth });
                    }
                }

                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryData_Viindoo(strdate, opers, storedProceduce, tableName);
                
                if (models!= null && models.Count > 0)
                {
                    models = models.OrderBy(x => x.WC).ToList();
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

        public async Task<IActionResult> ChartInfoPerOper(DateTime date, string oper)
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
                    if (item.WC != null && item.WC.Count > 0)
                    {
                        foreach (var wc in item.WC)
                        {
                            opers.Add(new OperInfo { Operation = item.Operation, WCName = wc.WCName, Produce_id = wc.Produce_id, Top_row = wc.Top_row, ColWidth = item.ColWidth });
                        }
                    }
                    else
                    {
                        opers.Add(new OperInfo { Operation = item.Operation, WCName = "", Produce_id = new List<int>(), ColWidth = item.ColWidth });
                    }
                }

                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryData_Viindoo(strdate, opers, storedProceduce, tableName);

                //Lấy danh sách thiết bị
                List<SVN_Equipment_InfoUI> equipments = new List<SVN_Equipment_InfoUI>();
                string equipmentTable = "SVN_Equipment_Info";
                GrandDataPortal<SVN_Equipment_InfoUI> grandDataPortal = new GrandDataPortal<SVN_Equipment_InfoUI>(equipmentTable, connectionString);
                string query = "SELECT * FROM SVN_Equipment_Info WHERE Operation = @Operation";
                var param = new { Operation = oper };
                equipments = grandDataPortal.GetListData(query, param);
                ViewBag.Equipments = equipments;

                if (models != null && models.Count > 0)
                {
                    models = models.OrderBy(x => x.WC).ToList();
                    foreach (var model in models)
                    {
                        var userInfo = qCInfoConfig.UserInfo.FirstOrDefault(x => x.Operation == model.Operation);
                        var operInfo = operInfoConfig.OperInfo.FirstOrDefault(x => x.Operation == model.Operation);
                        if (userInfo != null)
                        {
                            model.PDName = userInfo.PDName;
                            model.QCName = userInfo.QCName;
                            model.PDURL = userInfo.PDURL;
                            model.QCURL = userInfo.QCURL;
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

        [HttpPost]
        public async Task<IActionResult> GetDataByOperAndWC(string date, string oper, string wc)
        {
            QtyProdResultByOperViewModel model = new QtyProdResultByOperViewModel();
            string strProductionResultTable = string.Empty;
            string strTargetTable = string.Empty;

            try
            {
                string storedProceduce = "SVN_Pro_CalTarget_Viindoo";
                string tableName = "SVN_Production_result_Viindoo";
                OperInfo operInfo = new OperInfo();
                operInfo = operInfoConfig.OperInfo.FirstOrDefault(x => x.Operation == oper);
                if(operInfo != null)
                {
                    if (!string.IsNullOrWhiteSpace(wc))
                    {
                        operInfo.WCName = wc;
                    }
                }
                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                model = await dataPortal.GetDataByOperAndWC(date, operInfo, storedProceduce, tableName);

                //sử dụng stringBuilder để build lại 2 table
                if (model != null) 
                {
                    strProductionResultTable = BuildProductionResultTable(model);
                    strTargetTable = BuildTargetTable(model);
                }
                return new JsonResult(new { result = true, productionResultTable = strProductionResultTable, 
                    targetTable = strTargetTable, pdmodel = JsonConvert.SerializeObject(model.ViewModels),
                    achieve = model.Achieve,
                    forecast = model.Forecast,
                    woRunning = model.WORunning,
                    product = model.Product,
                    customer = model.Customer
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { result = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetDataByOperAndWCMainDashBoard(string date, string oper, string wc)
        {
            QtyProdResultByOperViewModel model = new QtyProdResultByOperViewModel();
            string strProductionResultTable = string.Empty;
            string strTargetTable = string.Empty;

            try
            {
                string storedProceduce = "SVN_Pro_CalTarget_Viindoo";
                string tableName = "SVN_Production_result_Viindoo";
                OperInfo operInfo = new OperInfo();
                operInfo = operInfoConfig.OperInfo.FirstOrDefault(x => x.Operation == oper);
                if (operInfo != null)
                {
                    if (!string.IsNullOrWhiteSpace(wc))
                    {
                        operInfo.WCName = wc;
                    }
                }
                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                model = await dataPortal.GetDataByOperAndWC(date, operInfo, storedProceduce, tableName);

                //sử dụng stringBuilder để build lại 2 table
                if (model != null)
                {
                    strProductionResultTable = BuildProductionResultTable(model);
                    strTargetTable = BuildTargetTable(model);
                }
                return new JsonResult(new
                {
                    result = true,
                    productionResultTable = strProductionResultTable,
                    targetTable = strTargetTable,
                    pdmodel = JsonConvert.SerializeObject(model.ViewModels),
                    achieve = model.Achieve,
                    forecast = model.Forecast,
                    woRunning = model.WORunning,
                    product = model.Product,
                    customer = model.Customer
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new { result = false, message = ex.Message });
            }
        }

        private string BuildProductionResultTable(QtyProdResultByOperViewModel model)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<div class='col-12 border table-cell text-center'>");
            sb.Append("<strong>Production result: " + model.Operation + "</strong>");
            sb.Append("</div>");
            sb.Append("<div class='col-2'>");
            sb.Append(" <div class='row'>");
            sb.Append("<div class='col-12 border table-cell text-center'>");
            sb.Append("<strong>Time</strong>");
            sb.Append("</div>");
            sb.Append("<div class='col-12 border table-cell text-center'>");
            sb.Append("<strong>Target</strong>");
            sb.Append("</div>");
            sb.Append("<div class='col-12 border table-cell text-center'>");
            sb.Append("<strong>Line</strong>");
            sb.Append("</div>");
            sb.Append("<div class='col-12 border table-cell text-center'>");
            sb.Append("<strong>Labor</strong>");
            sb.Append("</div>");
            sb.Append("<div class='col-12 border table-cell text-center'>");
            sb.Append("<strong>Defect</strong>");
            sb.Append("</div>");
            sb.Append("</div>");
            sb.Append("</div>");
            foreach(var item in model.ViewModels)
            {
                sb.Append("<div class='col-2'>");
                sb.Append("<div class='row'>");
                sb.Append("<div class='col-12 border table-cell text-center'>");
                sb.Append(item.Time);
                sb.Append("</div>");
                sb.Append("<div class='col-12 border table-cell text-center'>");
                sb.Append(Math.Round(item.Target, 2));
                sb.Append("</div>");
                sb.Append("<div class='col-12 border table-cell text-center'>");
                sb.Append(Math.Round(item.Line, 2));
                sb.Append("</div>");
                sb.Append("<div class='col-12 border table-cell text-center'>");
                sb.Append(Math.Round(item.ManQuantity, 2));
                sb.Append("</div>");
                sb.Append("<div class='col-12 border table-cell text-center'>");
                sb.Append(Math.Round(item.NG, 2));
                sb.Append("</div>");
                sb.Append("</div>");
                sb.Append("</div>");
            }
            return sb.ToString();
        }

        private string BuildTargetTable(QtyProdResultByOperViewModel model) 
        {
            StringBuilder sb = new StringBuilder();
            if((!string.IsNullOrWhiteSpace(model.WC) && model.WC.Contains("FG")) || appConfig.ShowSingleChart.Contains(model.Operation))
            {
                sb.Append("<div class='col-3 border table-cell text-center'><strong>Item</strong></div>");
                sb.Append("<div class='col-2 border table-cell text-center'><strong>Target</strong></div>");
                sb.Append("<div class='col-2 border table-cell text-center'><strong>Current</strong></div>");
                sb.Append("<div class='col-3 border table-cell text-center'><strong>Rate</strong></div>");
                sb.Append("<div class='col-2 border table-cell text-center'><strong>Status</strong></div>");
                foreach(var item in model.TargetViewModels)
                {
                    string status = string.Empty;
                    sb.Append("<div class='col-3 border table-cell text-center'><strong>" + item.Item + "</strong></div>");
                    if(item.Item == "Defect")
                    {
                        sb.Append("<div class='col-2 border table-cell text-center'>" + Math.Round(item.Target, 2) + " %</div>");
                        sb.Append("<div class='col-2 border table-cell text-center'>" + Math.Round(item.Current, 2) + " %</div>");
                        sb.Append("<div class='col-3 border table-cell text-center'>" + Math.Round(item.Percent, 2) + " %</div>");
                        if (item.Percent > 100)
                        {
                            status = "bg-danger";
                        }
                        else if (item.Percent > 75 && item.Percent <= 100)
                        {
                            status = "bg-warning";
                        }
                        else
                        {
                            status = "bg-primary";
                        }
                    }
                    else
                    {
                        sb.Append("<div class='col-2 border table-cell text-center'>" + Math.Round(item.Target, 2) + "</div>");
                        sb.Append("<div class='col-2 border table-cell text-center'>" + Math.Round(item.Current, 2) + "</div>");
                        sb.Append("<div class='col-3 border table-cell text-center'>" + Math.Round(item.Percent, 2) + " %</div>");
                        if (item.Percent > 0 && item.Percent <= 75)
                        {
                            status = "bg-danger";
                        }
                        else if (item.Percent > 75 && item.Percent <= 92)
                        {
                            status = "bg-warning";
                        }
                        else
                        {
                            status = "bg-primary";
                        }
                    }
                    sb.Append("<div class='col-2 border table-cell text-center " + status + "'></div>");
                }

            }
            else
            {
                sb.Append("<div class='col-4 border table-cell text-center'>");
                sb.Append("<strong>WO Name</strong>");
                sb.Append("</div>");
                sb.Append("<div class='col-4 border table-cell text-center'>");
                sb.Append("<strong>State</strong>");
                sb.Append("</div>");
                sb.Append("<div class='col-4 border table-cell text-center'>");
                sb.Append("<strong>Product Qty</strong>");
                sb.Append("</div>");
                foreach(var item in model.ProductionUIs)
                {
                    sb.Append("<div class='col-4 border table-cell text-center'>");
                    sb.Append(item.name);
                    sb.Append("</div>");
                    sb.Append("<div class='col-4 border table-cell text-center'>");
                    sb.Append(item.state);
                    sb.Append("</div>");
                    sb.Append("<div class='col-4 border table-cell text-center'>");
                    sb.Append(item.product_uom_qty);
                    sb.Append("</div>");
                }
            }
                return sb.ToString();
        }

        private string BuildAchievementCard(QtyProdResultByOperViewModel model)
        {
            StringBuilder sb = new StringBuilder();
            foreach(var item in model.TargetViewModels)
            {
                string textColor = string.Empty;
                sb.Append("<div class='target-item bg-primary'>");
                string status = string.Empty;
                if (item.Item == "Defect")
                {
                    if (item.Percent > 100)
                    {
                        status = "bg-danger";
                    }
                    else if (item.Percent > 75 && item.Percent <= 100)
                    {
                        status = "bg-warning";
                    }
                    else
                    {
                        status = "bg-primary";
                    }
                }
                else
                {
                    if (item.Percent > 0 && item.Percent <= 75)
                    {
                        status = "bg-danger";
                    }
                    else if (item.Percent > 75 && item.Percent <= 92)
                    {
                        status = "bg-warning";
                    }
                    else
                    {
                        status = "bg-primary";
                    }
                }
                sb.Append("<div>");
                if(item.Item == "Daily Plan")
                sb.Append("</div>");
                sb.Append("</div>");
            }
            return sb.ToString();
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
