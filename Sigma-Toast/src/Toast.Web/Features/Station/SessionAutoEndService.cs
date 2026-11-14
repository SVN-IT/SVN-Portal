using Microsoft.Extensions.Options;

namespace Toast.Web.Features.Station;

/// <summary>
/// Chạy nền trong app: mỗi phút tìm phiên Open không hoạt động từ Toast:AutoEndInactiveHours giờ trở lên, kết thúc lần lượt từng phiên
/// và nhập KQSX lên Viindoo (nếu bật Toast:PushProduction), như kết thúc tay. 0 / không khai báo = tắt.
/// Trên IIS: app pool phải chạy liên tục (Start Mode AlwaysRunning, Idle Time-out 0, site Preload Enabled), không thì app bị tắt
/// khi không ai truy cập và tác vụ này dừng theo.
/// </summary>
public sealed class SessionAutoEndService(
    IServiceScopeFactory scopes,
    IOptionsMonitor<StationOptions> options,
    ILogger<SessionAutoEndService> logger) : BackgroundService
{
    static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        try
        {
            // Lần đầu sau 1 phút (để app khởi động xong), sau đó mỗi phút
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await RunOnceAsync();
        }
        catch (OperationCanceledException) { }
    }

    async Task RunOnceAsync()
    {
        double hours = options.CurrentValue.AutoEndInactiveHours;
        if (hours <= 0) return;
        try
        {
            using var scope = scopes.CreateScope();
            var sessions = scope.ServiceProvider.GetRequiredService<SessionService>();
            var ended = await sessions.EndInactiveAsync(TimeSpan.FromHours(hours), DateTime.Now, EndedBy(hours),
                (s, ex) => logger.LogError(ex, "Tự kết thúc phiên #{Id} ({Wo}) lỗi", s.Id, s.WoName));
            foreach (var s in ended)
                logger.LogInformation("Tự kết thúc phiên #{Id} ({Wo}), {EndedBy}. KQSX: {Status} {Message}",
                    s.Id, s.WoName, s.EndedBy, s.PushStatus, s.PushMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Kiểm tra phiên không hoạt động lỗi");
        }
    }

    // VD "Tự động: không hoạt động 4 giờ", "… 1 giờ 30 phút", "… 6 phút" (0.1 giờ)
    public static string EndedBy(double hours) => "Tự động: không hoạt động " + Duration(hours);

    static string Duration(double hours)
    {
        int minutes = Math.Max(1, (int)Math.Round(hours * 60));
        int h = minutes / 60, m = minutes % 60;
        return h == 0 ? $"{m} phút" : m == 0 ? $"{h} giờ" : $"{h} giờ {m} phút";
    }
}
