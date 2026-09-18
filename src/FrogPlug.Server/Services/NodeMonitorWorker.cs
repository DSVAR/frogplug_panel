namespace FrogPlug.Server.Services;

/// <summary>
/// Раз в минуту запускает <see cref="NodeMonitor"/> в отдельном scope. Ошибки логируются и не останавливают цикл.
/// </summary>
public sealed class NodeMonitorWorker(IServiceScopeFactory scopeFactory, ILogger<NodeMonitorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var monitor = scope.ServiceProvider.GetRequiredService<NodeMonitor>();
                await monitor.CollectOnce(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Ошибка опроса нод");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
