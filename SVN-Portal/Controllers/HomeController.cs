using ClosedXML.Excel;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.EMMA;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SVN_Portal.DAL.DataPortal;
using SVN_Portal.DAL.DTO;
using SVN_Portal.Models;
using SVN_Portal.Services.Configurations;
using SVN_Portal.Services.ObjectClasses;
using SVNShareLib;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO.Packaging;
using System.Linq;
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
        APIConfiguration aPIConfiguration;

        public HomeController(ILogger<HomeController> logger,
            AppConfig appConfig,
            DBConfiguration dBConfiguration,
            OperInfoConfig operInfoConfig,
            APIConfiguration aPIConfiguration,
            QCInfoConfig qCInfoConfig)
        {
            _logger = logger;
            this.appConfig = appConfig;
            this.dBConfiguration = dBConfiguration;
            connectionString = dBConfiguration.GetConnectionString();
            this.qCInfoConfig = qCInfoConfig;
            this.operInfoConfig = operInfoConfig;
            this.aPIConfiguration = aPIConfiguration;
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
                //List<string> opers = appConfig.OperList.Split(",").ToList();

                var appSettingDataPortal = new SVN_AppSettingDataPortal(connectionString);
                var operInfo1 = await appSettingDataPortal.GetOperInfoConfig();
                List<OperInfo> opers = operInfo1.OperInfo;

                //List<OperInfo> opers = operInfoConfig.OperInfo;
                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryData(strdate, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString, 3);
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

        /// <summary>
        /// Trang dashboard tổng hợp kết quả sản xuất theo từng ca, từng tổ
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
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
                //List<string> opers = appConfig.OperList.Split(",").ToList();

                var appSettingDataPortal = new SVN_AppSettingDataPortal(connectionString);
                var operInfo1 = await appSettingDataPortal.GetOperInfoConfig();
                List<OperInfo> opers = operInfo1.OperInfo;

                //List<OperInfo> opers = operInfoConfig.OperInfo;
                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryData(strdate, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString, 3);
                if (models != null && models.Count > 0)
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
                    models = models.Where(x => x.IsProduction).OrderByDescending(x => x.CanProductionByCheclist).OrderByDescending(x => x.IsProduction).ToList();

                    models = models
                    .OrderByDescending(x => x.ViewModels.Any(i => i.Target != 0))
                    .ThenBy(x => x.ViewModels.Sum(i => i.Line) == x.ViewModels.Sum(i => i.Target))
                    .ThenBy(x => x.ViewModels.Sum(i => i.Line) > x.ViewModels.Sum(i => i.Target))
                    .ThenBy(x => x.Line)
                    .ThenBy(x => x.ViewModels
                        .Where(i => i.Target != 0)
                        .Select(i => GetStartTime(i.Time))
                        .DefaultIfEmpty(TimeSpan.MaxValue)
                        .Min())
                    .ToList();
                }

                var compareDataPortal = new SVN_Compare_peopleDataPortal(connectionString);
                var compareUI = await compareDataPortal.ReadList(strdate);

                if (compareUI != null && compareUI.Count > 0)
                {
                    
                    int checkingQty = compareUI.Where(x => x.type_value == "Qty_check_in").Sum(x => x.Qty);
                    //int arrangeQty = compareUI.Where(x => x.type_value == "PD_arrange").Sum(x => x.Qty);
                    int arrangeQty = ArrangingNumber(models);

                    decimal rate = checkingQty != 0 ? arrangeQty * 100 / checkingQty : 0;

                    string comparePeople = "👷‍👷‍ Check-in: " + checkingQty + " /Arranging: " + arrangeQty + " /Rate: " + rate + "%";
                    ViewBag.ComparePeople = comparePeople;
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

        /// <summary>
        /// dashboard thêm phần chia biểu đồ ra các slide
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public async Task<IActionResult> ProductionResultV1(DateTime date)
        {
            List<QtyPDByOperVMPerSlide> qtyPDByOperVMPerSlides = new List<QtyPDByOperVMPerSlide>();
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
                //List<string> opers = appConfig.OperList.Split(",").ToList();

                var appSettingDataPortal = new SVN_AppSettingDataPortal(connectionString);
                var operInfo1 = await appSettingDataPortal.GetOperInfoConfig();
                List<OperInfo> opers = operInfo1.OperInfo;

                //List<OperInfo> opers = operInfoConfig.OperInfo;
                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryData(strdate, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString, 3);
                if (models != null && models.Count > 0)
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
                    models = models.Where(x => x.IsProduction).OrderByDescending(x => x.CanProductionByCheclist).OrderByDescending(x => x.IsProduction).ToList();

                    models = models
                    .OrderByDescending(x => x.ViewModels.Any(i => i.Target != 0))
                    .ThenBy(x => x.ViewModels.Sum(i => i.Line) == x.ViewModels.Sum(i => i.Target))
                    .ThenBy(x => x.ViewModels.Sum(i => i.Line) > x.ViewModels.Sum(i => i.Target))
                    .ThenBy(x => x.Line)
                    .ThenBy(x => x.ViewModels
                        .Where(i => i.Target != 0)
                        .Select(i => GetStartTime(i.Time))
                        .DefaultIfEmpty(TimeSpan.MaxValue)
                        .Min())
                    .ToList();

                    //chuẩn bị xong nguyên liệu, bây giờ thì cook :)))
                    int pageSize = 9;
                    for (int i = 0; i < models.Count; i += pageSize)
                    {
                        var slide = new QtyPDByOperVMPerSlide();

                        slide.OperViewModels = models
                            .Skip(i)
                            .Take(pageSize)
                            .ToList();

                        qtyPDByOperVMPerSlides.Add(slide);
                    }
                }

                var compareDataPortal = new SVN_Compare_peopleDataPortal(connectionString);
                var compareUI = await compareDataPortal.ReadList(strdate);

                if (compareUI != null && compareUI.Count > 0)
                {

                    int checkingQty = compareUI.Where(x => x.type_value == "Qty_check_in").Sum(x => x.Qty);
                    //int arrangeQty = compareUI.Where(x => x.type_value == "PD_arrange").Sum(x => x.Qty);
                    int arrangeQty = ArrangingNumber(models);

                    decimal rate = checkingQty != 0 ? arrangeQty * 100 / checkingQty : 0;

                    string comparePeople = "👷‍👷‍ Check-in: " + checkingQty + " /Arranging: " + arrangeQty + " /Rate: " + rate + "%";
                    ViewBag.ComparePeople = comparePeople;
                }

                return View(qtyPDByOperVMPerSlides);
            }
            catch (Exception ex)
            {
                List<QtyPDByOperVMPerSlide> vMPerSlides = new List<QtyPDByOperVMPerSlide>();
                return View(vMPerSlides);

            }
        }

        // Defect rate 20250103

        public async Task<IActionResult> Defect_Rate(DateTime date)
        {
            List<SVN_Defect_recordUI> models = new List<SVN_Defect_recordUI>();
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
                //List<string> opers = appConfig.OperList.Split(",").ToList();

                var appSettingDataPortal = new SVN_AppSettingDataPortal(connectionString);
                var operInfo1 = await appSettingDataPortal.GetOperInfoConfig();
                List<OperInfo> opers = operInfo1.OperInfo;

                //List<OperInfo> opers = operInfoConfig.OperInfo;
                opers = opers.Where(x => x.Operation == oper).ToList();
                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryData(strdate, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString, 3);
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
            List<PDResultDailyViewModel> pdResultviewModels = new List<PDResultDailyViewModel>();
            List<CostDailyViewModel> costDailyViewModels = new List<CostDailyViewModel>();

            var appSettingDataPortal = new SVN_AppSettingDataPortal(connectionString);
            var operInfo = await appSettingDataPortal.GetOperInfoConfig();
            var cost = await appSettingDataPortal.GetCostPerDay();
            var companyCode = await appSettingDataPortal.GetLocalCompanyCode();
            var localCurrency = await appSettingDataPortal.GetCurrencyInfoByCode(companyCode);
            var vndRate = await appSettingDataPortal.GetVNDRate();
            var finalCurrency = "USD";
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
                //List<string> opers = appConfig.OperList.Split(",").ToList();
                //List<OperInfo> opers = operInfoConfig.OperInfo;

                //Thay đổi đọc setting từ csdl
                List<OperInfo> opers = operInfo.OperInfo;
                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryDataV1(strdate, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString, 3);
                if (models != null && models.Count > 0)
                {
                    pdResultviewModels = GetPDResultDailyViewModel(models, localCurrency, vndRate, out finalCurrency);

                    //Tính tổng danh thu trên ngày của tất cả operation
                    var grandTotalTargetRevenue = Math.Round(pdResultviewModels.Sum(x => double.Parse(x.TargetRevenue)), appConfig.Rounding);
                    var grandTotalActualRevenue = Math.Round(pdResultviewModels.Sum(x => double.Parse(x.ActualRevenue)), appConfig.Rounding);
                    var grandTotalRevenueRate = Math.Round(grandTotalTargetRevenue == 0 ? 0 : (grandTotalActualRevenue / grandTotalTargetRevenue) * 100, appConfig.Rounding);

                    //var finalCostResult = await GetAmountByCurrency(cost, "USD", localCurrency);
                    //try
                    //{
                    //    cost = Math.Round((double)finalCostResult.Content, 0);
                    //}
                    //catch
                    //{

                    //}
                    cost = localCurrency == "VND" ? Math.Round(cost * vndRate, 0) : cost;

                    CostDailyViewModel costDailyViewModel1 = new CostDailyViewModel();
                    costDailyViewModel1.Currency = finalCurrency;
                    costDailyViewModel1.Cost = cost;
                    costDailyViewModel1.Date = date.ToString("dd/MM/yyyy");
                    costDailyViewModel1.TargetRevenue = grandTotalTargetRevenue;
                    costDailyViewModel1.ActualRevenue = grandTotalActualRevenue;
                    costDailyViewModel1.RevenueRate = grandTotalRevenueRate;
                    costDailyViewModels.Add(costDailyViewModel1);

                    var costAndRevenueInfo = "💸FN Cost: " + cost.ToString("N0") + " " + finalCurrency
                        + " | 💰PMC WO: " + grandTotalTargetRevenue.ToString("N0") + " " + finalCurrency
                        + " /💰PD Output: " + grandTotalActualRevenue.ToString("N0") + " " + finalCurrency
                        + " /💰Rate: " + grandTotalRevenueRate.ToString() + "%";
                    ViewBag.CostAndRevenueInfo = costAndRevenueInfo;
                    ViewBag.LocalCurrency = finalCurrency;
                }

                // 21/12/2025: tính tri phí doanh thu của ngày hôm trc
                var yesterday = date.AddDays(-1);
                string stryesterday = yesterday.ToString("yyyyMMdd");
                List<QtyProdResultByOperViewModel> previousmodels = new List<QtyProdResultByOperViewModel>();
                previousmodels = await dataPortal.SummaryDataV1(stryesterday, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString, 3);

                while(previousmodels == null || previousmodels.Count == 0)
                {
                    yesterday = yesterday.AddDays(-1);
                    stryesterday = yesterday.ToString("yyyyMMdd");
                    previousmodels = await dataPortal.SummaryDataV1(stryesterday, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString, 3);
                }
                if(previousmodels != null && previousmodels.Count > 0)
                {
                    var previouspdResultviewModels = GetPDResultDailyViewModel(previousmodels, localCurrency, vndRate, out finalCurrency);
                    //Tính tổng danh thu trên ngày của tất cả operation
                    var previousgrandTotalTargetRevenue = Math.Round(previouspdResultviewModels.Sum(x => double.Parse(x.TargetRevenue)), appConfig.Rounding);
                    var previousgrandTotalActualRevenue = Math.Round(previouspdResultviewModels.Sum(x => double.Parse(x.ActualRevenue)), appConfig.Rounding);
                    var previousgrandTotalRevenueRate = Math.Round(previousgrandTotalTargetRevenue == 0 ? 0 : (previousgrandTotalActualRevenue / previousgrandTotalTargetRevenue) * 100, appConfig.Rounding);

                    CostDailyViewModel costDailyViewModel2 = new CostDailyViewModel();
                    costDailyViewModel2.Currency = finalCurrency;
                    costDailyViewModel2.Cost = cost;
                    costDailyViewModel2.Date = yesterday.ToString("dd/MM/yyyy");
                    costDailyViewModel2.TargetRevenue = previousgrandTotalTargetRevenue;
                    costDailyViewModel2.ActualRevenue = previousgrandTotalActualRevenue;
                    costDailyViewModel2.RevenueRate = previousgrandTotalRevenueRate;
                    costDailyViewModels.Add(costDailyViewModel2);
                }

                costDailyViewModels = costDailyViewModels.OrderBy(x => x.Date).ToList();
                ViewBag.CostDaily = costDailyViewModels;

                //23/12/2025: Tính toán Cost và doanh thu theo năm
                //Lấy cost của 1 năm được nhập bởi PMC
                var yearCost = await appSettingDataPortal.GetCostInYear();
                var svnTargetDataPortal = new SVN_TargetDataPortal(connectionString);
                string companyStartDate = await appSettingDataPortal.GetStartDate();
                string currentDate = date.ToString("yyyyMMdd");

                //Lấy target/Actual Output từ ngày bắt đầu đến hiện tại
                var yearlyTarget = await svnTargetDataPortal.ReadListTargetByDate(companyStartDate, currentDate);
                if(yearlyTarget != null)
                {
                    var costYearlyResult = GetCostPerYear(yearlyTarget, operInfoConfig, localCurrency, vndRate);
                    if(costYearlyResult != null)
                    {
                        //var finalYearlyCostResult = await GetAmountByCurrency(yearCost, "USD", localCurrency);
                        //double yearlyCostValue = 0;
                        //try
                        //{
                        //    yearlyCostValue = (double)finalYearlyCostResult.Content;
                        //}
                        //catch
                        //{
                        //}
                        //var yearlyTargetRevenueResult = await GetAmountByCurrency(costYearlyResult.TargetRevenue, "USD", localCurrency);
                        //var yearlyActualRevenueResult = await GetAmountByCurrency(costYearlyResult.ActualRevenue, "USD", localCurrency);
                        //double yearlyTargetRevenue = 0;
                        //double yearlyActualRevenue = 0;
                        //try
                        //{
                        //    yearlyTargetRevenue = (double)yearlyTargetRevenueResult.Content;
                        //}
                        //catch
                        //{
                        //}
                        //try
                        //{
                        //    yearlyActualRevenue = (double)yearlyActualRevenueResult.Content;
                        //}
                        //catch
                        //{
                        //}
                        var yearlyCostValue = localCurrency == "VND" ? Math.Round(yearCost * vndRate, 0) : yearCost;
                        var yearlyTargetRevenue = localCurrency == "VND" ? Math.Round(costYearlyResult.TargetRevenue * vndRate, 0) : costYearlyResult.TargetRevenue;
                        var yearlyActualRevenue = localCurrency == "VND" ? Math.Round(costYearlyResult.ActualRevenue * vndRate, 0) : costYearlyResult.ActualRevenue;
                        var yearlyRevenueRate = yearlyTargetRevenue == 0 ? 0 : Math.Round((yearlyActualRevenue / yearlyTargetRevenue) * 100, appConfig.Rounding);
                        var costAndRevenueYearlyInfo = "💸FN Yearly Cost: " + yearlyCostValue.ToString("N0") + " " + finalCurrency
                            + " | 💰PMC Yearly WO: " + yearlyTargetRevenue.ToString("N0") + " " + finalCurrency
                            + " |💰PD Yearly Output: " + yearlyActualRevenue.ToString("N0") + " " + finalCurrency
                            + " |💰PD/PMC Rate: " + yearlyRevenueRate.ToString() + "%";
                        ViewBag.CostAndRevenueYearlyInfo = costAndRevenueYearlyInfo;
                    }
                }



                List<string> statusList = new List<string>();
                foreach(var item in pdResultviewModels)
                {
                    string warning = " ⚠️ ";
                    string error = " ❌ ";
                    string issue = "❗";
                    string status = item.OperationActive + ": ";

                    if (double.Parse(item.DailyPlanAchieve.Replace("%", "")) >= 0 && double.Parse(item.DailyPlanAchieve.Replace("%", "")) <= 75)
                    {
                        status = status + "Daily plan " + error + item.DailyPlanAchieve;
                    }
                    else if (double.Parse(item.DailyPlanAchieve.Replace("%", "")) > 100)
                    {
                        status = status + "Daily plan " + warning + item.DailyPlanAchieve;
                    }
                    else if (double.Parse(item.DailyPlanAchieve.Replace("%", "")) > 75 && double.Parse(item.DailyPlanAchieve.Replace("%", "")) <= 92)
                    {
                        status = status + "Daily plan " + warning + item.DailyPlanAchieve;
                    }
                    else
                    {

                    }

                    if (double.Parse(item.UPH.Replace("%", "")) >= 0 && double.Parse(item.UPH.Replace("%", "")) <= 75)
                    {
                        status = status + " UPH " + error + item.UPH;
                    }
                    else if (double.Parse(item.UPH.Replace("%", "")) > 100)
                    {
                        status = status + " UPH " + warning + item.UPH;
                    }
                    else if (double.Parse(item.UPH.Replace("%", "")) > 75 && double.Parse(item.UPH.Replace("%", "")) <= 92)
                    {
                        status = status + " UPH " + warning + item.UPH;
                    }
                    else
                    {

                    }

                    if (double.Parse(item.UPPH.Replace("%", "")) >= 0 && double.Parse(item.UPPH.Replace("%", "")) <= 75)
                    {
                        status = status + " UPPH " + error + item.UPPH;
                    }
                    else if (double.Parse(item.UPPH.Replace("%", "")) > 100)
                    {
                        status = status + " UPPH " + warning + item.UPPH;
                    }
                    else if (double.Parse(item.UPPH.Replace("%", "")) > 75 && double.Parse(item.UPPH.Replace("%", "")) <= 92)
                    {
                        status = status + " UPPH " + warning + item.UPPH;
                    }
                    else
                    {

                    }

                    if (double.Parse(item.DefectRate.Replace("%", "")) > 100)
                    {
                        status = status + " Defect " + error + item.DefectRate;
                    }
                    else if (double.Parse(item.DefectRate.Replace("%", "")) > 75 && double.Parse(item.DefectRate.Replace("%", "")) <= 100)
                    {
                        status = status + " Defect " + warning + item.DefectRate;
                    }
                    else
                    {
                        
                    }

                    statusList.Add(status);
                }

                ViewBag.StatusList = statusList;

                return View(pdResultviewModels);
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

                var appSettingDataPortal = new SVN_AppSettingDataPortal(connectionString);
                var operInfo1 = await appSettingDataPortal.GetOperInfoConfig();

                List<OperInfo> opers = new List<OperInfo>();
                //var singleOper = operInfoConfig.OperInfo.Where(x => x.Operation == oper).ToList();

                var singleOper = operInfo1.OperInfo.Where(x => x.Operation == oper).ToList();
                foreach (var item in singleOper)
                {
                    if (item.WC != null && item.WC.Count > 0)
                    {
                        foreach (var wc in item.WC)
                        {
                            opers.Add(new OperInfo { Operation = item.Operation, WCName = wc.WCName, Top_row = wc.Top_row, ColWidth = item.ColWidth, StoreID = item.StoreID });
                        }
                    }
                    else
                    {
                        opers.Add(new OperInfo { Operation = item.Operation, WCName = "", ColWidth = item.ColWidth, StoreID = item.StoreID });
                    }
                }

                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryData_Viindoo(strdate, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString);

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

        /// <summary>
        /// Trang hiển thị thông tin chi tiết theo từng oper, từng ca, từng tổ
        /// </summary>
        /// <param name="date"></param>
        /// <param name="oper"></param>
        /// <returns></returns>
        public async Task<IActionResult> ChartInfoPerOper(DateTime date, string operline)
        {
            var appSettingDataPortal = new SVN_AppSettingDataPortal(connectionString);
            var inputItemsDataPortal = new SVN_InputItemsStatusDataPortal(connectionString);

            List<QtyProdResultByOperViewModel> models = new List<QtyProdResultByOperViewModel>();
            List<SVN_InputItemsStatusUI> inputItemsStatusUIs = new List<SVN_InputItemsStatusUI>();
            if (date == DateTime.MinValue)
            {
                date = DateTime.Now;
            }
            ViewBag.date = date;
            ViewBag.oper = operline;
            //Check xem truyền có đang setup hay không
            var operSetupInProcess = appSettingDataPortal.GetOperationSetupInProcess().Result.Split(",").FirstOrDefault(x => x == operline);
            var setupNote = await appSettingDataPortal.GetOperationInSetupStatus();
            var inSetupFontSize = await appSettingDataPortal.GetInSetupStatusFontSize();
            if (!string.IsNullOrWhiteSpace(operSetupInProcess))
            {
                ViewBag.IsInSetup = true;
                ViewBag.SetupNote = setupNote;
                ViewBag.InSetupFontSize = inSetupFontSize;
                return View(models);
            }
            else
            {
                ViewBag.IsInSetup = false;
            }

            try
            {
                string storedProceduce = "SVN_Pro_CalTarget_Viindoo";
                string strdate = "20241220";
                string tableName = "SVN_Production_result_Viindoo";
                
                var operLineList = operline.Split("-").ToList();
                var oper = operLineList[0];
                var line = operLineList.Count > 1 ? operLineList[1] : "";
                
                strdate = date.ToString("yyyyMMdd");

                var operInfo1 = await appSettingDataPortal.GetOperInfoConfig();

                List<OperInfo> opers = new List<OperInfo>();
                //var singleOper = operInfoConfig.OperInfo.Where(x => x.MasterOperation == oper).ToList();

                var singleOper = operInfo1.OperInfo.Where(x => x.MasterOperation == oper).ToList();
                foreach (var item in singleOper)
                {
                    if (item.WC != null && item.WC.Count > 0)
                    {
                        foreach (var wc in item.WC)
                        {
                            opers.Add(new OperInfo { Operation = item.Operation, MasterOperation = item.MasterOperation, WCName = wc.WCName, Top_row = wc.Top_row, ColWidth = item.ColWidth, Name = item.Name, StoreID = item.StoreID });
                        }
                    }
                    else
                    {
                        opers.Add(new OperInfo { Operation = item.Operation, MasterOperation = item.MasterOperation, WCName = "", ColWidth = item.ColWidth, Name = item.Name, StoreID = item.StoreID });
                    }
                }

                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryData_Viindoo(strdate, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString);
                inputItemsStatusUIs = await inputItemsDataPortal.ReadList(strdate, operline);
                ViewBag.InputItemsStatus = inputItemsStatusUIs;

                //Lọc dữ liệu lấy dc theo line
                if (!string.IsNullOrWhiteSpace(line))
                {
                    models = models.Where(x => x.Line == line).ToList();
                }

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

                    models = models.Where(x => x.IsProduction).OrderByDescending(x => x.CanProductionByCheclist).OrderByDescending(x => x.IsProduction).ToList();

                    models = models
                    .OrderByDescending(x => x.ViewModels.Any(i => i.Target != 0))
                    .ThenBy(x => x.Line)
                    .ThenBy(x => x.ViewModels
                        .Where(i => i.Target != 0)
                        .Select(i => GetStartTime(i.Time))
                        .DefaultIfEmpty(TimeSpan.MaxValue)
                        .Min())
                    .ToList();
                }
                return View(models);
            }
            catch (Exception ex)
            {
                QtyProdResultByOperViewModel model = new QtyProdResultByOperViewModel();
                var data = model.GetData(operline);
                models = new List<QtyProdResultByOperViewModel> { data };
                return View(models);

            }
        }

        public async Task<IActionResult> AutomationTrackingPerOper(DateTime date, string oper = "Injection")
        {
            List<QtyProdResultByOperViewModel> models = new List<QtyProdResultByOperViewModel>();
            QtyProdResultByOperViewModel model = new QtyProdResultByOperViewModel();
            try
            {

                string storedProceduce = "SVN_Pro_CalTarget_Viindoo";
                string strdate = "20241220";
                string tableName = "SVN_Production_result_Viindoo";
                if (date == DateTime.MinValue)
                {
                    date = DateTime.Now;
                }
                strdate = date.ToString("yyyyMMdd");
                ViewBag.date = date;
                ViewBag.oper = oper;

                var appSettingDataPortal = new SVN_AppSettingDataPortal(connectionString);
                var operInfo1 = await appSettingDataPortal.GetOperInfoConfig();
                List<OperInfo> opers = operInfo1.OperInfo;

                //List<OperInfo> opers = new List<OperInfo>();
                var singleOper = operInfoConfig.OperInfo.Where(x => x.Operation == oper).ToList();
                foreach (var item in singleOper)
                {
                    if (item.WC != null && item.WC.Count > 0)
                    {
                        foreach (var wc in item.WC)
                        {
                            opers.Add(new OperInfo { Operation = item.Operation, WCName = wc.WCName, Top_row = wc.Top_row, ColWidth = item.ColWidth, StoreID = item.StoreID });
                        }
                    }
                    else
                    {
                        opers.Add(new OperInfo { Operation = item.Operation, WCName = "", ColWidth = item.ColWidth, StoreID = item.StoreID });
                    }
                }

                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryData_Viindoo(strdate, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString);

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
                    foreach (var item in models)
                    {
                        var userInfo = qCInfoConfig.UserInfo.FirstOrDefault(x => x.Operation == model.Operation);
                        var operInfo = operInfoConfig.OperInfo.FirstOrDefault(x => x.Operation == model.Operation);
                        if (userInfo != null)
                        {
                            item.PDName = userInfo.PDName;
                            item.QCName = userInfo.QCName;
                            item.PDURL = userInfo.PDURL;
                            item.QCURL = userInfo.QCURL;
                        }
                        if (operInfo != null)
                        {
                            item.ColWidth = operInfo.ColWidth;
                        }
                    }
                    model = models.FirstOrDefault(x => x.Operation == oper);
                }


                return View(model);
            }
            catch (Exception ex)
            {
                return View(model);
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
                if (operInfo != null)
                {
                    if (!string.IsNullOrWhiteSpace(wc))
                    {
                        operInfo.WCName = wc;
                        var wcInfo = operInfo.WC.FirstOrDefault(x => x.WCName == wc);
                        if (wcInfo != null)
                        {
                            operInfo.Top_row = wcInfo.Top_row;
                        }
                    }
                }
                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                model = await dataPortal.GetDataByOperAndWC(date, operInfo, storedProceduce, tableName, dBConfiguration.CheckListConnectionString);
                //sử dụng stringBuilder để build lại 2 table
                if (model != null)
                {
                    string pdChecked = "🔴";
                    string mtChecked = "🔴";
                    string qcChecked = "🔴";
                    string pdConfirmed = "🔴";
                    string qcConfirmed = "🔴";

                    if (model.IsPDChecked)
                    {
                        pdChecked = "🟢";
                    }
                    if (model.IsMTChecked)
                    {
                        mtChecked = "🟢";
                    }
                    if (model.IsQCChecked)
                    {
                        qcChecked = "🟢";
                    }
                    if (model.IsPDConfirmed)
                    {
                        pdConfirmed = "🟢";
                    }
                    if (model.IsQCConfirmed)
                    {
                        qcConfirmed = "🟢";
                    }

                    StringBuilder sb = new StringBuilder();
                    

                    strProductionResultTable = BuildProductionResultTable(model);
                    strTargetTable = BuildAchievementCard(model, date);

                    //if (!oper.Contains("Walter"))
                    //{
                    //    model.CanProductionByCheclist = true;
                    //}
                    sb.Append("<p style='font-size:20px' class=' text-light'>");
                    sb.Append("<strong>Checklist status</strong>: ");
                    sb.Append(pdChecked + " PD - " + mtChecked + " MT - " + qcChecked + " QC Checked | " + pdConfirmed + " PD - " + qcConfirmed + " QC Confirmed");
                    sb.Append(" | <strong>Actual WorkingTime</strong>: ");
                    sb.Append(Math.Round(model.CurWorkingTime, appConfig.Rounding) + " h");
                    sb.Append(" | <strong>Downtime</strong>: ");
                    sb.Append(Math.Round(model.TotalDuration, appConfig.Rounding) + " h");
                    sb.Append("</p>");
                    string downTimeStatus = string.Empty;
                    if (!model.CanProductionByDowntime)
                    {
                        downTimeStatus = "Máy đang bảo trì, dự kiến kết thúc: " + model.EndDownTime.ToString("dd/MM/yyyy HH:mm");
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
                        customer = model.Customer,
                        checklistStatus = sb.ToString(),
                        isProduction = model.IsProduction,
                        canProductionByDowntime = model.CanProductionByDowntime,
                        downTimeStatus = downTimeStatus,
                        canProduction = model.CanProductionByCheclist
                    });
                }
                else
                {
                    return new JsonResult(new { result = false, message = "No data" });
                }

            }
            catch (Exception ex)
            {
                return new JsonResult(new { result = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Hàm build lại dữ liệu cho dashboard bằng cách lấy dữ liệu từ oper và wc bằng Ajax
        /// </summary>
        /// <param name="date"></param>
        /// <param name="oper"></param>
        /// <param name="wc"></param>
        /// <returns></returns>
        [HttpPost]
        public async Task<IActionResult> GetDataByOperAndWCMainDashBoard(string date, string oper, string wc)
        {
            QtyProdResultByOperViewModel model = new QtyProdResultByOperViewModel();
            string strForecase = string.Empty;
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
                model = await dataPortal.GetDataByOperAndWC(date, operInfo, storedProceduce, tableName, dBConfiguration.CheckListConnectionString);

                //sử dụng stringBuilder để build lại 2 table
                if (model != null)
                {
                    string pdChecked = "🔴";
                    string mtChecked = "🔴";
                    string qcChecked = "🔴";
                    string pdConfirmed = "🔴";
                    string qcConfirmed = "🔴";

                    if (model.IsPDChecked)
                    {
                        pdChecked = "🟢";
                    }
                    if (model.IsMTChecked)
                    {
                        mtChecked = "🟢";
                    }
                    if (model.IsQCChecked)
                    {
                        qcChecked = "🟢";
                    }
                    if (model.IsPDConfirmed)
                    {
                        pdConfirmed = "🟢";
                    }
                    if (model.IsQCConfirmed)
                    {
                        qcConfirmed = "🟢";
                    }

                    StringBuilder sb = new StringBuilder();
                    

                    //if (!oper.Contains("Walter"))
                    //{
                    //    model.CanProductionByCheclist = true;
                    //}
                    sb.Append("<p style='font-size:20px' class=' text-light'>");
                    sb.Append("<strong>Checklist status</strong>: ");
                    sb.Append(pdChecked + " PD - " + mtChecked + " MT - " + qcChecked + " QC Checked | " + pdConfirmed + " PD - " + qcConfirmed + " QC Confirmed");
                    sb.Append(" | <strong>Actual WorkingTime</strong>: ");
                    sb.Append(Math.Round(model.CurWorkingTime, appConfig.Rounding) + " h");
                    sb.Append(" | <strong>Downtime</strong>: ");
                    sb.Append(Math.Round(model.TotalDuration, appConfig.Rounding) + " h");
                    sb.Append("</p>");
                    //sb.Append("<p style='font-size:20px' class=' text-light'>");
                    //sb.Append("<strong>Current WorkingTime</strong>: ");
                    //sb.Append(Math.Round(model.CurWorkingTime, appConfig.Rounding) + " h");
                    //sb.Append(" |  <strong>Current Duration</strong>: ");
                    //sb.Append(Math.Round(model.CurDuration, appConfig.Rounding) + " h");
                    //sb.Append("</p>");
                    string downTimeStatus = string.Empty;
                    if(!model.CanProductionByDowntime)
                    {
                        if(model.EndDownTime == DateTime.MinValue)
                        {
                            downTimeStatus = "Máy đang bảo trì";
                        }
                        else
                        {
                            downTimeStatus = "Máy đang bảo trì, dự kiến kết thúc: " + model.EndDownTime.ToString("dd/MM/yyyy HH:mm");
                        }
                    }

                    strForecase = BuildForecastInfo(model.Forecast);
                    strTargetTable = BuildAchievementCard(model, date);

                    return new JsonResult(new
                    {
                        result = true,
                        forecase = strForecase,
                        targetTable = strTargetTable,
                        isProduction = model.IsProduction,
                        canProduction = model.CanProductionByCheclist,
                        canProductionByDowntime = model.CanProductionByDowntime,
                        downTimeStatus = downTimeStatus,
                        checklistStatus = sb.ToString(),
                        pdmodel = JsonConvert.SerializeObject(model.ViewModels),
                        defectcalmodel = JsonConvert.SerializeObject(model.DefectByCategoryViewModels)
                    });
                }
                else
                {
                    return new JsonResult(new { result = false, message = "No data" });
                }

            }
            catch (Exception ex)
            {
                return new JsonResult(new { result = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Hàm xây dựng bảng Production Result cho từng oper
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
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
            foreach (var item in model.ViewModels)
            {
                sb.Append("<div class='col-2'>");
                sb.Append("<div class='row'>");
                sb.Append("<div class='col-12 border table-cell text-center'>");
                sb.Append(item.Time);
                sb.Append("</div>");
                sb.Append("<div class='col-12 border table-cell text-center'>");
                sb.Append(Math.Round(item.Target, appConfig.Rounding));
                sb.Append("</div>");
                sb.Append("<div class='col-12 border table-cell text-center'>");
                sb.Append(Math.Round(item.Line, appConfig.Rounding));
                sb.Append("</div>");
                sb.Append("<div class='col-12 border table-cell text-center'>");
                sb.Append(Math.Round(item.ManQuantity, appConfig.Rounding));
                sb.Append("</div>");
                sb.Append("<div class='col-12 border table-cell text-center'>");
                sb.Append(Math.Round(item.NG, appConfig.Rounding));
                sb.Append("</div>");
                sb.Append("</div>");
                sb.Append("</div>");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Hàm xây dựng bảng Target Table cho từng oper
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        private string BuildTargetTable(QtyProdResultByOperViewModel model)
        {
            StringBuilder sb = new StringBuilder();
            if ((!string.IsNullOrWhiteSpace(model.WC) && model.WC.Contains("FG")) || appConfig.ShowSingleChart.Contains(model.Operation))
            {
                sb.Append("<div class='col-3 border table-cell text-center'><strong>Item</strong></div>");
                sb.Append("<div class='col-2 border table-cell text-center'><strong>Target</strong></div>");
                sb.Append("<div class='col-2 border table-cell text-center'><strong>Current</strong></div>");
                sb.Append("<div class='col-3 border table-cell text-center'><strong>Rate</strong></div>");
                sb.Append("<div class='col-2 border table-cell text-center'><strong>Status</strong></div>");
                foreach (var item in model.TargetViewModels)
                {
                    string status = string.Empty;
                    sb.Append("<div class='col-3 border table-cell text-center'><strong>" + item.Item + "</strong></div>");
                    if (item.Item == "Defect")
                    {
                        sb.Append("<div class='col-2 border table-cell text-center'>" + Math.Round(item.Target, appConfig.Rounding) + " %</div>");
                        sb.Append("<div class='col-2 border table-cell text-center'>" + Math.Round(item.Current, appConfig.Rounding) + " %</div>");
                        sb.Append("<div class='col-3 border table-cell text-center'>" + Math.Round(item.Percent, appConfig.Rounding) + " %</div>");
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
                        sb.Append("<div class='col-2 border table-cell text-center'>" + Math.Round(item.Target, appConfig.Rounding) + "</div>");
                        sb.Append("<div class='col-2 border table-cell text-center'>" + Math.Round(item.Current, appConfig.Rounding) + "</div>");
                        sb.Append("<div class='col-3 border table-cell text-center'>" + Math.Round(item.Percent, appConfig.Rounding) + " %</div>");
                        if (item.Percent >= 0 && item.Percent <= 75)
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
                sb.Append("<div class='row'>");
                sb.Append("<div class='col-4 border table-cell text-center'>");
                sb.Append("<strong>WO Name</strong>");
                sb.Append("</div>");
                sb.Append("<div class='col-4 border table-cell text-center'>");
                sb.Append("<strong>State</strong>");
                sb.Append("</div>");
                sb.Append("<div class='col-4 border table-cell text-center'>");
                sb.Append("<strong>Product Qty</strong>");
                sb.Append("</div>");
                foreach (var item in model.ProductionUIs)
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
                sb.Append("</div>");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Hàm xây dựng bảng Achievement Card cho từng oper
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        private string BuildAchievementCard(QtyProdResultByOperViewModel model, string date)
        {
            DateTime currentDate = DateTime.Now;
            try
            {
                currentDate = DateTime.ParseExact(date, "yyyyMMdd", null);
            }
            catch
            {
                currentDate = DateTime.Now;
            }
            string currentTime = string.Empty;
            List<SectionTime> sectionTimes = new List<SectionTime>();
            var listSection = model.ViewModels.Where(x => x.Target != 0).ToList();
            foreach (var subitem in listSection)
            {
                if (!string.IsNullOrWhiteSpace(subitem.Time))
                {
                    var times = subitem.Time.Split('-');
                    DateTime today = currentDate;

                    // Chuyển đổi thành định dạng HH:mm
                    string startTime = times[0].Replace("h", ":");
                    if (startTime.Last() == ':')
                    {
                        startTime = startTime + "00";
                    }
                    if (startTime.Length == 4)
                    {
                        startTime = "0" + startTime;
                    }

                    string endTime = times[1].Replace("h", ":");
                    if (endTime.Last() == ':')
                    {
                        endTime = endTime + "00";
                    }
                    if (endTime.Length == 4)
                    {
                        endTime = "0" + endTime;
                    }

                    // Tạo đối tượng DateTime với ngày hôm nay và giờ từ chuỗi
                    DateTime startDatetime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + startTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                    DateTime endDatetime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + endTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                    DateTime startRelaxTime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + "11:30", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                    DateTime endRelaxTime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + "12:30", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

                    DateTime startRelaxNoonTime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + "17:30", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                    DateTime endRelaxNoonTime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + "18:00", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                    if ((startDatetime <= DateTime.Now && endDatetime >= DateTime.Now) || 
                        (startRelaxTime <= DateTime.Now && endRelaxTime >= DateTime.Now) || 
                        (startRelaxNoonTime <= DateTime.Now && endRelaxNoonTime >= DateTime.Now))
                    {
                        currentTime = subitem.Time;
                    }

                    SectionTime sectionTime = new SectionTime();
                    sectionTime.StartTime = startDatetime;
                    sectionTime.EndTime = endDatetime;
                    sectionTimes.Add(sectionTime);
                }
            }

            sectionTimes = sectionTimes.OrderBy(x => x.StartTime).ToList();

            //gapTime = Math.Round(GetTotalGapv1(sectionTimes, curDateTime).TotalMinutes / 60.0, 2); // Tính khoảng thời gian trống giữa các ca

            var minStartSection = sectionTimes.FirstOrDefault() != null ? sectionTimes.FirstOrDefault().StartTime : DateTime.MinValue;
            var maxEndSection = sectionTimes.LastOrDefault() != null ? sectionTimes.LastOrDefault().EndTime : DateTime.MinValue;

            StringBuilder sb = new StringBuilder();
            if ((!string.IsNullOrWhiteSpace(model.WC) && model.WC.Contains("FG")) || appConfig.ShowSingleChart.Contains(model.Operation))
            {
                foreach (var item in model.TargetViewModels)
                {
                    string textColor = string.Empty;
                    string status = string.Empty;
                    string alert = string.Empty;
                    if (item.Item == "Defect")
                    {
                        if (item.Percent > 100)
                        {
                            status = "bg-danger";
                            if (model.IsProduction)
                            {
                                alert = "blinking";
                            }
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
                    else if (item.Item == "H.Plan" || item.Item == "UPH" || item.Item == "UPPH" || item.Item == "OEE")
                    {
                        if (!string.IsNullOrWhiteSpace(currentTime))
                        {
                            var times = currentTime.Split('-');
                            DateTime today = currentDate;

                            // Chuyển đổi thành định dạng HH:mm
                            string startTime = times[0].Replace("h", ":");
                            if (startTime.Last() == ':')
                            {
                                startTime = startTime + "00";
                            }
                            if (startTime.Length == 4)
                            {
                                startTime = "0" + startTime;
                            }

                            string endTime = times[1].Replace("h", ":");
                            if (endTime.Last() == ':')
                            {
                                endTime = endTime + "00";
                            }
                            if (endTime.Length == 4)
                            {
                                endTime = "0" + endTime;
                            }

                            // Tạo đối tượng DateTime với ngày hôm nay và giờ từ chuỗi
                            DateTime startDatetime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + startTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                            DateTime endDatetime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + endTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

                            DateTime startRelaxTime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + "11:30", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                            DateTime endRelaxTime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + "12:30", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

                            DateTime startRelaxNoonTime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + "17:30", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                            DateTime endRelaxNoonTime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + "18:00", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

                            if ((startDatetime <= DateTime.Now && endDatetime >= DateTime.Now) ||
                                (startRelaxTime <= DateTime.Now && endRelaxTime >= DateTime.Now) ||
                                (startRelaxNoonTime <= DateTime.Now && endRelaxNoonTime >= DateTime.Now))
                            {
                                status = "bg-primary";
                                if (item.Percent > 100)
                                {
                                    if (item.Item == "H.Plan")
                                    {
                                        if (model.IsProduction)
                                        {
                                            alert = "blinking-warning";
                                        }
                                    }
                                }
                            }
                            else
                            {
                                if (item.Percent > 0 && item.Percent <= 75)
                                {
                                    status = "bg-danger";
                                    if (model.IsProduction)
                                    {
                                        alert = "blinking";
                                    }
                                }
                                else if (item.Percent > 100)
                                {
                                    status = "bg-primary";
                                    if (item.Item == "H.Plan")
                                    {
                                        if (model.IsProduction)
                                        {
                                            alert = "blinking-warning";
                                        }
                                    }
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
                        }
                        else
                        {
                            if (item.Percent > 0 && item.Percent <= 75)
                            {
                                status = "bg-danger";
                                if (model.IsProduction)
                                {
                                    alert = "blinking";
                                }
                            }
                            else if (item.Percent > 100)
                            {
                                status = "bg-primary";
                                if (item.Item == "H.Plan")
                                {
                                    if (model.IsProduction)
                                    {
                                        alert = "blinking-warning";
                                    }
                                }
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
                    }
                    else
                    {
                        if (item.Percent >= 0 && item.Percent <= 75)
                        {
                            status = "bg-danger";
                            if (model.IsProduction)
                            {
                                alert = "blinking";
                            }
                        }
                        else if (item.Percent > 100)
                        {
                            status = "bg-primary";
                            if (item.Item == "Labor")
                            {
                                if (model.IsProduction)
                                {
                                    alert = "blinking-warning";
                                }
                            }
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

                    if(minStartSection > DateTime.Now)
                    {
                        status = status = "bg-primary";
                        alert = string.Empty;
                    }

                    sb.Append("<div class='target-item bg-primary " + alert + "'>");
                    sb.Append("<div>");
                    if (item.Item == "H.Plan")
                    {
                        sb.Append("<strong class='f-s-26'>📅 <span class='" + textColor + "'>" + item.Item + "</span></strong>");
                        sb.Append("<br>");
                    }
                    if (item.Item == "UPH")
                    {
                        sb.Append("<strong class='f-s-26'>⚙️ <span class='" + textColor + "'>" + item.Item + "</span></strong>");
                        sb.Append("<br>");
                    }
                    if (item.Item == "UPPH")
                    {
                        sb.Append("<strong class='f-s-26'>📈 <span class='" + textColor + "'>" + item.Item + "</span></strong>");
                        sb.Append("<br>");
                    }
                    if (item.Item == "Labor")
                    {
                        sb.Append("<strong class='f-s-26'>👷 <span class='" + textColor + "'>" + item.Item + "</span></strong>");
                        sb.Append("<br>");
                    }
                    if (item.Item == "OEE")
                    {
                        sb.Append("<strong class='f-s-26'>🎯 <span class='" + textColor + "'>" + item.Item + "</span></strong>");
                        sb.Append("<br>");
                    }
                    if (item.Item == "Defect")
                    {
                        sb.Append("<strong class='f-s-26'>❌ <span class='" + textColor + "'>" + item.Item + "</span></strong>");
                        sb.Append("<br>");
                    }

                    //if (item.Item == "Defect")
                    //{
                    //    sb.Append("<span>Tar: " + Math.Round(item.Target, appConfig.Rounding) + " %</span>");
                    //    sb.Append("<span> | Cur: " + Math.Round(item.Current, appConfig.Rounding) + " %</span> <br />");
                    //    sb.Append("<span>");
                    //    sb.Append("<strong class='f-s-23'>Rate:</strong> <strong class='rate-box " + status + " f-s-23'>" + Math.Round(item.Percent, appConfig.Rounding) + " %</strong>");
                    //    sb.Append("</span>");
                    //}
                    //else
                    //{
                    //    sb.Append("<span>Tar " + Math.Round(item.Target, appConfig.Rounding) + "</span>");
                    //    sb.Append("<span> | Cur: " + Math.Round(item.Current, appConfig.Rounding) + "</span> <br />");
                    //    sb.Append("<span>");
                    //    sb.Append("<strong class='f-s-23'>Rate:</strong> <strong class='rate-box " + status + " f-s-23'>" + Math.Round(item.Percent, appConfig.Rounding) + " %</strong>");
                    //    sb.Append("</span>");
                    //}

                    if (item.Item == "Defect")
                    {
                        sb.Append("<span>Tar: " + Math.Round(item.Target, appConfig.Rounding) + " %</span>");
                        sb.Append("<span> | Rate: " );
                        sb.Append("<strong class='rate-box " + status + "'>" + Math.Round(item.Percent, appConfig.Rounding) + " %</strong>");
                        sb.Append("</span> <br />");
                        sb.Append("<span>");
                        sb.Append("<strong class='f-s-23'>Cur: " + Math.Round(item.Current, appConfig.Rounding) + " %</strong>");
                        sb.Append("</span>");
                    }
                    else if(item.Item == "OEE")
                    {
                        sb.Append("<span>A: " + Math.Round(item.Availability, appConfig.Rounding) + " %</span>");
                        sb.Append("<span> | P: " + Math.Round(item.Performance, appConfig.Rounding) + " %</span>");
                        sb.Append("<span> | Q: " + Math.Round(item.Quality, appConfig.Rounding) + " %</span> <br />");
                        sb.Append("<span>");
                        sb.Append("<strong class='f-s-23'>Rate:</strong> <strong class='rate-box " + status + " f-s-23'>" + Math.Round(item.OEE, appConfig.Rounding) + " %</strong>");
                        sb.Append("</span>");
                    }
                    else
                    {
                        sb.Append("<span>Tar " + Math.Round(item.Target, appConfig.Rounding) + "</span>");
                        sb.Append("<span> | Cur: " + Math.Round(item.Current, appConfig.Rounding) + "</span> <br />");
                        sb.Append("<span>");
                        sb.Append("<strong class='f-s-23'>Rate:</strong> <strong class='rate-box " + status + " f-s-23'>" + Math.Round(item.Percent, appConfig.Rounding) + " %</strong>");
                        sb.Append("</span>");
                    }
                    //else
                    //{
                    //    sb.Append("<span>Tar " + Math.Round(item.Target, appConfig.Rounding) + "</span>");
                    //    sb.Append("<span> | Rate: ");
                    //    sb.Append("<strong class='rate-box " + status + "'>" + Math.Round(item.Percent, appConfig.Rounding) + " %</strong>");
                    //    sb.Append("</span> <br />");
                    //    sb.Append("<span>");
                    //    sb.Append("<strong class='f-s-23'>Cur: " + Math.Round(item.Current, appConfig.Rounding) + " </strong>");
                    //    sb.Append("</span>");
                    //}

                    sb.Append("</div>");
                    sb.Append("</div>");
                }
            }
            else
            {
                sb.Append("<div class='row'>");
                sb.Append("<div class='col-4 border table-cell text-center'>");
                sb.Append("<strong>WO Name</strong>");
                sb.Append("</div>");
                sb.Append("<div class='col-4 border table-cell text-center'>");
                sb.Append("<strong>State</strong>");
                sb.Append("</div>");
                sb.Append("<div class='col-4 border table-cell text-center'>");
                sb.Append("<strong>Product Qty</strong>");
                sb.Append("</div>");
                foreach (var item in model.ProductionUIs)
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
                sb.Append("</div>");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Hàm xây dựng thông tin dự báo (Forecast) cho dashboard
        /// </summary>
        /// <param name="Forecast"></param>
        /// <returns></returns>
        private string BuildForecastInfo(double Forecast)
        {
            StringBuilder sb = new StringBuilder();
            if (Forecast > 100)
            {
                sb.Append("<span style='font-size:60px;'>☀️</span>");
            }
            else if (Forecast > 75 && Forecast <= 100)
            {
                sb.Append("<span style='font-size:60px;'>🌥️</span>");
            }
            else if (Forecast > 50 && Forecast <= 75)
            {
                sb.Append("<span style='font-size:60px;'>☁️</span>");
            }
            else if (Forecast > 30 && Forecast <= 50)
            {
                sb.Append("<span style='font-size:60px;'>🌦️</span>");
            }
            else if (Forecast > 10 && Forecast <= 30)
            {
                sb.Append("<span style='font-size:60px;'>🌧️</span>");
            }
            else
            {
                sb.Append("<span style='font-size:60px;'>⚡</span>");
            }
            sb.Append("<h4 class='mt-3 text-center'>" + Forecast + " %</h4>");
            sb.Append("<h3 class='mt-3 text-center'>Forecast Achievement</h3>");
            return sb.ToString();
        }

        private int ArrangingNumber(List<QtyProdResultByOperViewModel> viewModels)
        {
            int number = 0;
            DateTime curDatetine = DateTime.Now;
            List<string> sessionTimes = new List<string>()
            {
                "8h-10h",
                "10h10-11h30",
                "12h30-15h",
                "15h10-17h30",
                "18h-20h"
            };
            foreach (var item in sessionTimes) 
            {
                var times = item.Split('-');
                DateTime today = curDatetine;
                // Chuyển đổi thành định dạng HH:mm
                string startTime = times[0].Replace("h", ":");
                if (startTime.Last() == ':')
                {
                    startTime = startTime + "00";
                }
                if (startTime.Length == 4)
                {
                    startTime = "0" + startTime;
                }
                string endTime = times[1].Replace("h", ":");
                if (endTime.Last() == ':')
                {
                    endTime = endTime + "00";
                }
                if (endTime.Length == 4)
                {
                    endTime = "0" + endTime;
                }
                // Tạo đối tượng DateTime với ngày hôm nay và giờ từ chuỗi
                DateTime startDatetime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + startTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                DateTime endDatetime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + endTime, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                DateTime startRelaxTime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + "11:30", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                DateTime endRelaxTime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + "12:30", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                DateTime startRelaxNoonTime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + "17:30", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                DateTime endRelaxNoonTime = DateTime.ParseExact(today.ToString("yyyy-MM-dd") + " " + "18:00", "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                if (startDatetime <= DateTime.Now && endDatetime >= DateTime.Now)
                {
                    var viewModel = viewModels.Select(x =>
                    {
                        var model = x.ViewModels.FirstOrDefault(y => y.Time == item && y.Line != 0);
                        if (model != null)
                            number = number + int.Parse(model.ManQuantity.ToString());
                        return x;
                    }).ToList();
                    //foreach (var x in viewModels)
                    //{
                    //    var model = x.ViewModels.FirstOrDefault(y => y.Time == item && y.Line != 0);
                    //    if (model != null)
                    //        number = number + int.Parse(model.ManQuantity.ToString());
                    //}
                }
            }
            return number;
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

        #region report
        public IActionResult PDResultByMonthReport(DateTime date)
        {
            List<SVN_production_summaryUI> models = new List<SVN_production_summaryUI>();
            SVN_production_summaryDataPortal dataPortal = new SVN_production_summaryDataPortal(connectionString);
            try
            {
                if (date == DateTime.MinValue)
                {
                    date = DateTime.Now;
                }
                int year = date.Year;
                int month = date.Month;
                models = dataPortal.ReadListByYearMonth(year, month);
                if (models.Count > 0)
                {
                    models = models.Where(x => x.Target != 0).ToList();
                }
            }
            catch (Exception ex)
            {


            }

            ViewBag.date = date;

            return View(models);
        }

        public async Task<IActionResult> PDResultDailyReport(DateTime fromdate, DateTime todate)
        {
            List<PDResultDailyViewModel> viewModels = new List<PDResultDailyViewModel>();
            try
            {
                viewModels = await GetPDResultDailyData(fromdate, todate);
                return View(viewModels);
            }
            catch (Exception ex)
            {
                return View(viewModels);
            }
        }

        public async Task<IActionResult> ExportPDResultDaily(DateTime fromdate, DateTime todate)
        {
            try
            {
                var viewModels = await GetPDResultDailyData(fromdate, todate);
                if (viewModels == null || viewModels.Count == 0)
                {
                    return RedirectToAction("PDResultDailyReport");
                }
                using(var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("PD Result Daily Report");
                    worksheet.Cell(1, 1).Value = "Production Result Daily";
                    worksheet.Cell(2, 1).Value = "Operation active";
                    worksheet.Cell(2, 2).Value = "Monthly Plan Achieve";
                    worksheet.Cell(2, 6).Value = "Daily Plan Achieve";
                    worksheet.Cell(2, 9).Value = "UPH";
                    worksheet.Cell(2, 10).Value = "UPPH";
                    worksheet.Cell(2, 11).Value = "Labor";
                    worksheet.Cell(2, 12).Value = "Defect Target Rate";
                    worksheet.Cell(2, 15).Value = "Check list on system";
                    worksheet.Cell(2, 16).Value = "Remark";
                    worksheet.Cell(3, 2).Value = "ERP WO #";
                    worksheet.Cell(3, 3).Value = "Plan";
                    worksheet.Cell(3, 4).Value = "Done";
                    worksheet.Cell(3, 5).Value = "%";
                    worksheet.Cell(3, 6).Value = "Target";
                    worksheet.Cell(3, 7).Value = "Current";
                    worksheet.Cell(3, 8).Value = "%";
                    worksheet.Cell(3, 12).Value = "Target";
                    worksheet.Cell(3, 13).Value = "Current";
                    worksheet.Cell(3, 14).Value = "%";

                    worksheet.Range(1, 1, 1, 16).Merge();
                    worksheet.Range(2, 1, 3, 1).Merge();
                    worksheet.Range(2, 2, 2, 5).Merge();
                    worksheet.Range(2, 6, 2, 8).Merge();
                    worksheet.Range(2, 9, 3, 9).Merge();
                    worksheet.Range(2, 10, 3, 10).Merge();
                    worksheet.Range(2, 11, 3, 11).Merge();
                    worksheet.Range(2, 12, 2, 14).Merge();
                    worksheet.Range(2, 15, 3, 15).Merge();
                    worksheet.Range(2, 16, 3, 16).Merge();

                    int row = 4;
                    foreach (var item in viewModels)
                    {
                        worksheet.Cell(row, 1).Value = item.OperationActive;
                        worksheet.Cell(row, 6).Value = item.DailyPlanTarget;
                        worksheet.Cell(row, 7).Value = item.DailyPlanCurrent;
                        worksheet.Cell(row, 8).Value = item.DailyPlanAchieve;
                        worksheet.Cell(row, 9).Value = item.UPH;
                        worksheet.Cell(row, 10).Value = item.UPPH;
                        worksheet.Cell(row, 11).Value = item.Labor;
                        worksheet.Cell(row, 12).Value = item.DefectTargetRate;
                        worksheet.Cell(row, 13).Value = item.DefectCurrentRate;
                        worksheet.Cell(row, 14).Value = item.DefectRate;
                        worksheet.Cell(row, 15).Value = item.CheckListOnSystem;
                        worksheet.Cell(row, 16).Value = item.Remark;
                        row++;
                    }

                    using (MemoryStream stream = new MemoryStream())
                    {
                        string fileName = "PDResultDailyReport" + DateTime.Now.ToString("yyyyMMdd") + ".xlsx";
                        workbook.SaveAs(stream);
                        //Return xlsx Excel File  
                        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = ex.Message }); 
            }
        }

        public async Task<List<PDResultDailyViewModel>> GetPDResultDailyData(DateTime fromdate, DateTime todate)
        {
            List<QtyProdResultByOperViewModel> models = new List<QtyProdResultByOperViewModel>();
            List<PDResultDailyViewModel> viewModels = new List<PDResultDailyViewModel>();
            List<PDResultDailyViewModel> dailyViewModels = new List<PDResultDailyViewModel>();
            try
            {
                if (fromdate == DateTime.MinValue)
                {
                    fromdate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                }
                if(todate == DateTime.MinValue)
                {
                    todate = DateTime.Now.Date.AddDays(-1).AddHours(23).AddMinutes(59);
                }

                if(fromdate.Date > DateTime.Now.Date)
                {
                    fromdate = DateTime.Now.Date;
                }

                if (todate.Date > DateTime.Now.Date)
                {
                    todate = DateTime.Now.Date.AddHours(23).AddMinutes(59);
                }

                if (fromdate.Date > todate.Date)
                {
                    fromdate = todate;
                    todate = todate.Date.AddHours(23).AddMinutes(59);
                }
                else if (fromdate.Date == todate.Date)
                {
                    todate = todate.Date.AddHours(23).AddMinutes(59);
                }

                ViewBag.FromDate = fromdate;
                ViewBag.ToDate = todate;
                //List<OperInfo> opers = operInfoConfig.OperInfo;

                var appSettingDataPortal = new SVN_AppSettingDataPortal(connectionString);
                var operInfo = await appSettingDataPortal.GetOperInfoConfig();
                List<OperInfo> opers = operInfo.OperInfo;

                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.GetDataForReport(fromdate, todate, opers, "FG");
                if (models != null && models.Count > 0)
                {
                    foreach (var model in models)
                    {
                        PDResultDailyViewModel viewModel = new PDResultDailyViewModel();
                        viewModel.MasterOperation = model.MasterOperation;
                        viewModel.OperationActive = model.Operation;
                        viewModel.DailyPlanTarget = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "H.Plan")?.Target ?? 0, appConfig.Rounding).ToString();
                        viewModel.DailyPlanCurrent = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "H.Plan")?.Current ?? 0, appConfig.Rounding).ToString();
                        viewModel.DailyPlanAchieve = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "H.Plan")?.Percent ?? 0, appConfig.Rounding).ToString() + "%";
                        viewModel.UPH = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "UPH")?.Percent ?? 0, appConfig.Rounding).ToString() + "%";
                        viewModel.UPPH = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "UPPH")?.Percent ?? 0, appConfig.Rounding).ToString() + "%";
                        viewModel.Labor = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "Labor")?.Percent ?? 0, appConfig.Rounding).ToString() + "%";
                        //viewModel.DefectTargetRate = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "Defect")?.Target ?? 0, appConfig.Rounding).ToString() + "%";
                        //viewModel.DefectCurrentRate = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "Defect")?.Current ?? 0, appConfig.Rounding).ToString() + "%";
                        //viewModel.DefectRate = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "Defect")?.Percent ?? 0, appConfig.Rounding).ToString() + "%";

                        viewModel.DefectTarget = model.TargetViewModels.FirstOrDefault(x => x.Item == "Defect")?.Target ?? 0;
                        viewModel.DefectCurrent = model.TargetViewModels.FirstOrDefault(x => x.Item == "Defect")?.Current ?? 0;
                        viewModel.QuantityResultCurrent = model.TargetViewModels.FirstOrDefault(x => x.Item == "H.Plan")?.Current ?? 0;


                        viewModel.DefectTargetRate = Math.Round(viewModel.DefectTarget * 100, appConfig.Rounding).ToString() + "%";
                        viewModel.DefectCurrentRate = Math.Round(viewModel.QuantityResultCurrent != 0? (viewModel.DefectCurrent / viewModel.QuantityResultCurrent) * 100 : 0, appConfig.Rounding).ToString() + "%";
                        viewModel.DefectRate = Math.Round(viewModel.DefectTarget != 0 && viewModel.QuantityResultCurrent != 0 ? (viewModel.DefectCurrent / viewModel.QuantityResultCurrent / viewModel.DefectTarget) * 100 : 0, appConfig.Rounding).ToString() + "%";
                        viewModel.CheckListOnSystem = "OK";
                        viewModel.Remark = model.DefectByCategoryViewModels.Where(x => x.value != "0").Count() > 0 ? "Defect reason:" + Environment.NewLine + string.Join(Environment.NewLine, model.DefectByCategoryViewModels.Where(x => x.value != "0").Select(x => $"{x.category}: {x.value}")) : string.Empty;
                        if (model.CanProductionByCheclist)
                        {
                            viewModel.CheckListOnSystem = "OK";
                        }
                        viewModel.Datetime = model.WorkTime;
                        dailyViewModels.Add(viewModel);
                    }

                    // sum dữ liệu theo master oper và date_time
                    var grouped = dailyViewModels
                    .GroupBy(x => new { x.MasterOperation, x.Datetime })
                    .Select(g => new PDResultDailyViewModel
                    {

                        MasterOperation = g.Key.MasterOperation,
                        OperationActive = g.Key.MasterOperation,
                        Datetime = g.Key.Datetime,

                        DailyPlanTarget = g.Sum(x => double.TryParse(x.DailyPlanTarget, out var v) ? v : 0).ToString("N0"),
                        DailyPlanCurrent = g.Sum(x => double.TryParse(x.DailyPlanCurrent, out var v) ? v : 0).ToString("N0"),
                        DailyPlanAchieve = g.Sum(x => double.TryParse(x.DailyPlanTarget, out var v) ? v : 0) == 0
                        ? "0%"
                        : ((g.Sum(x => double.TryParse(x.DailyPlanCurrent, out var v2) ? v2 : 0) /
                            g.Sum(x => double.TryParse(x.DailyPlanTarget, out var v3) ? v3 : 0)) * 100).ToString("0.##") + "%",

                        UPH = g.Average(x => double.TryParse(x.UPH?.Replace("%", ""), out var v) ? v : 0).ToString("0.##") + "%",
                        UPPH = g.Average(x => double.TryParse(x.UPPH?.Replace("%", ""), out var v) ? v : 0).ToString("0.##") + "%",
                        Labor = g.Average(x => double.TryParse(x.Labor?.Replace("%", ""), out var v) ? v : 0).ToString("0.##") + "%",

                        DefectTarget = g.Average(x => x.DefectTarget),
                        DefectCurrent = g.Sum(x => x.DefectCurrent),
                        QuantityResultCurrent = g.Sum(x => x.QuantityResultCurrent),

                        //DefectTargetRate = g.Sum(x => double.TryParse(x.DefectTargetRate?.Replace("%", ""), out var v) ? v : 0).ToString("0.##") + "%",
                        //DefectCurrentRate = g.Sum(x => double.TryParse(x.DefectCurrentRate?.Replace("%", ""), out var v) ? v : 0).ToString("0.##") + "%",
                        //DefectRate = g.Sum(x => double.TryParse(x.DefectRate?.Replace("%", ""), out var v) ? v : 0).ToString("0.##") + "%",

                        //CheckListOnSystem = g.All(x => x.CheckListOnSystem == "OK") ? "OK" : "NG",
                        Remark = string.Join("; ", g.Where(x => !string.IsNullOrEmpty(x.Remark)).Select(x => x.Remark))
                    }).ToList();

                    foreach (var item in grouped)
                    {
                        var defectTarget = item.DefectTarget;
                        var defectCurrent = item.QuantityResultCurrent != 0 ? (item.DefectCurrent / item.QuantityResultCurrent) : 0;
                        var defectRate = item.DefectTarget != 0 ?(defectCurrent / item.DefectTarget) : 0;

                        item.DefectTargetRate = Math.Round(defectTarget * 100, appConfig.Rounding).ToString() + "%";
                        item.DefectCurrentRate = Math.Round(defectCurrent * 100, appConfig.Rounding).ToString() + "%";
                        item.DefectRate = Math.Round(defectRate * 100, appConfig.Rounding).ToString() + "%";
                    }
                    grouped = grouped
                    .OrderBy(x => DateTime.ParseExact(x.Datetime, "yyyyMMdd", null))
                    .ToList();
                    string strUPHData = GetUPHDataByDayByDay(grouped);
                    ViewBag.strUPHData = strUPHData;
                    string strUPPHData = GetUPPHDataByDayByDay(grouped);
                    ViewBag.strUPPHData = strUPPHData;
                    string strLaborData = GetLaborDataByDayByDay(grouped);
                    ViewBag.strLaborData = strLaborData;
                    string strDefectRateData = GetDefectRateDataByDayByDay(grouped);
                    ViewBag.strDefectRateData = strDefectRateData;


                    // sum dữ liệu theo master oper
                    List<string> operations = models.Select(x => x.MasterOperation).Distinct().ToList();
                    foreach (var oper in operations)
                    {
                        var totalPlan = models.Where(x => x.MasterOperation == oper).Sum(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "H.Plan")?.Target ?? 0);
                        var totalPlanCurrent = models.Where(x => x.MasterOperation == oper).Sum(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "H.Plan")?.Current ?? 0);
                        var totalPlanAchieve = totalPlan == 0 ? 0 : (totalPlanCurrent / totalPlan) * 100;

                        var totalUPH = models.Where(x => x.MasterOperation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "UPH")?.Current ?? 0);
                        var totalUPHTarget = models.Where(x => x.MasterOperation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "UPH")?.Target ?? 0);
                        var totalUPHRate = totalUPHTarget == 0 ? 0 : (totalUPH / totalUPHTarget) * 100;

                        var totalUPPH = models.Where(x => x.MasterOperation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "UPPH")?.Current ?? 0);
                        var totalUPPHTarget = models.Where(x => x.MasterOperation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "UPPH")?.Target ?? 0);
                        var totalUPPHRate = totalUPPHTarget == 0 ? 0 : (totalUPPH / totalUPPHTarget) * 100;

                        var totalLabor = models.Where(x => x.MasterOperation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "Labor")?.Current ?? 0);
                        var totalLaborTarget = models.Where(x => x.MasterOperation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "Labor")?.Target ?? 0);
                        var totalLaborRate = totalLaborTarget == 0 ? 0 : (totalLabor / totalLaborTarget) * 100;

                        var totalDefect = models.Where(x => x.MasterOperation == oper).Sum(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "Defect")?.Current ?? 0);
                        var totalDefectTarget = models.Where(x => x.MasterOperation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "Defect")?.Target ?? 0);
                        var totalDefectRate = totalDefectTarget == 0 ? 0 : (totalDefect / totalDefectTarget) * 100;

                        totalDefectTarget = totalDefectTarget * 100;
                        totalDefect = totalPlanCurrent != 0 ? (totalDefect / totalPlanCurrent) * 100 : 0;
                        totalDefectRate = totalDefectTarget == 0 ? 0 : (totalDefect / totalDefectTarget) * 100;

                        PDResultDailyViewModel viewModel = new PDResultDailyViewModel();
                        viewModel.OperationActive = oper;
                        viewModel.DailyPlanTarget = Math.Round(totalPlan, appConfig.Rounding).ToString();
                        viewModel.DailyPlanCurrent = Math.Round(totalPlanCurrent, appConfig.Rounding).ToString();
                        viewModel.DailyPlanAchieve = Math.Round(totalPlanAchieve, appConfig.Rounding).ToString() + "%";
                        viewModel.UPH = Math.Round(totalUPHRate, appConfig.Rounding).ToString() + "%";
                        viewModel.UPPH = Math.Round(totalUPPHRate, appConfig.Rounding).ToString() + "%";
                        viewModel.Labor = Math.Round(totalLaborRate, appConfig.Rounding).ToString() + "%";
                        viewModel.DefectTargetRate = Math.Round(totalDefectTarget, appConfig.Rounding).ToString() + "%";
                        viewModel.DefectCurrentRate = Math.Round(totalDefect, appConfig.Rounding).ToString() + "%";
                        viewModel.DefectRate = Math.Round(totalDefectRate, appConfig.Rounding).ToString() + "%";
                        viewModel.CheckListOnSystem = models.Where(x => x.MasterOperation == oper).All(x => x.CanProductionByCheclist) ? "OK" : "NG";

                        var remarkList = models
                            .Where(x => x.MasterOperation == oper)
                            .SelectMany(x => x.DefectByCategoryViewModels
                                .Where(y => y.value != "0")
                                .Select(y => new { y.category, Value = int.Parse(y.value) })) // ép value sang số
                            .GroupBy(x => x.category)
                            .Select(g => $"{g.Key}: {g.Sum(x => x.Value)}")
                            .ToList();
                        viewModel.Remark = remarkList.Any()
                                            ? "Defect reason:" + Environment.NewLine + string.Join(Environment.NewLine, remarkList)
                                            : string.Empty;
                        viewModels.Add(viewModel);
                    }

                    return viewModels;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        public string GetUPHDataByDayByDay(List<PDResultDailyViewModel> grouped)
        {
            var chartData = grouped
            .GroupBy(x => x.Datetime)
            .Select(g =>
            {
                var dict = new Dictionary<string, object>();

                // Nếu g.Key đang là string kiểu "20250911"
                DateTime dt;
                if (DateTime.TryParseExact(g.Key, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out dt))
                {
                    dict["date"] = dt.ToString("dd/MM/yyyy"); // đổi format
                }
                else
                {
                    dict["date"] = g.Key; // fallback
                }
                foreach (var item in g)
                {
                    double value;
                    if (double.TryParse(item.UPH.Replace("%", ""), out value))
                    {
                        dict[item.OperationActive] = value;
                    }
                    else
                    {
                        dict[item.OperationActive] = 0;
                    }
                }
                return dict;
            })
            .ToList();
            return JsonConvert.SerializeObject(chartData);
        }

        public string GetUPPHDataByDayByDay(List<PDResultDailyViewModel> grouped)
        {
            var chartData = grouped
                .GroupBy(x => x.Datetime)
                .Select(g =>
                {
                    var dict = new Dictionary<string, object>();

                    // Nếu g.Key đang là string kiểu "20250911"
                    DateTime dt;
                    if (DateTime.TryParseExact(g.Key, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out dt))
                    {
                        dict["date"] = dt.ToString("dd/MM/yyyy"); // đổi format
                    }
                    else
                    {
                        dict["date"] = g.Key; // fallback
                    }
                    foreach (var item in g)
                    {
                        double value;
                        if (double.TryParse(item.UPPH.Replace("%", ""), out value))
                        {
                            dict[item.OperationActive] = value;
                        }
                        else
                        {
                            dict[item.OperationActive] = 0;
                        }
                    }
                    return dict;
                })
                .ToList();
            return JsonConvert.SerializeObject(chartData);
        }

        public string GetLaborDataByDayByDay(List<PDResultDailyViewModel> grouped)
        {
            var chartData = grouped
                .GroupBy(x => x.Datetime)
                .Select(g =>
                {
                    var dict = new Dictionary<string, object>();

                    // Nếu g.Key đang là string kiểu "20250911"
                    DateTime dt;
                    if (DateTime.TryParseExact(g.Key, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out dt))
                    {
                        dict["date"] = dt.ToString("dd/MM/yyyy"); // đổi format
                    }
                    else
                    {
                        dict["date"] = g.Key; // fallback
                    }
                    foreach (var item in g)
                    {
                        double value;
                        if (double.TryParse(item.Labor.Replace("%", ""), out value))
                        {
                            dict[item.OperationActive] = value;
                        }
                        else
                        {
                            dict[item.OperationActive] = 0;
                        }
                    }
                    return dict;
                })
                .ToList();
            return JsonConvert.SerializeObject(chartData);
        }

        public string GetDefectRateDataByDayByDay(List<PDResultDailyViewModel> grouped)
        {
            var chartData = grouped
                .GroupBy(x => x.Datetime)
                .Select(g =>
                {
                    var dict = new Dictionary<string, object>();

                    // Nếu g.Key đang là string kiểu "20250911"
                    DateTime dt;
                    if (DateTime.TryParseExact(g.Key, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out dt))
                    {
                        dict["date"] = dt.ToString("dd/MM/yyyy"); // đổi format
                    }
                    else
                    {
                        dict["date"] = g.Key; // fallback
                    }
                    foreach (var item in g)
                    {
                        double value;
                        if (double.TryParse(item.DefectRate.Replace("%", ""), out value))
                        {
                            dict[item.OperationActive] = value;
                        }
                        else
                        {
                            dict[item.OperationActive] = 0;
                        }
                    }
                    return dict;
                })
                .ToList();
            return JsonConvert.SerializeObject(chartData);
        }


        public async Task<IActionResult> PDResultDailyReportV0(DateTime fromdate, DateTime todate, string itemType = "ALL")
        {
            List<PDResultDailyViewModel> viewModels = new List<PDResultDailyViewModel>();
            try
            {
                viewModels = await GetPDResultDailyDataV0(fromdate, todate, itemType);
                return View(viewModels);
            }
            catch (Exception ex)
            {
                return View(viewModels);
            }
        }

        public async Task<IActionResult> ExportPDResultDailyV0(DateTime fromdate, DateTime todate, string itemType = "ALL")
        {
            try
            {
                var viewModels = await GetPDResultDailyDataV0(fromdate, todate, itemType);
                if (viewModels == null || viewModels.Count == 0)
                {
                    return RedirectToAction("PDResultDailyReport");
                }
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("PD Result Daily Report");
                    worksheet.Cell(1, 1).Value = "Production Result Daily";
                    worksheet.Cell(2, 1).Value = "Operation active";
                    worksheet.Cell(2, 2).Value = "Monthly Plan Achieve";
                    worksheet.Cell(2, 6).Value = "Daily Plan Achieve";
                    worksheet.Cell(2, 9).Value = "UPH";
                    worksheet.Cell(2, 12).Value = "UPPH";
                    worksheet.Cell(2, 13).Value = "Labor";
                    worksheet.Cell(2, 14).Value = "Defect Target Rate";
                    worksheet.Cell(2, 17).Value = "Check list on system";
                    worksheet.Cell(2, 18).Value = "Remark";
                    worksheet.Cell(3, 2).Value = "ERP WO #";
                    worksheet.Cell(3, 3).Value = "Plan";
                    worksheet.Cell(3, 4).Value = "Done";
                    worksheet.Cell(3, 5).Value = "%";
                    worksheet.Cell(3, 6).Value = "Target";
                    worksheet.Cell(3, 7).Value = "Current";
                    worksheet.Cell(3, 8).Value = "%";
                    worksheet.Cell(3, 9).Value = "Target";
                    worksheet.Cell(3, 10).Value = "Current";
                    worksheet.Cell(3, 11).Value = "%";
                    worksheet.Cell(3, 14).Value = "Target";
                    worksheet.Cell(3, 15).Value = "Current";
                    worksheet.Cell(3, 16).Value = "%";

                    worksheet.Range(1, 1, 1, 16).Merge();
                    worksheet.Range(2, 1, 3, 1).Merge();
                    worksheet.Range(2, 2, 2, 5).Merge();
                    worksheet.Range(2, 6, 2, 8).Merge();
                    worksheet.Range(2, 9, 2, 11).Merge();
                    worksheet.Range(2, 12, 3, 12).Merge();
                    worksheet.Range(2, 13, 3, 13).Merge();
                    worksheet.Range(2, 14, 2, 16).Merge();
                    worksheet.Range(2, 17, 3, 17).Merge();
                    worksheet.Range(2, 18, 3, 18).Merge();

                    int row = 4;
                    foreach (var item in viewModels)
                    {
                        worksheet.Cell(row, 1).Value = item.OperationActive;
                        worksheet.Cell(row, 6).Value = item.DailyPlanTarget;
                        worksheet.Cell(row, 7).Value = item.DailyPlanCurrent;
                        worksheet.Cell(row, 8).Value = item.DailyPlanAchieve;
                        worksheet.Cell(row, 9).Value = item.UPHTarget;
                        worksheet.Cell(row, 10).Value = item.UPHCurrent;
                        worksheet.Cell(row, 11).Value = item.UPH;
                        worksheet.Cell(row, 12).Value = item.UPPH;
                        worksheet.Cell(row, 13).Value = item.Labor;
                        worksheet.Cell(row, 14).Value = item.DefectTargetRate;
                        worksheet.Cell(row, 15).Value = item.DefectCurrentRate;
                        worksheet.Cell(row, 16).Value = item.DefectRate;
                        worksheet.Cell(row, 17).Value = item.CheckListOnSystem;
                        worksheet.Cell(row, 18).Value = item.Remark;
                        row++;
                    }

                    using (MemoryStream stream = new MemoryStream())
                    {
                        string fileName = "PDResultDailyReport" + DateTime.Now.ToString("yyyyMMdd") + ".xlsx";
                        workbook.SaveAs(stream);
                        //Return xlsx Excel File  
                        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = ex.Message });
            }
        }


        /// <summary>
        /// Hàm lấy dữ liệu sản xuất thời gian thực
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public async Task<List<PDResultDailyViewModel>> GetPDResultDailyDataV0(DateTime fromdate, DateTime todate, string itemType)
        {
            List<QtyProdResultByOperViewModel> models = new List<QtyProdResultByOperViewModel>();
            List<PDResultDailyViewModel> viewModels = new List<PDResultDailyViewModel>();
            List<PDResultDailyViewModel> dailyViewModels = new List<PDResultDailyViewModel>();
            try
            {
                string storedProceduce = "SVN_Pro_CalTarget_Viindoo";
                string strdate = "20241220";
                string tableName = "SVN_Production_result_Viindoo";

                if (fromdate == DateTime.MinValue)
                {
                    fromdate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
                }
                if (todate == DateTime.MinValue)
                {
                    todate = DateTime.Now.Date.AddDays(-1).AddHours(23).AddMinutes(59);
                }

                if (fromdate.Date > DateTime.Now.Date)
                {
                    fromdate = DateTime.Now.Date;
                }

                if (todate.Date > DateTime.Now.Date)
                {
                    todate = DateTime.Now.Date.AddHours(23).AddMinutes(59);
                }

                if (fromdate.Date > todate.Date)
                {
                    fromdate = todate;
                    todate = todate.Date.AddHours(23).AddMinutes(59);
                }
                else if (fromdate.Date == todate.Date)
                {
                    todate = todate.Date.AddHours(23).AddMinutes(59);
                }

                ViewBag.FromDate = fromdate;
                ViewBag.ToDate = todate;
                ViewBag.ItemType = itemType;

                //List<string> opers = appConfig.OperList.Split(",").ToList();
                List<OperInfo> opers = operInfoConfig.OperInfo;
                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                //models = await dataPortal.SummaryData(strdate, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString, 0);
                models = await dataPortal.GetDataForReport(fromdate, todate, opers, itemType);
                if (models != null && models.Count > 0)
                {
                    //models = models.Where(x => x.IsProduction).OrderByDescending(x => x.CanProductionByCheclist).OrderByDescending(x => x.IsProduction).ToList();
                    // sum dữ liệu theo master oper
                    List<string> operations = models.Select(x => x.Operation).Distinct().ToList();
                    foreach (var oper in operations)
                    {
                        var totalPlan = models.Where(x => x.Operation == oper).Sum(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "H.Plan")?.Target ?? 0);
                        var totalPlanCurrent = models.Where(x => x.Operation == oper).Sum(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "H.Plan")?.Current ?? 0);
                        var totalPlanAchieve = totalPlan == 0 ? 0 : (totalPlanCurrent / totalPlan) * 100;

                        var totalUPH = models.Where(x => x.Operation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "UPH")?.Current ?? 0);
                        var totalUPHTarget = models.Where(x => x.Operation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "UPH")?.Target ?? 0);
                        var totalUPHRate = totalUPHTarget == 0 ? 0 : (totalUPH / totalUPHTarget) * 100;

                        var totalUPPH = models.Where(x => x.Operation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "UPPH")?.Current ?? 0);
                        var totalUPPHTarget = models.Where(x => x.Operation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "UPPH")?.Target ?? 0);
                        var totalUPPHRate = totalUPPHTarget == 0 ? 0 : (totalUPPH / totalUPPHTarget) * 100;

                        var totalLabor = models.Where(x => x.Operation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "Labor")?.Current ?? 0);
                        var totalLaborTarget = models.Where(x => x.Operation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "Labor")?.Target ?? 0);
                        var totalLaborRate = totalLaborTarget == 0 ? 0 : (totalLabor / totalLaborTarget) * 100;

                        var totalDefect = models.Where(x => x.Operation == oper).Sum(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "Defect")?.Current ?? 0);
                        var totalDefectTarget = models.Where(x => x.Operation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "Defect")?.Target ?? 0);
                        var totalDefectRate = totalDefectTarget == 0 ? 0 : (totalDefect / totalDefectTarget) * 100;

                        totalDefectTarget = totalDefectTarget * 100;
                        totalDefect = totalPlanCurrent != 0 ? (totalDefect / totalPlanCurrent) * 100 : 0;
                        totalDefectRate = totalDefectTarget == 0 ? 0 : (totalDefect / totalDefectTarget) * 100;

                        PDResultDailyViewModel viewModel = new PDResultDailyViewModel();
                        viewModel.OperationActive = oper;
                        viewModel.DailyPlanTarget = Math.Round(totalPlan, appConfig.Rounding).ToString();
                        viewModel.DailyPlanCurrent = Math.Round(totalPlanCurrent, appConfig.Rounding).ToString();
                        viewModel.DailyPlanAchieve = Math.Round(totalPlanAchieve, appConfig.Rounding).ToString() + "%";
                        viewModel.UPHTarget = Math.Round(totalUPHTarget, appConfig.Rounding).ToString();
                        viewModel.UPHCurrent = Math.Round(totalUPH, appConfig.Rounding).ToString();
                        viewModel.UPH = Math.Round(totalUPHRate, appConfig.Rounding).ToString() + "%";
                        viewModel.UPPH = Math.Round(totalUPPHRate, appConfig.Rounding).ToString() + "%";
                        viewModel.Labor = Math.Round(totalLaborRate, appConfig.Rounding).ToString() + "%";
                        viewModel.DefectTargetRate = Math.Round(totalDefectTarget, appConfig.Rounding).ToString() + "%";
                        viewModel.DefectCurrentRate = Math.Round(totalDefect, appConfig.Rounding).ToString() + "%";
                        viewModel.DefectRate = Math.Round(totalDefectRate, appConfig.Rounding).ToString() + "%";
                        //viewModel.CheckListOnSystem = models.Where(x => x.Operation == oper).All(x => x.CanProductionByCheclist) ? "OK" : "NG";

                        var remarkList = models
                            .Where(x => x.Operation == oper)
                            .SelectMany(x => x.DefectByCategoryViewModels
                                .Where(y => y.value != "0")
                                .Select(y => new { y.category, Value = int.Parse(y.value) })) // ép value sang số
                            .GroupBy(x => x.category)
                            .Select(g => $"{g.Key}: {g.Sum(x => x.Value)}")
                            .ToList();
                        viewModel.Remark = remarkList.Any()
                                            ? "Defect reason:" + Environment.NewLine + string.Join(Environment.NewLine, remarkList)
                                            : string.Empty;
                        viewModels.Add(viewModel);
                    }

                    //Tính tổng Sản lượng Target và Curent
                    var sumTotalPlan = viewModels.Sum(x =>
                    {
                        return double.TryParse(x.DailyPlanTarget, out var value)
                            ? value
                            : 0;
                    });

                    var sumTotalCurrent = viewModels.Sum(x =>
                    {
                        return double.TryParse(x.DailyPlanCurrent, out var value)
                            ? value
                            : 0;
                    });

                    var sumPlanAchieve = sumTotalPlan == 0 ? 0 : (sumTotalCurrent / sumTotalPlan) * 100;

                    PDResultDailyViewModel sumViewModel = new PDResultDailyViewModel();
                    sumViewModel.OperationActive = "Total Quatity";
                    sumViewModel.DailyPlanTarget = Math.Round(sumTotalPlan, appConfig.Rounding).ToString();
                    sumViewModel.DailyPlanCurrent = Math.Round(sumTotalCurrent, appConfig.Rounding).ToString();
                    sumViewModel.DailyPlanAchieve = Math.Round(sumPlanAchieve, appConfig.Rounding).ToString() + "%";
                    viewModels.Add(sumViewModel);

                    return viewModels;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        public async Task<IActionResult> CostRevenueReport(DateTime date)
        {
            List<QtyProdResultByOperViewModel> models = new List<QtyProdResultByOperViewModel>();
            List<PDResultDailyViewModel> pdResultviewModels = new List<PDResultDailyViewModel>();
            List<CostDailyViewModel> costDailyViewModels = new List<CostDailyViewModel>();
            List<CostRevenueViewModel> costRevenueViewModels = new List<CostRevenueViewModel>();

            var appSettingDataPortal = new SVN_AppSettingDataPortal(connectionString);
            var operInfo = await appSettingDataPortal.GetOperInfoConfig();
            var cost = await appSettingDataPortal.GetCostPerDay();
            var companyCode = await appSettingDataPortal.GetLocalCompanyCode();
            var localCurrency = await appSettingDataPortal.GetCurrencyInfoByCode(companyCode);
            var vndRate = await appSettingDataPortal.GetVNDRate();
            var finalCurrency = "USD";
            cost = localCurrency == "VND" ? Math.Round(cost * vndRate, 0) : cost;
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
                //List<string> opers = appConfig.OperList.Split(",").ToList();
                //List<OperInfo> opers = operInfoConfig.OperInfo;

                //Thay đổi đọc setting từ csdl
                List<OperInfo> opers = operInfo.OperInfo;
                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryDataV1(strdate, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString, 3);
                if (models != null && models.Count > 0)
                {
                    pdResultviewModels = GetPDResultDailyViewModel(models, localCurrency, vndRate, out finalCurrency);

                    //Tính tổng danh thu trên ngày của tất cả operation
                    var grandTotalTargetRevenue = Math.Round(pdResultviewModels.Sum(x => double.Parse(x.TargetRevenue)), appConfig.Rounding);
                    var grandTotalActualRevenue = Math.Round(pdResultviewModels.Sum(x => double.Parse(x.ActualRevenue)), appConfig.Rounding);
                    var grandTotalRevenueRate = Math.Round(grandTotalTargetRevenue == 0 ? 0 : (grandTotalActualRevenue / grandTotalTargetRevenue) * 100, appConfig.Rounding);

                    //var finalCostResult = await GetAmountByCurrency(cost, "USD", localCurrency);
                    //try
                    //{
                    //    cost = Math.Round((double)finalCostResult.Content, 0);
                    //}
                    //catch
                    //{

                    //}
                    //cost = localCurrency == "VND" ? Math.Round(cost * vndRate, 0) : cost;

                    CostDailyViewModel costDailyViewModel1 = new CostDailyViewModel();
                    costDailyViewModel1.Currency = finalCurrency;
                    costDailyViewModel1.Cost = cost;
                    costDailyViewModel1.Date = date.ToString("dd/MM/yyyy");
                    costDailyViewModel1.TargetRevenue = grandTotalTargetRevenue;
                    costDailyViewModel1.ActualRevenue = grandTotalActualRevenue;
                    costDailyViewModel1.RevenueRate = grandTotalRevenueRate;
                    costDailyViewModels.Add(costDailyViewModel1);

                    var costAndRevenueInfo = "💸FN Cost: " + cost.ToString("N0") + " " + finalCurrency
                        + " | 💰PMC WO: " + grandTotalTargetRevenue.ToString("N0") + " " + finalCurrency
                        + " /💰PD Output: " + grandTotalActualRevenue.ToString("N0") + " " + finalCurrency
                        + " /💰Rate: " + grandTotalRevenueRate.ToString() + "%";
                    ViewBag.CostAndRevenueInfo = costAndRevenueInfo;
                    //ViewBag.LocalCurrency = finalCurrency;

                    CostRevenueViewModel todayCostVM = new CostRevenueViewModel();
                    todayCostVM.Time = "Today";
                    todayCostVM.StrDateTime = date.ToString("dd/MM/yyyy");
                    todayCostVM.Key = "Cost";
                    todayCostVM.Value = cost;
                    costRevenueViewModels.Add(todayCostVM);

                    CostRevenueViewModel todayPMCWOVM = new CostRevenueViewModel();
                    todayPMCWOVM.Time = "Today";
                    todayPMCWOVM.StrDateTime = date.ToString("dd/MM/yyyy");
                    todayPMCWOVM.Key = "PMC WO";
                    todayPMCWOVM.Value = grandTotalTargetRevenue;
                    costRevenueViewModels.Add(todayPMCWOVM);

                    CostRevenueViewModel todayPDOutputVM = new CostRevenueViewModel();
                    todayPDOutputVM.Time = "Today";
                    todayPDOutputVM.StrDateTime = date.ToString("dd/MM/yyyy");
                    todayPDOutputVM.Key = "PD Output";
                    todayPDOutputVM.Value = grandTotalActualRevenue;
                    costRevenueViewModels.Add(todayPDOutputVM);
                }

                // 21/12/2025: tính tri phí doanh thu của ngày hôm trc
                var yesterday = date.AddDays(-1);
                string stryesterday = yesterday.ToString("yyyyMMdd");
                List<QtyProdResultByOperViewModel> previousmodels = new List<QtyProdResultByOperViewModel>();
                previousmodels = await dataPortal.SummaryDataV1(stryesterday, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString, 3);

                while (previousmodels == null || previousmodels.Count == 0)
                {
                    yesterday = yesterday.AddDays(-1);
                    stryesterday = yesterday.ToString("yyyyMMdd");
                    previousmodels = await dataPortal.SummaryDataV1(stryesterday, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString, 3);
                }
                if (previousmodels != null && previousmodels.Count > 0)
                {
                    var previouspdResultviewModels = GetPDResultDailyViewModel(previousmodels, localCurrency, vndRate, out finalCurrency);
                    //Tính tổng danh thu trên ngày của tất cả operation
                    var previousgrandTotalTargetRevenue = Math.Round(previouspdResultviewModels.Sum(x => double.Parse(x.TargetRevenue)), appConfig.Rounding);
                    var previousgrandTotalActualRevenue = Math.Round(previouspdResultviewModels.Sum(x => double.Parse(x.ActualRevenue)), appConfig.Rounding);
                    var previousgrandTotalRevenueRate = Math.Round(previousgrandTotalTargetRevenue == 0 ? 0 : (previousgrandTotalActualRevenue / previousgrandTotalTargetRevenue) * 100, appConfig.Rounding);

                    CostDailyViewModel costDailyViewModel2 = new CostDailyViewModel();
                    costDailyViewModel2.Currency = finalCurrency;
                    costDailyViewModel2.Cost = cost;
                    costDailyViewModel2.Date = yesterday.ToString("dd/MM/yyyy");
                    costDailyViewModel2.TargetRevenue = previousgrandTotalTargetRevenue;
                    costDailyViewModel2.ActualRevenue = previousgrandTotalActualRevenue;
                    costDailyViewModel2.RevenueRate = previousgrandTotalRevenueRate;
                    costDailyViewModels.Add(costDailyViewModel2);

                    CostRevenueViewModel yesterdayCostVM = new CostRevenueViewModel();
                    yesterdayCostVM.Time = "Yesterday";
                    yesterdayCostVM.StrDateTime = yesterday.ToString("dd/MM/yyyy");
                    yesterdayCostVM.Key = "Cost";
                    yesterdayCostVM.Value = cost;
                    costRevenueViewModels.Add(yesterdayCostVM);

                    CostRevenueViewModel yesterdayPMCWOVM = new CostRevenueViewModel();
                    yesterdayPMCWOVM.Time = "Yesterday";
                    yesterdayPMCWOVM.StrDateTime = yesterday.ToString("dd/MM/yyyy");
                    yesterdayPMCWOVM.Key = "PMC WO";
                    yesterdayPMCWOVM.Value = previousgrandTotalTargetRevenue;
                    costRevenueViewModels.Add(yesterdayPMCWOVM);

                    CostRevenueViewModel yesterdayPDOutputVM = new CostRevenueViewModel();
                    yesterdayPDOutputVM.Time = "Yesterday";
                    yesterdayPDOutputVM.StrDateTime = yesterday.ToString("dd/MM/yyyy");
                    yesterdayPDOutputVM.Key = "PD Output";
                    yesterdayPDOutputVM.Value = previousgrandTotalActualRevenue;
                    costRevenueViewModels.Add(yesterdayPDOutputVM);

                    ViewBag.Yesterday = yesterday.ToString("dd/MM/yyyy");
                }

                costDailyViewModels = costDailyViewModels.OrderBy(x => x.Date).ToList();
                ViewBag.CostDaily = costDailyViewModels;

                //23/12/2025: Tính toán Cost và doanh thu theo năm
                //Lấy cost của 1 năm được nhập bởi PMC
                var yearCost = await appSettingDataPortal.GetCostInYear();
                var svnTargetDataPortal = new SVN_TargetDataPortal(connectionString);
                string companyStartDate = await appSettingDataPortal.GetStartDate();
                string currentDate = date.ToString("yyyyMMdd");

                DateTime startDate = DateTime.ParseExact(companyStartDate, "yyyyMMdd", null);
                if(date.Year > startDate.Year)
                {
                    companyStartDate = date.Year.ToString() + "0101";
                }

                //Tính yearcost từ thời điểm đầu tiên đến hiện tại
                startDate = DateTime.ParseExact(companyStartDate, "yyyyMMdd", null);
                int totalDays = (date.Date - startDate.Date).Days;
                yearCost = cost * totalDays;

                //Lấy target/Actual Output từ ngày bắt đầu đến hiện tại
                var yearlyTarget = await svnTargetDataPortal.ReadListTargetByDate(companyStartDate, currentDate);
                if (yearlyTarget != null)
                {
                    var costYearlyResult = GetCostPerYear(yearlyTarget, operInfo, localCurrency, vndRate);
                    if (costYearlyResult != null)
                    {
                        //var finalYearlyCostResult = await GetAmountByCurrency(yearCost, "USD", localCurrency);
                        //double yearlyCostValue = 0;
                        //try
                        //{
                        //    yearlyCostValue = (double)finalYearlyCostResult.Content;
                        //}
                        //catch
                        //{
                        //}
                        //var yearlyTargetRevenueResult = await GetAmountByCurrency(costYearlyResult.TargetRevenue, "USD", localCurrency);
                        //var yearlyActualRevenueResult = await GetAmountByCurrency(costYearlyResult.ActualRevenue, "USD", localCurrency);
                        //double yearlyTargetRevenue = 0;
                        //double yearlyActualRevenue = 0;
                        //try
                        //{
                        //    yearlyTargetRevenue = (double)yearlyTargetRevenueResult.Content;
                        //}
                        //catch
                        //{
                        //}
                        //try
                        //{
                        //    yearlyActualRevenue = (double)yearlyActualRevenueResult.Content;
                        //}
                        //catch
                        //{
                        //}
                        //double yearlyCostValue = localCurrency == "VND" ? Math.Round(yearCost * vndRate, 0) : yearCost;
                        double yearlyCostValue = yearCost;
                        double yearlyTargetRevenue = localCurrency == "VND" ? Math.Round(costYearlyResult.TargetRevenue * vndRate, 0) : costYearlyResult.TargetRevenue;
                        double yearlyActualRevenue = localCurrency == "VND" ? Math.Round(costYearlyResult.ActualRevenue * vndRate, 0) : costYearlyResult.ActualRevenue;
                        var yearlyRevenueRate = yearlyTargetRevenue == 0 ? 0 : Math.Round((yearlyActualRevenue / yearlyTargetRevenue) * 100, appConfig.Rounding);
                        var costAndRevenueYearlyInfo = "💸FN Yearly Cost: " + yearlyCostValue.ToString("N0") + " " + finalCurrency
                            + " | 💰PMC Yearly WO: " + yearlyTargetRevenue.ToString("N0") + " " + finalCurrency
                            + " |💰PD Yearly Output: " + yearlyActualRevenue.ToString("N0") + " " + finalCurrency
                            + " |💰PD/PMC Rate: " + yearlyRevenueRate.ToString() + "%";
                        ViewBag.CostAndRevenueYearlyInfo = costAndRevenueYearlyInfo;

                        CostRevenueViewModel yearlyCostVM = new CostRevenueViewModel();
                        yearlyCostVM.Time = "Yearly";
                        yearlyCostVM.StrDateTime = date.Year.ToString();
                        yearlyCostVM.Key = "Cost";
                        yearlyCostVM.Value = yearlyCostValue;
                        costRevenueViewModels.Add(yearlyCostVM);

                        CostRevenueViewModel yearlyPMCWOVM = new CostRevenueViewModel();
                        yearlyPMCWOVM.Time = "Yearly";
                        yearlyPMCWOVM.StrDateTime = date.Year.ToString();
                        yearlyPMCWOVM.Key = "PMC WO";
                        yearlyPMCWOVM.Value = yearlyTargetRevenue;
                        costRevenueViewModels.Add(yearlyPMCWOVM);

                        CostRevenueViewModel yearlyPDOutputVM = new CostRevenueViewModel();
                        yearlyPDOutputVM.Time = "Yearly";
                        yearlyPDOutputVM.StrDateTime = date.Year.ToString();
                        yearlyPDOutputVM.Key = "PD Output";
                        yearlyPDOutputVM.Value = yearlyActualRevenue;
                        costRevenueViewModels.Add(yearlyPDOutputVM);

                        //Lấy top 3 có doanh thu cao nhất và top 3 có doanh thu thấp nhất
                        
                        if (costYearlyResult.CostYearlyPerOpers != null)
                        {
                            var itemHaveRevenue = costYearlyResult.CostYearlyPerOpers.Where(x => x.ActualRevenue > 0).ToList();
                            if(itemHaveRevenue != null)
                            {
                                //var top3HighRevenue = itemHaveRevenue.OrderByDescending(x => x.ActualRevenue).Take(5).ToList();
                                //var top3LowRevenue = itemHaveRevenue.OrderBy(x => x.ActualRevenue).Take(5).OrderByDescending(x => x.ActualRevenue).ToList();

                                //ViewBag.Top3HighRevenue = top3HighRevenue;
                                //ViewBag.Top3LowRevenue = top3LowRevenue;

                                var topHigh = itemHaveRevenue
                                .OrderByDescending(x => x.ActualRevenue)
                                .Take(5)
                                .ToList();

                                var highKeys = new HashSet<string>(topHigh.Select(x => x.Operation));

                                var topLow = itemHaveRevenue
                                    .Where(x => !highKeys.Contains(x.Operation))
                                    .OrderBy(x => x.ActualRevenue)
                                    .Take(5)
                                    .OrderByDescending(x => x.ActualRevenue)
                                    .ToList();

                                ViewBag.Top3HighRevenue = topHigh;
                                ViewBag.Top3LowRevenue = topLow;
                            }
                        }
                        
                    }
                }



                List<string> statusList = new List<string>();
                foreach (var item in pdResultviewModels)
                {
                    string warning = " ⚠️ ";
                    string error = " ❌ ";
                    string issue = "❗";
                    string status = item.OperationActive + ": ";

                    if (double.Parse(item.DailyPlanAchieve.Replace("%", "")) >= 0 && double.Parse(item.DailyPlanAchieve.Replace("%", "")) <= 75)
                    {
                        status = status + "Daily plan " + error + item.DailyPlanAchieve;
                    }
                    else if (double.Parse(item.DailyPlanAchieve.Replace("%", "")) > 100)
                    {
                        status = status + "Daily plan " + warning + item.DailyPlanAchieve;
                    }
                    else if (double.Parse(item.DailyPlanAchieve.Replace("%", "")) > 75 && double.Parse(item.DailyPlanAchieve.Replace("%", "")) <= 92)
                    {
                        status = status + "Daily plan " + warning + item.DailyPlanAchieve;
                    }
                    else
                    {

                    }

                    if (double.Parse(item.UPH.Replace("%", "")) >= 0 && double.Parse(item.UPH.Replace("%", "")) <= 75)
                    {
                        status = status + " UPH " + error + item.UPH;
                    }
                    else if (double.Parse(item.UPH.Replace("%", "")) > 100)
                    {
                        status = status + " UPH " + warning + item.UPH;
                    }
                    else if (double.Parse(item.UPH.Replace("%", "")) > 75 && double.Parse(item.UPH.Replace("%", "")) <= 92)
                    {
                        status = status + " UPH " + warning + item.UPH;
                    }
                    else
                    {

                    }

                    if (double.Parse(item.UPPH.Replace("%", "")) >= 0 && double.Parse(item.UPPH.Replace("%", "")) <= 75)
                    {
                        status = status + " UPPH " + error + item.UPPH;
                    }
                    else if (double.Parse(item.UPPH.Replace("%", "")) > 100)
                    {
                        status = status + " UPPH " + warning + item.UPPH;
                    }
                    else if (double.Parse(item.UPPH.Replace("%", "")) > 75 && double.Parse(item.UPPH.Replace("%", "")) <= 92)
                    {
                        status = status + " UPPH " + warning + item.UPPH;
                    }
                    else
                    {

                    }

                    if (double.Parse(item.DefectRate.Replace("%", "")) > 100)
                    {
                        status = status + " Defect " + error + item.DefectRate;
                    }
                    else if (double.Parse(item.DefectRate.Replace("%", "")) > 75 && double.Parse(item.DefectRate.Replace("%", "")) <= 100)
                    {
                        status = status + " Defect " + warning + item.DefectRate;
                    }
                    else
                    {

                    }

                    statusList.Add(status);
                }

                ViewBag.StatusList = statusList;
                ViewBag.LocalCurrency = finalCurrency;

                return View(costRevenueViewModels);
            }
            catch (Exception ex)
            {
                costRevenueViewModels = new List<CostRevenueViewModel>();
                return View(costRevenueViewModels);

            }
        }

        /// <summary>
        /// Update thêm 1 số biểu đồ theo dõi cashin cashout 
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public async Task<IActionResult> CostRevenueReportV2(DateTime date)
        {
            List<QtyProdResultByOperViewModel> models = new List<QtyProdResultByOperViewModel>();
            List<PDResultDailyViewModel> pdResultviewModels = new List<PDResultDailyViewModel>();
            List<CostDailyViewModel> costDailyViewModels = new List<CostDailyViewModel>();
            List<CostRevenueViewModel> costRevenueViewModels = new List<CostRevenueViewModel>();

            var appSettingDataPortal = new SVN_AppSettingDataPortal(connectionString);
            var operInfo = await appSettingDataPortal.GetOperInfoConfig();
            var cost = await appSettingDataPortal.GetCostPerDay();
            var companyCode = await appSettingDataPortal.GetLocalCompanyCode();
            var localCurrency = await appSettingDataPortal.GetCurrencyInfoByCode(companyCode);
            var vndRate = await appSettingDataPortal.GetVNDRate();
            var finalCurrency = "USD";
            cost = localCurrency == "VND" ? Math.Round(cost * vndRate, 0) : cost;
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
                //List<string> opers = appConfig.OperList.Split(",").ToList();
                //List<OperInfo> opers = operInfoConfig.OperInfo;

                //Thay đổi đọc setting từ csdl
                List<OperInfo> opers = operInfo.OperInfo;
                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryDataV1(strdate, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString, 3);
                if (models != null && models.Count > 0)
                {
                    pdResultviewModels = GetPDResultDailyViewModel(models, localCurrency, vndRate, out finalCurrency);

                    //Tính tổng danh thu trên ngày của tất cả operation
                    var grandTotalTargetRevenue = Math.Round(pdResultviewModels.Sum(x => double.Parse(x.TargetRevenue)), appConfig.Rounding);
                    var grandTotalActualRevenue = Math.Round(pdResultviewModels.Sum(x => double.Parse(x.ActualRevenue)), appConfig.Rounding);
                    var grandTotalRevenueRate = Math.Round(grandTotalTargetRevenue == 0 ? 0 : (grandTotalActualRevenue / grandTotalTargetRevenue) * 100, appConfig.Rounding);

                    //var finalCostResult = await GetAmountByCurrency(cost, "USD", localCurrency);
                    //try
                    //{
                    //    cost = Math.Round((double)finalCostResult.Content, 0);
                    //}
                    //catch
                    //{

                    //}
                    //cost = localCurrency == "VND" ? Math.Round(cost * vndRate, 0) : cost;

                    CostDailyViewModel costDailyViewModel1 = new CostDailyViewModel();
                    costDailyViewModel1.Currency = finalCurrency;
                    costDailyViewModel1.Cost = cost;
                    costDailyViewModel1.Date = date.ToString("dd/MM/yyyy");
                    costDailyViewModel1.TargetRevenue = grandTotalTargetRevenue;
                    costDailyViewModel1.ActualRevenue = grandTotalActualRevenue;
                    costDailyViewModel1.RevenueRate = grandTotalRevenueRate;
                    costDailyViewModels.Add(costDailyViewModel1);

                    var costAndRevenueInfo = "💸FN Cost: " + cost.ToString("N0") + " " + finalCurrency
                        + " | 💰PMC WO: " + grandTotalTargetRevenue.ToString("N0") + " " + finalCurrency
                        + " /💰PD Output: " + grandTotalActualRevenue.ToString("N0") + " " + finalCurrency
                        + " /💰Rate: " + grandTotalRevenueRate.ToString() + "%";
                    ViewBag.CostAndRevenueInfo = costAndRevenueInfo;
                    //ViewBag.LocalCurrency = finalCurrency;

                    CostRevenueViewModel todayCostVM = new CostRevenueViewModel();
                    todayCostVM.Time = "Today";
                    todayCostVM.StrDateTime = date.ToString("dd/MM/yyyy");
                    todayCostVM.Key = "Cost";
                    todayCostVM.Value = cost;
                    costRevenueViewModels.Add(todayCostVM);

                    CostRevenueViewModel todayPMCWOVM = new CostRevenueViewModel();
                    todayPMCWOVM.Time = "Today";
                    todayPMCWOVM.StrDateTime = date.ToString("dd/MM/yyyy");
                    todayPMCWOVM.Key = "PMC WO";
                    todayPMCWOVM.Value = grandTotalTargetRevenue;
                    costRevenueViewModels.Add(todayPMCWOVM);

                    CostRevenueViewModel todayPDOutputVM = new CostRevenueViewModel();
                    todayPDOutputVM.Time = "Today";
                    todayPDOutputVM.StrDateTime = date.ToString("dd/MM/yyyy");
                    todayPDOutputVM.Key = "PD Output";
                    todayPDOutputVM.Value = grandTotalActualRevenue;
                    costRevenueViewModels.Add(todayPDOutputVM);
                }

                // 21/12/2025: tính tri phí doanh thu của ngày hôm trc
                var yesterday = date.AddDays(-1);
                string stryesterday = yesterday.ToString("yyyyMMdd");
                List<QtyProdResultByOperViewModel> previousmodels = new List<QtyProdResultByOperViewModel>();
                previousmodels = await dataPortal.SummaryDataV1(stryesterday, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString, 3);

                while (previousmodels == null || previousmodels.Count == 0)
                {
                    yesterday = yesterday.AddDays(-1);
                    stryesterday = yesterday.ToString("yyyyMMdd");
                    previousmodels = await dataPortal.SummaryDataV1(stryesterday, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString, 3);
                }
                if (previousmodels != null && previousmodels.Count > 0)
                {
                    var previouspdResultviewModels = GetPDResultDailyViewModel(previousmodels, localCurrency, vndRate, out finalCurrency);
                    //Tính tổng danh thu trên ngày của tất cả operation
                    var previousgrandTotalTargetRevenue = Math.Round(previouspdResultviewModels.Sum(x => double.Parse(x.TargetRevenue)), appConfig.Rounding);
                    var previousgrandTotalActualRevenue = Math.Round(previouspdResultviewModels.Sum(x => double.Parse(x.ActualRevenue)), appConfig.Rounding);
                    var previousgrandTotalRevenueRate = Math.Round(previousgrandTotalTargetRevenue == 0 ? 0 : (previousgrandTotalActualRevenue / previousgrandTotalTargetRevenue) * 100, appConfig.Rounding);

                    CostDailyViewModel costDailyViewModel2 = new CostDailyViewModel();
                    costDailyViewModel2.Currency = finalCurrency;
                    costDailyViewModel2.Cost = cost;
                    costDailyViewModel2.Date = yesterday.ToString("dd/MM/yyyy");
                    costDailyViewModel2.TargetRevenue = previousgrandTotalTargetRevenue;
                    costDailyViewModel2.ActualRevenue = previousgrandTotalActualRevenue;
                    costDailyViewModel2.RevenueRate = previousgrandTotalRevenueRate;
                    costDailyViewModels.Add(costDailyViewModel2);

                    CostRevenueViewModel yesterdayCostVM = new CostRevenueViewModel();
                    yesterdayCostVM.Time = "Yesterday";
                    yesterdayCostVM.StrDateTime = yesterday.ToString("dd/MM/yyyy");
                    yesterdayCostVM.Key = "Cost";
                    yesterdayCostVM.Value = cost;
                    costRevenueViewModels.Add(yesterdayCostVM);

                    CostRevenueViewModel yesterdayPMCWOVM = new CostRevenueViewModel();
                    yesterdayPMCWOVM.Time = "Yesterday";
                    yesterdayPMCWOVM.StrDateTime = yesterday.ToString("dd/MM/yyyy");
                    yesterdayPMCWOVM.Key = "PMC WO";
                    yesterdayPMCWOVM.Value = previousgrandTotalTargetRevenue;
                    costRevenueViewModels.Add(yesterdayPMCWOVM);

                    CostRevenueViewModel yesterdayPDOutputVM = new CostRevenueViewModel();
                    yesterdayPDOutputVM.Time = "Yesterday";
                    yesterdayPDOutputVM.StrDateTime = yesterday.ToString("dd/MM/yyyy");
                    yesterdayPDOutputVM.Key = "PD Output";
                    yesterdayPDOutputVM.Value = previousgrandTotalActualRevenue;
                    costRevenueViewModels.Add(yesterdayPDOutputVM);

                    ViewBag.Yesterday = yesterday.ToString("dd/MM/yyyy");
                }

                costDailyViewModels = costDailyViewModels.OrderBy(x => x.Date).ToList();
                ViewBag.CostDaily = costDailyViewModels;

                //23/12/2025: Tính toán Cost và doanh thu theo năm
                //Lấy cost của 1 năm được nhập bởi PMC
                var yearCost = await appSettingDataPortal.GetCostInYear();
                

                var svnTargetDataPortal = new SVN_TargetDataPortal(connectionString);
                string companyStartDate = await appSettingDataPortal.GetStartDate();
                string currentDate = date.ToString("yyyyMMdd");

                DateTime startDate = DateTime.ParseExact(companyStartDate, "yyyyMMdd", null);
                if (date.Year > startDate.Year)
                {
                    companyStartDate = date.Year.ToString() + "0101";
                }

                //Tính yearcost từ thời điểm đầu tiên đến hiện tại
                startDate = DateTime.ParseExact(companyStartDate, "yyyyMMdd", null);
                int totalDays = (date.Date - startDate.Date).Days;
                yearCost = cost * totalDays;

                //Lấy target/Actual Output từ ngày bắt đầu đến hiện tại
                var yearlyTarget = await svnTargetDataPortal.ReadListTargetByDate(companyStartDate, currentDate);
                if (yearlyTarget != null)
                {
                    var costYearlyResult = GetCostPerYear(yearlyTarget, operInfo, localCurrency, vndRate);
                    if (costYearlyResult != null)
                    {
                        //var finalYearlyCostResult = await GetAmountByCurrency(yearCost, "USD", localCurrency);
                        //double yearlyCostValue = 0;
                        //try
                        //{
                        //    yearlyCostValue = (double)finalYearlyCostResult.Content;
                        //}
                        //catch
                        //{
                        //}
                        //var yearlyTargetRevenueResult = await GetAmountByCurrency(costYearlyResult.TargetRevenue, "USD", localCurrency);
                        //var yearlyActualRevenueResult = await GetAmountByCurrency(costYearlyResult.ActualRevenue, "USD", localCurrency);
                        //double yearlyTargetRevenue = 0;
                        //double yearlyActualRevenue = 0;
                        //try
                        //{
                        //    yearlyTargetRevenue = (double)yearlyTargetRevenueResult.Content;
                        //}
                        //catch
                        //{
                        //}
                        //try
                        //{
                        //    yearlyActualRevenue = (double)yearlyActualRevenueResult.Content;
                        //}
                        //catch
                        //{
                        //}
                        //double yearlyCostValue = localCurrency == "VND" ? Math.Round(yearCost * vndRate, 0) : yearCost;
                        double yearlyCostValue = yearCost;
                        double yearlyTargetRevenue = localCurrency == "VND" ? Math.Round(costYearlyResult.TargetRevenue * vndRate, 0) : costYearlyResult.TargetRevenue;
                        double yearlyActualRevenue = localCurrency == "VND" ? Math.Round(costYearlyResult.ActualRevenue * vndRate, 0) : costYearlyResult.ActualRevenue;
                        var yearlyRevenueRate = yearlyTargetRevenue == 0 ? 0 : Math.Round((yearlyActualRevenue / yearlyTargetRevenue) * 100, appConfig.Rounding);
                        var costAndRevenueYearlyInfo = "💸FN Yearly Cost: " + yearlyCostValue.ToString("N0") + " " + finalCurrency
                            + " | 💰PMC Yearly WO: " + yearlyTargetRevenue.ToString("N0") + " " + finalCurrency
                            + " |💰PD Yearly Output: " + yearlyActualRevenue.ToString("N0") + " " + finalCurrency
                            + " |💰PD/PMC Rate: " + yearlyRevenueRate.ToString() + "%";
                        ViewBag.CostAndRevenueYearlyInfo = costAndRevenueYearlyInfo;

                        CostRevenueViewModel yearlyCostVM = new CostRevenueViewModel();
                        yearlyCostVM.Time = "Yearly";
                        yearlyCostVM.StrDateTime = date.Year.ToString();
                        yearlyCostVM.Key = "Cost";
                        yearlyCostVM.Value = yearlyCostValue;
                        costRevenueViewModels.Add(yearlyCostVM);

                        CostRevenueViewModel yearlyPMCWOVM = new CostRevenueViewModel();
                        yearlyPMCWOVM.Time = "Yearly";
                        yearlyPMCWOVM.StrDateTime = date.Year.ToString();
                        yearlyPMCWOVM.Key = "PMC WO";
                        yearlyPMCWOVM.Value = yearlyTargetRevenue;
                        costRevenueViewModels.Add(yearlyPMCWOVM);

                        CostRevenueViewModel yearlyPDOutputVM = new CostRevenueViewModel();
                        yearlyPDOutputVM.Time = "Yearly";
                        yearlyPDOutputVM.StrDateTime = date.Year.ToString();
                        yearlyPDOutputVM.Key = "PD Output";
                        yearlyPDOutputVM.Value = yearlyActualRevenue;
                        costRevenueViewModels.Add(yearlyPDOutputVM);

                        //Lấy top 3 có doanh thu cao nhất và top 3 có doanh thu thấp nhất

                        if (costYearlyResult.CostYearlyPerOpers != null)
                        {
                            var itemHaveRevenue = costYearlyResult.CostYearlyPerOpers.Where(x => x.ActualRevenue > 0).ToList();
                            if (itemHaveRevenue != null)
                            {
                                //var top3HighRevenue = itemHaveRevenue.OrderByDescending(x => x.ActualRevenue).Take(5).ToList();
                                //var top3LowRevenue = itemHaveRevenue.OrderBy(x => x.ActualRevenue).Take(5).OrderByDescending(x => x.ActualRevenue).ToList();

                                //ViewBag.Top3HighRevenue = top3HighRevenue;
                                //ViewBag.Top3LowRevenue = top3LowRevenue;

                                var topHigh = itemHaveRevenue
                                .OrderByDescending(x => x.ActualRevenue)
                                .Take(5)
                                .ToList();

                                var highKeys = new HashSet<string>(topHigh.Select(x => x.Operation));

                                var topLow = itemHaveRevenue
                                    .Where(x => !highKeys.Contains(x.Operation))
                                    .OrderBy(x => x.ActualRevenue)
                                    .Take(5)
                                    .OrderByDescending(x => x.ActualRevenue)
                                    .ToList();

                                ViewBag.Top3HighRevenue = topHigh;
                                ViewBag.Top3LowRevenue = topLow;
                            }
                        }

                    }
                }



                List<string> statusList = new List<string>();
                foreach (var item in pdResultviewModels)
                {
                    string warning = " ⚠️ ";
                    string error = " ❌ ";
                    string issue = "❗";
                    string status = item.OperationActive + ": ";

                    if (double.Parse(item.DailyPlanAchieve.Replace("%", "")) >= 0 && double.Parse(item.DailyPlanAchieve.Replace("%", "")) <= 75)
                    {
                        status = status + "Daily plan " + error + item.DailyPlanAchieve;
                    }
                    else if (double.Parse(item.DailyPlanAchieve.Replace("%", "")) > 100)
                    {
                        status = status + "Daily plan " + warning + item.DailyPlanAchieve;
                    }
                    else if (double.Parse(item.DailyPlanAchieve.Replace("%", "")) > 75 && double.Parse(item.DailyPlanAchieve.Replace("%", "")) <= 92)
                    {
                        status = status + "Daily plan " + warning + item.DailyPlanAchieve;
                    }
                    else
                    {

                    }

                    if (double.Parse(item.UPH.Replace("%", "")) >= 0 && double.Parse(item.UPH.Replace("%", "")) <= 75)
                    {
                        status = status + " UPH " + error + item.UPH;
                    }
                    else if (double.Parse(item.UPH.Replace("%", "")) > 100)
                    {
                        status = status + " UPH " + warning + item.UPH;
                    }
                    else if (double.Parse(item.UPH.Replace("%", "")) > 75 && double.Parse(item.UPH.Replace("%", "")) <= 92)
                    {
                        status = status + " UPH " + warning + item.UPH;
                    }
                    else
                    {

                    }

                    if (double.Parse(item.UPPH.Replace("%", "")) >= 0 && double.Parse(item.UPPH.Replace("%", "")) <= 75)
                    {
                        status = status + " UPPH " + error + item.UPPH;
                    }
                    else if (double.Parse(item.UPPH.Replace("%", "")) > 100)
                    {
                        status = status + " UPPH " + warning + item.UPPH;
                    }
                    else if (double.Parse(item.UPPH.Replace("%", "")) > 75 && double.Parse(item.UPPH.Replace("%", "")) <= 92)
                    {
                        status = status + " UPPH " + warning + item.UPPH;
                    }
                    else
                    {

                    }

                    if (double.Parse(item.DefectRate.Replace("%", "")) > 100)
                    {
                        status = status + " Defect " + error + item.DefectRate;
                    }
                    else if (double.Parse(item.DefectRate.Replace("%", "")) > 75 && double.Parse(item.DefectRate.Replace("%", "")) <= 100)
                    {
                        status = status + " Defect " + warning + item.DefectRate;
                    }
                    else
                    {

                    }

                    statusList.Add(status);
                }

                ViewBag.StatusList = statusList;
                ViewBag.LocalCurrency = finalCurrency;
                
                return View(costRevenueViewModels);
            }
            catch (Exception ex)
            {
                costRevenueViewModels = new List<CostRevenueViewModel>();
                return View(costRevenueViewModels);

            }
        }
        #endregion

        #region privatelogic
        private async Task<BODataProcessResult> GetAmountByCurrency(double amount, string fromCurrency, string toCurrency)
        {
            BODataProcessResult dataProcessResult = new BODataProcessResult();
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string url = aPIConfiguration.ChangeCurrencyURL;
                    url = url.Replace("fromCurrency", fromCurrency);
                    var response = await client.GetStringAsync(url);

                    var json = JObject.Parse(response);
                    double rate = (double)json["rates"][toCurrency];

                    dataProcessResult.OK = true;
                    dataProcessResult.Message = "Change currency from " + fromCurrency + " to " + toCurrency + " OK";
                    dataProcessResult.Content = amount * rate;
                }
            }
            catch (Exception ex)
            {
                dataProcessResult.OK = false;
                dataProcessResult.Message = ex.Message;
                dataProcessResult.Content = amount;
            }
            return dataProcessResult;
        }

        private List<PDResultDailyViewModel> GetPDResultDailyViewModel(List<QtyProdResultByOperViewModel> models, string localCurrency, double vndRate, out string finalCurrency)
        {
            bool callCurrencyAPI = false;
            List<PDResultDailyViewModel> pdResultviewModels = new List<PDResultDailyViewModel>();
            foreach (var model in models)
            {
                var userInfo = qCInfoConfig.UserInfo.FirstOrDefault(x => x.Operation == model.Operation);
                if (userInfo != null)
                {
                    model.PDName = userInfo.PDName;
                    model.QCName = userInfo.QCName;
                }
            }
            models = models.Where(x => x.IsProduction).OrderByDescending(x => x.CanProductionByCheclist).OrderByDescending(x => x.IsProduction).ToList();

            // sum dữ liệu theo master oper
            List<string> operations = models.Select(x => x.MasterOperation).Distinct().ToList();
            foreach (var oper in operations)
            {
                var totalPlan = models.Where(x => x.MasterOperation == oper).Sum(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "H.Plan")?.Target ?? 0);
                var totalPlanCurrent = models.Where(x => x.MasterOperation == oper).Sum(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "H.Plan")?.Current ?? 0);
                var totalPlanAchieve = totalPlan == 0 ? 0 : (totalPlanCurrent / totalPlan) * 100;

                var totalUPH = models.Where(x => x.MasterOperation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "UPH")?.Current ?? 0);
                var totalUPHTarget = models.Where(x => x.MasterOperation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "UPH")?.Target ?? 0);
                var totalUPHRate = totalUPHTarget == 0 ? 0 : (totalUPH / totalUPHTarget) * 100;

                var totalUPPH = models.Where(x => x.MasterOperation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "UPPH")?.Current ?? 0);
                var totalUPPHTarget = models.Where(x => x.MasterOperation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "UPPH")?.Target ?? 0);
                var totalUPPHRate = totalUPPHTarget == 0 ? 0 : (totalUPPH / totalUPPHTarget) * 100;

                var totalLabor = models.Where(x => x.MasterOperation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "Labor")?.Current ?? 0);
                var totalLaborTarget = models.Where(x => x.MasterOperation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "Labor")?.Target ?? 0);
                var totalLaborRate = totalLaborTarget == 0 ? 0 : (totalLabor / totalLaborTarget) * 100;

                var totalDefect = models.Where(x => x.MasterOperation == oper).Sum(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "Defect")?.Current ?? 0);
                var totalDefectTarget = models.Where(x => x.MasterOperation == oper).Average(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "Defect")?.Target ?? 0);
                var totalDefectRate = totalDefectTarget == 0 ? 0 : (totalDefect / totalDefectTarget) * 100;

                totalDefectTarget = totalDefectTarget * 100;
                totalDefect = totalPlanCurrent != 0 ? (totalDefect / totalPlanCurrent) * 100 : 0;
                totalDefectRate = totalDefectTarget == 0 ? 0 : (totalDefect / totalDefectTarget) * 100;

                PDResultDailyViewModel viewModel = new PDResultDailyViewModel();
                viewModel.OperationActive = oper;
                viewModel.DailyPlanTarget = Math.Round(totalPlan, appConfig.Rounding).ToString();
                viewModel.DailyPlanCurrent = Math.Round(totalPlanCurrent, appConfig.Rounding).ToString();
                viewModel.DailyPlanAchieve = Math.Round(totalPlanAchieve, appConfig.Rounding).ToString() + "%";
                viewModel.UPH = Math.Round(totalUPHRate, appConfig.Rounding).ToString() + "%";
                viewModel.UPPH = Math.Round(totalUPPHRate, appConfig.Rounding).ToString() + "%";
                viewModel.Labor = Math.Round(totalLaborRate, appConfig.Rounding).ToString() + "%";
                viewModel.DefectTargetRate = Math.Round(totalDefectTarget, appConfig.Rounding).ToString() + "%";
                viewModel.DefectCurrentRate = Math.Round(totalDefect, appConfig.Rounding).ToString() + "%";
                viewModel.DefectRate = Math.Round(totalDefectRate, appConfig.Rounding).ToString() + "%";
                viewModel.CheckListOnSystem = models.Where(x => x.MasterOperation == oper).All(x => x.CanProductionByCheclist) ? "OK" : "NG";

                var remarkList = models
                    .Where(x => x.MasterOperation == oper)
                    .SelectMany(x => x.DefectByCategoryViewModels
                        .Where(y => y.value != "0")
                        .Select(y => new { y.category, Value = int.Parse(y.value) })) // ép value sang số
                    .GroupBy(x => x.category)
                    .Select(g => $"{g.Key}: {g.Sum(x => x.Value)}")
                    .ToList();
                viewModel.Remark = remarkList.Any()
                                    ? "Defect reason:" + Environment.NewLine + string.Join(Environment.NewLine, remarkList)
                                    : string.Empty;
                viewModel.UPHTarget = Math.Round(totalUPHTarget, appConfig.Rounding).ToString();
                viewModel.UPPHTarget = Math.Round(totalUPPHTarget, appConfig.Rounding).ToString();
                viewModel.LaborTarget = Math.Round(totalLaborTarget, appConfig.Rounding).ToString();
                viewModel.UPHCurrent = Math.Round(totalUPH, appConfig.Rounding).ToString();
                viewModel.UPPHCurrent = Math.Round(totalUPPH, appConfig.Rounding).ToString();
                viewModel.LaborCurrent = Math.Round(totalLabor, appConfig.Rounding).ToString();

                //2025/12/12: Tính toán doanh thu cho từng công đoạn
                var totalTargetRevenue = models.Where(x => x.MasterOperation == oper).Sum(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "H.Plan")?.TargetRevenue ?? 0);
                var totalActualRevenue = models.Where(x => x.MasterOperation == oper).Sum(x => x.TargetViewModels.FirstOrDefault(y => y.Item == "H.Plan")?.ActualRevenue ?? 0);
                var totalRevenueRate = totalTargetRevenue == 0 ? 0 : (totalActualRevenue / totalTargetRevenue) * 100;

                //var targetRResult = GetAmountByCurrency(totalTargetRevenue, "USD", localCurrency).GetAwaiter().GetResult();
                //if (targetRResult != null)
                //{
                //    try
                //    {
                //        totalTargetRevenue = (double)targetRResult.Content;
                //    }
                //    catch
                //    {

                //    }
                //    if (targetRResult.OK)
                //    {
                //        callCurrencyAPI = true;
                //        //finalCurrency = localCurrency;
                //    }
                //    else
                //    {
                //        callCurrencyAPI = false;
                //        //finalCurrency = "USD";
                //    }
                //}
                //var actualRResult = GetAmountByCurrency(totalActualRevenue, "USD", localCurrency).GetAwaiter().GetResult();
                //if (actualRResult != null)
                //{
                //    try
                //    {
                //        totalActualRevenue = (double)actualRResult.Content;
                //    }
                //    catch
                //    {

                //    }
                //    if (actualRResult.OK)
                //    {
                //        callCurrencyAPI = true;
                //        //finalCurrency = localCurrency;
                //    }
                //    else
                //    {
                //        callCurrencyAPI = false;
                //        //finalCurrency = "USD";
                //    }
                //}

                totalTargetRevenue = localCurrency == "VND" ? Math.Round(totalTargetRevenue * vndRate, 0) : totalTargetRevenue;
                totalActualRevenue = localCurrency == "VND" ? Math.Round(totalActualRevenue * vndRate, 0) : totalActualRevenue;


                viewModel.TargetRevenue = Math.Round(totalTargetRevenue, appConfig.Rounding).ToString("N0");
                viewModel.ActualRevenue = Math.Round(totalActualRevenue, appConfig.Rounding).ToString("N0");
                viewModel.RevenueRate = Math.Round(totalRevenueRate, appConfig.Rounding).ToString() + "%";

                pdResultviewModels.Add(viewModel);
            }
            //if(callCurrencyAPI == false)
            //{
            //    finalCurrency = "USD";
            //}
            //else
            //{
            //    finalCurrency = localCurrency;
            //}

            finalCurrency = localCurrency;

            return pdResultviewModels;
        }

        //Hàm tính tri phí, doanh thu theo năm
        private CostDailyViewModel GetCostPerYear(List<SVN_target> sVN_Targets, OperInfoConfig operInfoConfig, string localCurrency = "VND", double SVNRate = 26152.137911)
        {
            CostDailyViewModel costDailyViewModel = new CostDailyViewModel();
            foreach(var item in operInfoConfig.OperInfo)
            {
                var dataByOper = sVN_Targets.Where(x => x.Operation == item.Operation).ToList();
                if(dataByOper != null && dataByOper.Count > 0)
                {
                    var sumTargetQtyByOper = dataByOper.Sum(x => x.Daily_plan);
                    var sumActualQtyByOper = dataByOper.Sum(x => x.Total_Qty);

                    var sumTargetRevenueByOper = sumTargetQtyByOper * item.Price;
                    var sumActualRevenueByOper = sumActualQtyByOper * item.Price;

                    costDailyViewModel.TargetRevenue = costDailyViewModel.TargetRevenue + sumTargetRevenueByOper;
                    costDailyViewModel.ActualRevenue = costDailyViewModel.ActualRevenue + sumActualRevenueByOper;

                    CostYearlyPerOperViewModel costYearlyPerOperViewModel = new CostYearlyPerOperViewModel();
                    costYearlyPerOperViewModel.Operation = item.Operation;
                    costYearlyPerOperViewModel.TargetOutput = sumTargetQtyByOper;
                    costYearlyPerOperViewModel.ActualOutput = sumActualQtyByOper;

                    //var yearlyTargetRevenueResult = GetAmountByCurrency(sumTargetRevenueByOper, "USD", localCurrency).GetAwaiter().GetResult();
                    //var yearlyActualRevenueResult = GetAmountByCurrency(sumActualRevenueByOper, "USD", localCurrency).GetAwaiter().GetResult();
                    //double yearlyTargetRevenue = 0;
                    //double yearlyActualRevenue = 0;
                    //try
                    //{
                    //    yearlyTargetRevenue = (double)yearlyTargetRevenueResult.Content;
                    //}
                    //catch
                    //{
                    //}
                    //try
                    //{
                    //    yearlyActualRevenue = (double)yearlyActualRevenueResult.Content;
                    //}
                    //catch
                    //{
                    //}
                    double yearlyTargetRevenue = localCurrency == "VND" ? Math.Round(sumTargetRevenueByOper * SVNRate, 0) : sumTargetRevenueByOper;
                    double yearlyActualRevenue = localCurrency == "VND" ? Math.Round(sumActualRevenueByOper * SVNRate, 0) : sumActualRevenueByOper;

                    costYearlyPerOperViewModel.TargetRevenue = yearlyTargetRevenue;
                    costYearlyPerOperViewModel.ActualRevenue = yearlyActualRevenue;

                    costDailyViewModel.CostYearlyPerOpers.Add(costYearlyPerOperViewModel);
                }
            }
            costDailyViewModel.RevenueRate = costDailyViewModel.TargetRevenue == 0 ? 0 : (costDailyViewModel.ActualRevenue / costDailyViewModel.TargetRevenue) * 100;
            return costDailyViewModel;
        }

        /// <summary>
        /// convert time
        /// </summary>
        /// <param name="time"></param>
        /// <returns></returns>
        private TimeSpan GetStartTime(string time)
        {
            var part = time.Split('-')[0];   // "10h10"
            var arr = part.Split('h');

            int hour = int.Parse(arr[0]);
            int minute = arr.Length > 1 && arr[1] != ""
                ? int.Parse(arr[1])
                : 0;

            return new TimeSpan(hour, minute, 0);
        }

        #endregion
    }
}
