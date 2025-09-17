using Microsoft.AspNetCore.Mvc;
using SVN_Portal.DAL.DataPortal;
using SVN_Portal.Models;
using SVN_Portal.Services.Configurations;
using SVNShareLib;

namespace SVN_Portal.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class APIServicesController : Controller
    {
        AppConfig appConfig;
        DBConfiguration dBConfiguration;
        string connectionString;
        QCInfoConfig qCInfoConfig;
        OperInfoConfig operInfoConfig;

        public APIServicesController(AppConfig appConfig,
            DBConfiguration dBConfiguration,
            OperInfoConfig operInfoConfig,
            QCInfoConfig qCInfoConfig)
        {
            this.appConfig = appConfig;
            this.dBConfiguration = dBConfiguration;
            connectionString = dBConfiguration.GetConnectionString();
            this.qCInfoConfig = qCInfoConfig;
            this.operInfoConfig = operInfoConfig;
        }

        [Route("GetPDResultRealtime")]
        [HttpPost]
        public async Task<BODataProcessResult> GetPDResultRealtime()
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                var data = await GetPDResultDailyDataV0(DateTime.Now);
                if(data != null && data.Count > 0)
                {
                    processResult.OK = true;
                    processResult.Content = data;
                    processResult.Message = "Lấy dữ liệu thành công";
                }
                else
                {
                    processResult.OK = false;
                    processResult.Message = "Không có dữ liệu";
                }
            }
            catch(Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return processResult;
        }

        /// <summary>
        /// Hàm lấy dữ liệu sản xuất thời gian thực
        /// </summary>
        /// <param name="date"></param>
        /// <returns></returns>
        public async Task<List<PDResultDailyViewModel>> GetPDResultDailyDataV0(DateTime date)
        {
            List<QtyProdResultByOperViewModel> models = new List<QtyProdResultByOperViewModel>();
            List<PDResultDailyViewModel> viewModels = new List<PDResultDailyViewModel>();
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
                List<OperInfo> opers = operInfoConfig.OperInfo;
                var dataPortal = new SVN_production_resultDataPortal(connectionString);
                models = await dataPortal.SummaryData(strdate, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString, 0);
                if (models != null && models.Count > 0)
                {
                    models = models.Where(x => x.IsProduction).OrderByDescending(x => x.CanProductionByCheclist).OrderByDescending(x => x.IsProduction).ToList();
                    foreach (var model in models)
                    {
                        var userInfo = qCInfoConfig.UserInfo.FirstOrDefault(x => x.Operation == model.Operation);
                        if (userInfo != null)
                        {
                            model.PDName = userInfo.PDName;
                            model.QCName = userInfo.QCName;
                        }

                        PDResultDailyViewModel viewModel = new PDResultDailyViewModel();
                        viewModel.OperationActive = model.Operation;
                        viewModel.DailyPlanTarget = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "H.Plan")?.Target ?? 0, appConfig.Rounding).ToString();
                        viewModel.DailyPlanCurrent = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "H.Plan")?.Current ?? 0, appConfig.Rounding).ToString();
                        viewModel.DailyPlanAchieve = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "H.Plan")?.Percent ?? 0, appConfig.Rounding).ToString() + "%";
                        viewModel.UPH = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "UPH")?.Percent ?? 0, appConfig.Rounding).ToString() + "%";
                        viewModel.UPPH = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "UPPH")?.Percent ?? 0, appConfig.Rounding).ToString() + "%";
                        viewModel.Labor = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "Labor")?.Percent ?? 0, appConfig.Rounding).ToString() + "%";
                        viewModel.DefectTargetRate = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "Defect")?.Target ?? 0, appConfig.Rounding).ToString() + "%";
                        viewModel.DefectCurrentRate = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "Defect")?.Current ?? 0, appConfig.Rounding).ToString() + "%";
                        viewModel.DefectRate = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "Defect")?.Percent ?? 0, appConfig.Rounding).ToString() + "%";
                        viewModel.CheckListOnSystem = "OK";
                        viewModel.Remark = model.DefectByCategoryViewModels.Where(x => x.value != "0").Count() > 0 ? "Defect reason:" + Environment.NewLine + string.Join(Environment.NewLine, model.DefectByCategoryViewModels.Where(x => x.value != "0").Select(x => $"{x.category}: {x.value}")) : string.Empty;
                        if (model.CanProductionByCheclist)
                        {
                            viewModel.CheckListOnSystem = "OK";
                        }
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
    }
}
