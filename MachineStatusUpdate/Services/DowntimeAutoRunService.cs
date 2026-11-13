using MachineStatusUpdate.Models;
using Microsoft.EntityFrameworkCore;

namespace MachineStatusUpdate.Services
{
    public class DowntimeAutoRunService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DowntimeAutoRunService> _logger;

        public DowntimeAutoRunService(IServiceScopeFactory scopeFactory,
                                      ILogger<DowntimeAutoRunService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await ProcessAutoRuns(); }
                catch (Exception ex) { _logger.LogError(ex, "DowntimeAutoRunService error"); }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }

        private async Task ProcessAutoRuns()
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.Now;

            // Lấy Stop có AutoRunEnabled = true và chưa được auto-run
            var pendingStops = await context.SVN_Downtime_Infos
                .Where(x => x.State == "Stop"
                         && x.AutoRunEnabled == true
                         && x.IsAutoRunExecuted == false
                         && x.Datetime.HasValue
                         && !string.IsNullOrEmpty(x.EstimateTime))
                .OrderBy(x => x.Datetime)
                .ToListAsync();

            foreach (var stop in pendingStops)
            {
                // Parse EstimateTime (HH:mm)
                if (!TimeSpan.TryParse(stop.EstimateTime, out var estimateSpan))
                {
                    _logger.LogWarning("Không parse được EstimateTime={ET} cho Id={Id}", stop.EstimateTime, stop.Id);
                    continue;
                }

                // Thời điểm dự kiến Run = ngày của Stop + giờ EstimateTime
                var runAt = stop.Datetime!.Value.Date
                    .AddHours(estimateSpan.Hours)
                    .AddMinutes(estimateSpan.Minutes);

                // Nếu runAt trước hoặc bằng thời điểm Stop (ví dụ EstimateTime là 00:00 hôm sau),
                // dịch sang ngày tiếp theo
                if (runAt <= stop.Datetime!.Value)
                    runAt = runAt.AddDays(1);

                // Chưa đến giờ → bỏ qua
                if (now < runAt)
                    continue;

                // ✅ Insert Run tự động
                var runRecord = new SVN_Downtime_Info
                {
                    Code           = stop.Code,
                    Name           = stop.Name,
                    SVNCode        = stop.SVNCode,
                    Operation      = stop.Operation,
                    ISS_Code       = stop.ISS_Code,
                    State          = "Run",
                    Datetime       = runAt,
                    EstimateTime   = string.Empty,
                    AutoRunEnabled = false,
                    Description    = string.IsNullOrWhiteSpace(stop.AutoRunDescription) ? null : stop.AutoRunDescription,
                    Image          = string.Empty
                };

                context.SVN_Downtime_Infos.Add(runRecord);

                // Đánh dấu đã xử lý để không tạo thêm Run record nữa
                stop.IsAutoRunExecuted = true;

                await context.SaveChangesAsync();

                _logger.LogInformation(
                    "Auto-Run inserted: Op={Op} SVN={Code} at {At}",
                    stop.Operation, stop.SVNCode, runAt);
            }

            await ProcessFallbackAutoRuns(context, now);
        }

        /* Với Stop KHÔNG có EstimateTime thủ công (người dùng quên nhập), tự tính mốc "hết giờ SX
           theo kế hoạch" từ bảng SVN_Production_result_Viindoo (Target day + night) và tự Run khi
           tới mốc đó mà vẫn chưa có Run thật, tránh Stop bị treo overdue vô thời hạn. */
        // Chỉ xử lý Stop trong N ngày gần đây — tránh backfill ồ ạt các Stop cũ đã treo từ lâu.
        private const int FallbackLookbackDays = 3;

        private async Task ProcessFallbackAutoRuns(ApplicationDbContext context, DateTime now)
        {
            var earliestDate = now.Date.AddDays(-FallbackLookbackDays);

            var pendingStops = await context.SVN_Downtime_Infos
                .Where(x => x.State == "Stop"
                         && x.IsAutoRunExecuted == false
                         && x.Datetime.HasValue
                         && x.Datetime.Value >= earliestDate
                         && string.IsNullOrEmpty(x.EstimateTime)
                         && x.Operation != null)
                .OrderBy(x => x.Datetime)
                .ToListAsync();

            if (pendingStops.Count == 0) return;

            // Nhóm theo "ngày sản xuất" (không phải ngày dương lịch) — Stop xảy ra trước 8h sáng
            // vẫn thuộc ngày sản xuất hôm trước (đang ở đuôi ca đêm hôm trước).
            var opsByProdDay = pendingStops
                .GroupBy(s => ProductionScheduleHelper.GetProductionDay(s.Datetime!.Value))
                .ToDictionary(g => g.Key, g => g.Select(s => s.Operation!).Distinct().ToList());

            var targetRows = new List<SVN_Production_result_Viindoo>();
            foreach (var (prodDay, ops) in opsByProdDay)
            {
                var dateStr = prodDay.ToString("yyyyMMdd");
                var rows = await context.SVN_Production_result_Viindoo
                    .Where(r => r.Type_value == "Target" && r.Date_time == dateStr && r.Operation != null && ops.Contains(r.Operation))
                    .ToListAsync();
                targetRows.AddRange(rows);
            }

            var targetRowsByDateAndOperation = targetRows
                .Where(r => r.Operation != null)
                .GroupBy(r => (r.Date_time ?? "", r.Operation!))
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var stop in pendingStops)
            {
                var prodDay = ProductionScheduleHelper.GetProductionDay(stop.Datetime!.Value);
                var dateStr = prodDay.ToString("yyyyMMdd");
                if (!targetRowsByDateAndOperation.TryGetValue((dateStr, stop.Operation!), out var rows))
                    continue;

                var runAt = ProductionScheduleHelper.CalculateEndOfScheduledProduction(prodDay, rows);
                if (!runAt.HasValue || now < runAt.Value)
                    continue;

                var runRecord = new SVN_Downtime_Info
                {
                    Code           = stop.Code,
                    Name           = stop.Name,
                    SVNCode        = stop.SVNCode,
                    Operation      = stop.Operation,
                    ISS_Code       = stop.ISS_Code,
                    State          = "Run",
                    Datetime       = runAt.Value,
                    EstimateTime   = string.Empty,
                    AutoRunEnabled = false,
                    Description    = "Tự động Run theo kế hoạch sản xuất (hết Target trong ngày)",
                    Image          = string.Empty
                };

                context.SVN_Downtime_Infos.Add(runRecord);
                stop.IsAutoRunExecuted = true;

                await context.SaveChangesAsync();

                _logger.LogInformation(
                    "Fallback Auto-Run inserted (no EstimateTime, theo KH SX): Op={Op} SVN={Code} at {At}",
                    stop.Operation, stop.SVNCode, runAt.Value);
            }
        }
    }
}