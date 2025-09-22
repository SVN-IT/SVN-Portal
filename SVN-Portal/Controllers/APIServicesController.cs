using Microsoft.AspNetCore.Mvc;
using SVN_Portal.DAL.DataPortal;
using SVN_Portal.DAL.DTO;
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
                var date = DateTime.Now;
                var strDate = date.ToString("yyyyMMdd");
                var data = await GetPDResultDailyDataV0(date);
                if(data != null && data.Count > 0)
                {
                    var targetDataPortal = new SVN_TargetDataPortal(connectionString);
                    var exitData = await targetDataPortal.ReadListTargetByDate(strDate);
                    var deleteResult = 0;
                    var insertResult = 0;
                    if (exitData != null && exitData.Count > 0)
                    {
                        deleteResult = targetDataPortal.Delete(strDate);
                    }

                    if(exitData == null || exitData.Count == 0 || deleteResult > 0)
                    {
                        insertResult = targetDataPortal.InsertBulk(data);
                    }

                    if (insertResult > 0)
                    {
                        processResult.OK = true;
                        processResult.Message = "Cập nhật dữ liệu thành công";
                    }
                    else
                    {
                        processResult.OK = false;
                        processResult.Message = "Cập nhật dữ liệu không thành công";
                    }
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
        public async Task<List<SVN_target>> GetPDResultDailyDataV0(DateTime date)
        {
            List<QtyProdResultByOperViewModel> models = new List<QtyProdResultByOperViewModel>();
            List<SVN_target> viewModels = new List<SVN_target>();
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
                models = await dataPortal.SummaryDatav2(strdate, opers, storedProceduce, tableName, dBConfiguration.CheckListConnectionString, 0);
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

                        SVN_target viewModel = new SVN_target();
                        viewModel.Operation = model.Operation;
                        viewModel.Daily_plan = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "H.Plan")?.Target ?? 0, appConfig.Rounding);
                        viewModel.Total_Qty = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "H.Plan")?.Current ?? 0, appConfig.Rounding);
                        viewModel.UPH = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "UPH")?.Target ?? 0, appConfig.Rounding);
                        viewModel.UPPH = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "UPPH")?.Target ?? 0, appConfig.Rounding);
                        viewModel.Labor = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "Labor")?.Target ?? 0, appConfig.Rounding);
                        viewModel.Date_time = strdate;
                        viewModel.MaxLabor = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "Labor")?.Current ?? 0, appConfig.Rounding);
                        viewModel.Current_UPH = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "UPH")?.Current ?? 0, appConfig.Rounding);
                        viewModel.Current_UPPH = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "UPPH")?.Current ?? 0, appConfig.Rounding);
                        viewModel.Defect = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "Defect")?.Target ?? 0, appConfig.Rounding);
                        viewModel.Total_NG_Qty = Math.Round(model.TargetViewModels.FirstOrDefault(x => x.Item == "Defect")?.Current ?? 0, appConfig.Rounding);
                        viewModel.WC = model.WC;
                        viewModel.Workingtime = Math.Round(model.CurWorkingTime, appConfig.Rounding);

                        viewModels.Add(viewModel);
                    }
                    viewModels = viewModels.Where(x => x.Daily_plan > 0).ToList();
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
