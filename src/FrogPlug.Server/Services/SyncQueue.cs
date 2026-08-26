using System.Threading.Channels;

namespace FrogPlug.Server.Services;

/// <summary>
/// Очередь запросов на синхронизацию конфигов нод.
/// Ёмкость 1: повторные запросы, пока предыдущий не обработан, склеиваются в один.
/// </summary>
public sealed class SyncQueue
{
    private readonly Channel<bool> requests = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
        SingleReader = true
    });

    public void RequestAll()
    {
        requests.Writer.TryWrite(true);
    }

    /// <summary>
    /// Ждёт запрос не дольше <paramref name="maxWait"/>. true — пришёл запрос, false — истёк таймаут.
    /// </summary>
    public async Task<bool> WaitForRequest(TimeSpan maxWait, TimeProvider time, CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(maxWait, time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        try
        {
            await requests.Reader.WaitToReadAsync(linked.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        Drain();
        return true;
    }

    /// <summary>
    /// Забирает все накопившиеся запросы (после паузы debounce).
    /// </summary>
    public void Drain()
    {
        while (requests.Reader.TryRead(out _))
        {
        }
    }
}
