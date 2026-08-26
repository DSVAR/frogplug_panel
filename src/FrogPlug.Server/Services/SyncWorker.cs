namespace FrogPlug.Server.Services;

/// <summary>
/// Применяет конфиги на ноды: при старте, после изменений (с паузой 5 с, чтобы склеить серию правок)
/// и раз в 30 минут. Ошибки логируются и не останавливают цикл.
/// </summary>
public sealed class SyncWorker(
    SyncQueue queue,
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    ILogger<SyncWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan FullSyncInterval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Sync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            var requested = await queue.WaitForRequest(FullSyncInterval, time, stoppingToken);
            if (requested)
            {
                await Task.Delay(Debounce, time, stoppingToken);
                queue.Drain();
            }

            await Sync(stoppingToken);
        }
    }

    private async Task Sync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<NodeSyncService>();
            await service.SyncAll(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Ошибка синхронизации нод");
        }
    }
}
