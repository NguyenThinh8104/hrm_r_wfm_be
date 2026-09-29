using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Modules.Dispatch.Interfaces;

namespace Modules.Dispatch.Services;

/// <summary>
/// Background Service tự động kiểm tra và đồng bộ nhân sự điều chuyển:
/// - Chuyển nhân sự sang chi nhánh đích khi lệnh điều động bắt đầu có hiệu lực.
/// - Hoàn trả nhân sự về lại chi nhánh ban đầu ngay khi hết thời hạn điều chuyển.
/// </summary>
public class DispatchExpirationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DispatchExpirationWorker> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(10);

    public DispatchExpirationWorker(IServiceScopeFactory scopeFactory, ILogger<DispatchExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DispatchExpirationWorker background service đã khởi động.");

        // Chạy lần đầu sau 15 giây khi ứng dụng sẵn sàng
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<IDispatchSyncService>();
                await syncService.SyncDispatchesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong vòng lặp DispatchExpirationWorker.");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("DispatchExpirationWorker background service đã dừng.");
    }
}
