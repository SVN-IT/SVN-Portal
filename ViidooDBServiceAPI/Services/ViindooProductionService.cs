using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using SVNShareLib;
using SVNShareLib.DAL;
using SVNShareLib.DTO;
using SVNShareLib.Request;
using SVNShareLib.Utils;

namespace ViidooDBServiceAPI.Services
{
    public class ViindooProductionService
    {
        private readonly NewViindooAPIService _odooAPIService;
        private readonly ViindooDBConfig _dbConfig;
        private readonly SVNDBConfig _svnDBConfig;

        public ViindooProductionService(NewViindooAPIService odooAPIService, ViindooDBConfig dbConfig, SVNDBConfig svnDBConfig)
        {
            _odooAPIService = odooAPIService;
            _dbConfig = dbConfig;
            _svnDBConfig = svnDBConfig;
        }

        private async Task EnsureSessionAsync()
        {
            if (string.IsNullOrWhiteSpace(_dbConfig.SessionID) || _dbConfig.UserID == 0)
            {
                var loginRes = await _odooAPIService.LoginAsync();
                if (loginRes.OK)
                {
                    _dbConfig.SessionID = loginRes.DataType;
                    _dbConfig.UserID = loginRes.UserID;
                }
            }
        }

        public async Task<BODataProcessResult> ProcessProductionInputV1Async(InputProductDataRequest dataRequest)
        {
            var logger = new LogService(_svnDBConfig.ConnectionString);
            var result = new BODataProcessResult();

            try
            {
                await EnsureSessionAsync();

                // 1. Lấy thông tin Lệnh sản xuất (MO)
                dynamic mo = await _odooAPIService.ReadProductionByProductIDAsync(dataRequest.WorkOrderNumber, _dbConfig.UserID, _dbConfig.SessionID);
                if (mo == null)
                {
                    string errorMsg = $"Không tìm thấy lệnh sản xuất cho mã seri: {dataRequest.WorkOrderNumber}";
                    logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, errorMsg);
                    result.OK = false;
                    result.Message = errorMsg;
                    return result;
                }

                int moId = (int)mo.id;
                string moName = mo.name.ToString();
                int remainingQty = (int)mo.product_qty;
                if (dataRequest.Quality >= remainingQty)
                {
                    dataRequest.IsLastOrder = true;
                }

                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, $"Bắt đầu thực hiện lệnh sản xuất: {moName}");

                // Parse danh sách raw move IDs trực tiếp qua JArray thay vì String Split
                List<int> moveIds = mo.move_raw_ids != null ? ((JArray)mo.move_raw_ids).Select(x => (int)x).ToList() : new List<int>();
                string productTracking = mo.product_tracking?.ToString();
                int productId = (int)mo.product_id[0];
                int companyId = (int)mo.company_id[0];

                // 2. Lấy thông tin Stock Move
                dynamic stockMoves = await _odooAPIService.GetStockMoveByIDAsync(moveIds, companyId, _dbConfig.UserID, _dbConfig.SessionID);

                // Lọc các thành phần có tracking là 'serial' hoặc 'lot'
                var stockMoveTracked = new List<dynamic>();
                if (stockMoves is JArray moveArray)
                {
                    foreach (dynamic item in moveArray)
                    {
                        string tracking = item.has_tracking?.ToString();
                        if (tracking == "serial" || tracking == "lot")
                        {
                            stockMoveTracked.Add(item);
                        }
                    }
                }

