using AutomationService.Configurations;
using System.Threading.Tasks;

namespace AutomationService.Services
{
    public class TimedHostedService:IHostedService, IDisposable
    {
        private Timer? _timer;
        APIConfiguration _apiConfiguration;
        APIService _apiService;
        ILogger<TimedHostedService> _logger;
        private static SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public TimedHostedService(APIConfiguration apiConfiguration, APIService apiService, ILogger<TimedHostedService> logger)
        {
            _apiConfiguration = apiConfiguration;
            _apiService = apiService;
            _logger = logger;
        }
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer = new Timer(DoWork, null, TimeSpan.Zero, TimeSpan.FromMinutes(_apiConfiguration.TimeReload));
            return Task.CompletedTask;
        }
        private async void DoWork(object? state)
        {
            await _semaphore.WaitAsync();
            try
            {
                await _apiService.CallAPI();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[Time]: {DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss")} [Status]: {false} [Message]: {ex.Message}");
            }
            finally
            {
                _semaphore.Release();
            }
        }
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer?.Change(Timeout.Infinite, 0);
            return Task.CompletedTask;
        }
        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
