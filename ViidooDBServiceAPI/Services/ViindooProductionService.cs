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
using static Org.BouncyCastle.Math.EC.ECCurve;

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
                if (!loginRes.OK)
                {
                    throw new Exception($"Đăng nhập Viindoo thất bại: {loginRes.Message}");
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

                // 1. Đọc lệnh sản xuất (MO)
                dynamic mo = await _odooAPIService.ReadProductionByProductIDAsync(dataRequest.WorkOrderNumber);
                if (mo == null)
                {
                    string errorMsg = $"Không tìm thấy lệnh sản xuất cho mã: {dataRequest.WorkOrderNumber}";
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

                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, $"Bắt đầu thực hiện Lệnh sản xuất: {moName}");

                List<int> moveIds = mo.move_raw_ids != null ? ((JArray)mo.move_raw_ids).Select(x => (int)x).ToList() : new List<int>();
                string productTracking = mo.product_tracking?.ToString();
                int productId = (int)mo.product_id[0];
                int companyId = (int)mo.company_id[0];

                // 2. Lấy thông tin Stock Move của linh kiện/NVL
                dynamic stockMoves = await _odooAPIService.GetStockMoveByIDAsync(moveIds, companyId);
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

                // 3. XỬ LÝ CONSUME LINH KIỆN / NVL DÙNG TRONG SẢN XUẤT
                if (stockMoveTracked.Count > 0 && dataRequest.LotScaneds != null && dataRequest.LotScaneds.Count > 0)
                {
                    foreach (dynamic moveItem in stockMoveTracked)
                    {
                        int materialProductId = (int)moveItem.product_id[0];
                        var lotScaned = dataRequest.LotScaneds.FirstOrDefault(x => x.product_id == materialProductId);

                        if (lotScaned != null)
                        {
                            logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, $"Xử lý Consume linh kiện Serial/Lot: {lotScaned.lotNumber} cho LSX: {moName}");

                            int lotIdInfo = await _odooAPIService.GetLotInfoAsync(lotScaned.lotNumber, materialProductId);
                            if (lotIdInfo == 0)
                            {
                                string msg = $"Mã lot linh kiện {lotScaned.lotNumber} không tồn tại trên hệ thống.";
                                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, msg);
                                result.OK = false;
                                result.Message = msg;
                                return result;
                            }

                            JArray moveLineIds = moveItem.move_line_ids as JArray;

                            // Trường hợp 1: Chưa có move line -> Gọi ConsumeComponentAsync tạo mới
                            if (moveLineIds == null || moveLineIds.Count == 0)
                            {
                                dynamic consumeRes = await _odooAPIService.ConsumeComponentAsync(moveItem, lotIdInfo, lotScaned.quantity, moId);
                                if (consumeRes == null || consumeRes.result == null)
                                {
                                    result.OK = false;
                                    result.Message = $"Lỗi tiêu hao linh kiện (Consume) mã lot {lotScaned.lotNumber}";
                                    return result;
                                }
                            }
                            // Trường hợp 2: Đã có move line -> Cập nhật thông tin tiêu hao
                            else
                            {
                                dynamic firstMoveLine = await _odooAPIService.GetStockMoveLineByIDAsync((int)moveLineIds[0]);
                                if (firstMoveLine == null)
                                {
                                    result.OK = false;
                                    result.Message = $"Mã move line của lot {lotScaned.lotNumber} không khả dụng.";
                                    return result;
                                }

                                object[] updateMoveLines = moveLineIds.Select(idToken =>
                                {
                                    int lineId = (int)idToken;
                                    if (lineId == (int)firstMoveLine.id)
                                    {
                                        return new object[] { 1, lineId, new { lot_id = lotIdInfo, qty_done = lotScaned.quantity } };
                                    }
                                    return new object[] { 4, lineId, false };
                                }).ToArray();

                                dynamic writeRes = await _odooAPIService.SaveSerialStockMoveAsync(moveItem, updateMoveLines);
                                if (writeRes == null || writeRes.result?.ToString() != "True")
                                {
                                    result.OK = false;
                                    result.Message = $"Cập nhật tiêu hao thất bại cho mã lot {lotScaned.lotNumber}";
                                    return result;
                                }
                            }
                        }
                    }
                }

                // 4. Xử lý Mã Lot / Serial của Thành phẩm
                int lotId = 0;
                if (!string.IsNullOrWhiteSpace(productTracking) && (productTracking == "serial" || productTracking == "lot"))
                {
                    dynamic stockLotInfo = await _odooAPIService.LotSearchAsync(dataRequest.LotNumber, productId, companyId);
                    if (stockLotInfo == null || ((JArray)stockLotInfo).Count == 0)
                    {
                        await _odooAPIService.CreateLotAsync(dataRequest.LotNumber, productId, companyId);
                        stockLotInfo = await _odooAPIService.LotSearchAsync(dataRequest.LotNumber, productId, companyId);
                    }

                    if (stockLotInfo is JArray lotArray && lotArray.Count > 0)
                    {
                        lotId = (int)lotArray.Last["id"];
                    }
                    else
                    {
                        result.OK = false;
                        result.Message = $"Không tạo/tìm thấy mã lô thành phẩm: {dataRequest.LotNumber}";
                        return result;
                    }
                }

                // Kiểm tra mã Lot đã gán cho LSX khác chưa
                if (lotId > 0)
                {
                    dynamic checkLot = await _odooAPIService.CheckUsedLotIDAsync(lotId);
                    if (checkLot != null)
                    {
                        string msg = $"Mã lô thành phẩm {dataRequest.LotNumber} đã dùng cho LSX {checkLot.name}";
                        logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, msg);
                        result.OK = false;
                        result.Message = msg;
                        return result;
                    }
                }

                // 5. Chuẩn bị Move Raw IDs
                var moveRawList = new List<object>();
                if (mo.move_raw_ids is JArray rawIds)
                {
                    foreach (var idToken in rawIds)
                    {
                        int id = (int)idToken;
                        moveRawList.Add(new object[] { 1, id, new { quantity_done = dataRequest.Quality } });
                    }
                }

                // 6. Lưu thông tin sản xuất & Xử lý BackOrder theo cách mới
                dynamic saveResult = await _odooAPIService.SaveProductionOrderAsyncv1(moId, lotId, dataRequest.Quality, moveRawList.ToArray(), null);
                if (saveResult == null || saveResult.result?.ToString() != "True")
                {
                    result.OK = false;
                    result.Message = "Lỗi lưu dữ liệu Lệnh sản xuất.";
                    return result;
                }

                var markDoneResult = await _odooAPIService.MarkDoneProductionOrderAsync(moId);
                // 2. Kiểm tra res_model nếu bị dính popup warning
                if (markDoneResult?.result?.res_model == "mrp.consumption.warning")
                {
                    // Truyền thẳng markDoneResult (dynamic) vào hàm
                    int warningId = await _odooAPIService.CreateConsumptionWarningAsync(markDoneResult);

                    // Tiếp tục bước gọi button confirm trên warningId nếu có...
                    dynamic confirmWarningResult = await _odooAPIService.ConfirmConsumptionWarningAsync(warningId, markDoneResult);

                    if (confirmWarningResult?.result?.res_model == "mrp.production.backorder" && !dataRequest.IsLastOrder)
                    {
                        // 3.1. Tạo bản ghi backorder
                        int backorderId = await _odooAPIService.CreateProductionBackorderAsync(confirmWarningResult);

                        // 3.2. Xác nhận backorder (action_backorder để tạo dở dang HOẶC action_close để đóng luôn)
                        await _odooAPIService.ConfirmProductionBackorderAsync(backorderId, confirmWarningResult);
                    }
                }
                else if (markDoneResult?.result?.res_model == "mrp.production.backorder" && !dataRequest.IsLastOrder)
                {
                    // 3.1. Tạo bản ghi backorder
                    int backorderId = await _odooAPIService.CreateProductionBackorderAsync(markDoneResult);

                    // 3.2. Xác nhận backorder (action_backorder để tạo dở dang HOẶC action_close để đóng luôn)
                    await _odooAPIService.ConfirmProductionBackorderAsync(backorderId, markDoneResult);
                }

                result.OK = true;
                result.Message = "Hoàn thành ghi nhận kết quả sản xuất";
                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Info, $"Hoàn thành thành công cho LSX {moName}");
            }
            catch (Exception ex)
            {
                result.OK = false;
                result.Message = ex.Message;
                logger.Log(LogService.LogApp.SVNAPI, LogService.LogAction.InputProduction, LogService.LogType.Error, $"Lỗi xử lý: {ex.Message}");
            }

            return result;
        }
    }
}