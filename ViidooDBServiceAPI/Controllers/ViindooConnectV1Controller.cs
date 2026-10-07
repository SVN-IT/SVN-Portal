using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SVNShareLib;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
using SVNShareLib.Request;
using SVNShareLib.Utils;
using ViidooDBServiceAPI.Services;

namespace ViidooDBServiceAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ViindooConnectV1Controller : ControllerBase
    {
        private readonly ViindooProductionService _productionService;
        private readonly SVNDBConfig _svnDBConfig;

        public ViindooConnectV1Controller(ViindooProductionService productionService, SVNDBConfig svnDBConfig)
        {
            _productionService = productionService;
            _svnDBConfig = svnDBConfig;
        }

        /// <summary>
        /// Nhập kết quả sản xuất V1 (Đã tối ưu)
        /// </summary>
        [HttpPost("InputProductionResultToViindooV1")]
        public async Task<BODataProcessResult> InputProductionResultToViindooV1([FromBody] InputProductDataRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.WorkOrderNumber))
            {
                return new BODataProcessResult
                {
                    OK = false,
                    Message = "Dữ liệu đầu vào không hợp lệ."
                };
            }

            return await _productionService.ProcessProductionInputV1Async(request);
        }

        /// <summary>
        /// Đồng bộ dữ liệu kết quả sản xuất sang Viindoo theo khoảng thời gian
        /// </summary>
        [HttpPost("SynchPDResultDataToViindoo")]
        public async Task<BODataProcessResult> SynchPDResultDataToViindoo([FromBody] SynchPDDataRequest dataRequest)
        {
            var logger = new LogService(_svnDBConfig.ConnectionString);
            var processResult = new BODataProcessResult();
            var syncResults = new List<BODataProcessResult>();
            var dataPortal = new SVN_ProductionInputLogDataPortal(_svnDBConfig.ConnectionString);

            try
            {
                DateTime fromDate = dataRequest.FromDate;
                DateTime toDate = dataRequest.ToDate <= DateTime.MinValue
                    ? DateTime.Today.AddHours(23).AddMinutes(59).AddSeconds(59)
                    : dataRequest.ToDate;

                var inputtedData = await dataPortal.GetCountDataFromDateToDateFinishedAsync(fromDate, toDate, dataRequest.Status, dataRequest.CountRows);

                if (inputtedData != null && inputtedData.Count > 0)
                {
                    var validRecords = inputtedData.Where(x => !string.IsNullOrWhiteSpace(x.serial_code)).ToList();

                    foreach (var item in validRecords)
                    {
                        var request = JsonConvert.DeserializeObject<InputProductDataRequest>(item.API_parameters);
                        var inputResult = await _productionService.ProcessProductionInputV1Async(request);

                        item.status = inputResult.OK ? "synch success" : "synch failed";
                        inputResult.Message = $"{item.id} - {item.wo_code} - {inputResult.Message}";
                        inputResult.Content = item;

                        syncResults.Add(inputResult);
                        await dataPortal.UpdateAsync(item);

                        logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, $"{item.id} - {item.wo_code} - {item.status} - {inputResult.Message}");
                    }

                    int successCount = syncResults.Count(x => x.OK);
                    int failedCount = syncResults.Count(x => !x.OK);

                    processResult.OK = failedCount == 0;
                    processResult.Message = $"{DateTime.Now:dd/MM/yyyy HH:mm:ss} | Đồng bộ hoàn tất: {successCount} thành công, {failedCount} lỗi.";
                    processResult.NumOfRow = validRecords.Count;
                    processResult.Content = syncResults;
                }
                else
                {
                    processResult.OK = false;
                    processResult.Message = "Không tìm thấy dữ liệu cần đồng bộ";
                }
            }
            catch (Exception ex)
            {
                processResult.OK = false;
                processResult.Message = ex.Message;
            }

            return processResult;
        }
    }
}