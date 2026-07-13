using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SVNShareLib;
using SVNShareLib.DAL;
using SVNShareLib.DAL.NewDashboard;
using SVNShareLib.DTO;
using System.Threading.Tasks;

namespace ViidooDBServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CheckServiceController : Controller
    {
        SVNDBConfig svnDBConfig;
        public CheckServiceController(SVNDBConfig svnDBConfig)
        {
            this.svnDBConfig = svnDBConfig;
        }

        [Route("ProductionResultCompare")]
        [HttpPost]
        public async Task<BODataProcessResult> ProductionResultCompare()
        {
            BODataProcessResult processResult = new BODataProcessResult();
            try
            {
                DateTime today = DateTime.Today;
                DateTime fromDate = today.AddHours(-7);
                DateTime toDate = today.AddHours(16).AddMinutes(59);

                DateTime nightStartTime = today.AddDays(-1).AddHours(13);
                DateTime nightEndTime = today.AddHours(1);

                DateTime dayStartTime = today.AddHours(1);
                DateTime dayEndTime = today.AddHours(13);

                SVN_production_resultV1DataPortal dataPortal = new SVN_production_resultV1DataPortal(svnDBConfig.ConnectionString);
                mrp_productionDataPortal productionDataPortal = new mrp_productionDataPortal(svnDBConfig.ConnectionString);
                SVN_AppSetting_v1DataPortal appSettingDataPortal = new SVN_AppSetting_v1DataPortal(svnDBConfig.ConnectionString);

                var svnNightData = await dataPortal.ReadListByOperationsRunning(today.AddDays(-1).ToString("yyyyMMdd"));
                var svnDayData = await dataPortal.ReadListByOperationsRunning(today.ToString("yyyyMMdd"));
                var odooNightData = productionDataPortal.GetDataProductFromTimeToTime(nightStartTime, nightEndTime);
                var odooDayData = productionDataPortal.GetDataProductFromTimeToTime(dayStartTime, dayEndTime);

                OperInfoConfigV1 operInfoConfig = new OperInfoConfigV1();
                try
                {
                    var appSetting = await appSettingDataPortal.GetSettingByGroupAndKey("OperInfo");
                    if (appSetting != null && !string.IsNullOrWhiteSpace(appSetting.Value))
                    {
                        operInfoConfig.OperInfo = System.Text.Json.JsonSerializer.Deserialize<List<OperInfoV1>>(appSetting.Value);
                    }
                }
                catch
                {
                    
                }

                if (svnDayData != null && odooNightData != null && odooDayData != null && operInfoConfig.OperInfo != null)
                {
                    processResult.Message = $"{today.ToString("dd/MM/yyyy")} So sánh kết quả sản xuất giữa SVN và Viindoo: ";

                    var svnNightDataPerShift = svnNightData.Where(x => x.Type_value == "Production Qty" && x.Shift.Contains("night")).ToList();

                    var svnDayDataPerShift = svnDayData.Where(x => x.Type_value == "Production Qty" && x.Shift.Contains("day")).ToList();

                    var svnDataPerShift = new List<SVN_production_resultV1UI>();
                    if (svnNightDataPerShift != null)
                    {
                        svnDataPerShift.AddRange(svnNightDataPerShift);
                    }
                    if (svnDayDataPerShift != null)
                    {
                        svnDataPerShift.AddRange(svnDayDataPerShift);
                    }

                    List<ProductionResultCompare> compareResults = new List<ProductionResultCompare>();
                    foreach(var item in svnDataPerShift)
                    {
                        var productIDs = operInfoConfig.OperInfo.FirstOrDefault(x => x.Operation == item.Operation) != null ? operInfoConfig.OperInfo.FirstOrDefault(x => x.Operation == item.Operation).Produce_id : new List<int>();

                        double svnQty = 0;
                        decimal odooSumQtyPerOperation = 0;

                        svnQty = item.Time1 + item.Time2 + item.Time3 + item.Time4 + item.Time5;

                        if (item.Shift.Contains("day"))
                        {
                            
                            odooSumQtyPerOperation = odooDayData.Where(x => productIDs.Contains(x.product_id.Value)).Select(x => x.product_qty).Sum();
                        }
                        else if (item.Shift.Contains("night"))
                        {

                            odooSumQtyPerOperation = odooNightData.Where(x => productIDs.Contains(x.product_id.Value)).Select(x => x.product_qty).Sum();
                        }

                        ProductionResultCompare compareItem = new ProductionResultCompare();
                        compareItem.Operation = item.Operation;
                        compareItem.Shift = item.Shift;
                        compareItem.ProductID = productIDs != null ? JsonConvert.SerializeObject(productIDs): "";
                        compareItem.SVNQty = svnQty;
                        compareItem.ViindooQty = odooSumQtyPerOperation;
                        compareResults.Add(compareItem);

                        processResult.Message = processResult.Message + Environment.NewLine + $"Operation: {item.Operation} - Shift: {item.Shift} - SVN Qty: {compareItem.SVNQty} - Viindoo Qty: {compareItem.ViindooQty}; ";
                    }
                    processResult.OK = true;
                    processResult.Content = compareResults;
                }
                else
                {
                    processResult.OK = false;
                    processResult.Message = "Không có dữ liệu để so sánh";
                }
            }
            catch(Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }
            return processResult;
        }
    }
}