                // 3. Xử lý các NVL/Thành phần theo Mã Lot / Serial đã quét
                if (stockMoveTracked.Count > 0 && dataRequest.LotScaneds != null && dataRequest.LotScaneds.Count > 0)
                {
                    foreach (dynamic moveItem in stockMoveTracked)
                    {
                        int materialProductId = (int)moveItem.product_id[0];
                        var lotScaned = dataRequest.LotScaneds.FirstOrDefault(x => x.product_id == materialProductId);

                        if (lotScaned != null)
                        {
                            logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, $"Xử lý thành phần có mã serial: {lotScaned.lotNumber} cho lsx: {moName}");

                            int lotIdInfo = await _odooAPIService.GetLotInfoAsync(lotScaned.lotNumber, moId, moveItem, _dbConfig.UserID, _dbConfig.SessionID);
                            if (lotIdInfo == 0)
                            {
                                string msg = $"Mã lot {lotScaned.lotNumber} không tìm thấy ";
                                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, msg + $" cho lsx: {moName}");
                                result.OK = false;
                                result.Message = msg;
                                return result;
                            }

                            JArray moveLineIds = moveItem.move_line_ids as JArray;
                            bool isCreated = false;

                            if (moveLineIds == null || moveLineIds.Count == 0)
                            {
                                dynamic createRes = await _odooAPIService.CreateLotComponentForMOAsync(lotIdInfo, moId, moveItem, _dbConfig.UserID, _dbConfig.SessionID, lotScaned.quantity);
                                if (createRes == null || createRes.result == null)
                                {
                                    result.OK = false;
                                    result.Message = $"Lỗi không tạo được stock move line cho mã lot {lotScaned.lotNumber}";
                                    return result;
                                }
                                isCreated = true;
                            }

                            if (!isCreated && moveLineIds != null && moveLineIds.Count > 0)
                            {
                                dynamic firstMoveLine = await _odooAPIService.GetStockMoveLineByIDAsync((int)moveLineIds[0], moId, moveItem, _dbConfig.UserID, _dbConfig.SessionID);
                                if (firstMoveLine == null)
                                {
                                    result.OK = false;
                                    result.Message = $"Mã lot {lotScaned.lotNumber} không tìm thấy ";
                                    return result;
                                }

                                // Cập nhật move line
                                object[] updateMoveLines = moveLineIds.Select(idToken =>
                                {
                                    int lineId = (int)idToken;
                                    if (lineId == (int)firstMoveLine.id)
                                    {
                                        return new object[] { 1, lineId, new { lot_id = lotIdInfo, qty_done = 1 } };
                                    }
                                    return new object[] { 4, lineId, false };
                                }).ToArray();

                                dynamic writeRes = await _odooAPIService.SaveSerialStockMoveAsync(moveItem, updateMoveLines, _dbConfig.UserID, _dbConfig.SessionID);
                                if (writeRes == null || writeRes.result?.ToString() != "True")
                                {
                                    result.OK = false;
                                    result.Message = $"Mã lot id {lotIdInfo} không tiêu hao thành công cho product id: {materialProductId}";
                                    return result;
                                }
                            }
                        }
                    }
                }

                // 4. Xử lý Mã Lot / Serial Thành phẩm (FG)
                int lotId = 0;
                if (!string.IsNullOrWhiteSpace(productTracking) && (productTracking == "serial" || productTracking == "lot"))
                {
                    dynamic stockLotInfo = await _odooAPIService.LotSearchAsync(dataRequest.LotNumber, productId, companyId, _dbConfig.UserID, _dbConfig.SessionID);
                    if (stockLotInfo == null || ((JArray)stockLotInfo).Count == 0)
                    {
                        await _odooAPIService.CreateLotAsync(dataRequest.LotNumber, productId, companyId, _dbConfig.UserID, _dbConfig.SessionID);
                        stockLotInfo = await _odooAPIService.LotSearchAsync(dataRequest.LotNumber, productId, companyId, _dbConfig.UserID, _dbConfig.SessionID);
                    }

                    if (stockLotInfo is JArray lotArray && lotArray.Count > 0)
                    {
                        lotId = (int)lotArray.Last["id"];
                    }
                    else
                    {
                        result.OK = false;
                        result.Message = $"Không tìm thấy hoặc tạo được mã lô: {dataRequest.LotNumber}";
                        return result;
                    }
                }

                // Kiểm tra Lot đã từng sử dụng chưa
                if (lotId > 0)
                {
                    dynamic checkLot = await _odooAPIService.CheckUsedLotIDAsync(lotId, _dbConfig.UserID, _dbConfig.SessionID);
                    if (checkLot != null)
                    {
                        string msg = $"Mã lô {dataRequest.LotNumber} đã được sử dụng cho lệnh sản xuất {checkLot.name}";
                        logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, msg);
                        result.OK = false;
                        result.Message = msg;
                        return result;
                    }
                }

                // 5. Tạo danh sách cập nhật Move Raw / Work Orders
                var moveRawList = new List<object>();
                if (mo.move_raw_ids is JArray rawIds)
                {
                    foreach (var idToken in rawIds)
                    {
                        int id = (int)idToken;
                        moveRawList.Add(new object[] { 1, id, new { quantity_done = dataRequest.Quality } });
                    }
                }

                // 6. Lưu và Hoàn tất Lệnh sản xuất
                dynamic saveResult = await _odooAPIService.SaveProductionOrderAsyncv1(moId, lotId, dataRequest.Quality, moveRawList.ToArray(), null, _dbConfig.UserID, _dbConfig.SessionID);
                if (saveResult == null || saveResult.result?.ToString() != "True")
                {
                    result.OK = false;
                    result.Message = "Lỗi không lưu được lệnh sản xuất ";
                    return result;
                }

                await _odooAPIService.MarkDoneProductionOrderAsync(moId, _dbConfig.UserID, _dbConfig.SessionID);
                await _odooAPIService.BackOrderOnchange(moId, _dbConfig.UserID, _dbConfig.SessionID);

                if (!dataRequest.IsLastOrder)
                {
                    int backOrderId = await _odooAPIService.BackOrderCreate(moId, _dbConfig.UserID, _dbConfig.SessionID, lotId);
                    await _odooAPIService.BackOrderAction(moId, backOrderId, _dbConfig.UserID, _dbConfig.SessionID);
                }

                result.OK = true;
                result.Message = "Hoàn thành lệnh sản xuất";
                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, $"Hoàn thành lệnh sản xuất {moName}");
            }
            catch (Exception ex)
            {
                result.OK = false;
                result.Message = ex.Message;
            }

            return result;
        }
    }
}