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
        private void DoWork(object? state)
        {
            var result = _apiService.CallAPI().Result;
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
