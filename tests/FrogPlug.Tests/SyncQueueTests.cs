using FrogPlug.Server.Services;

namespace FrogPlug.Tests;

public sealed class SyncQueueTests
{
    [Fact]
    public async Task Several_requests_are_coalesced_into_one()
    {
        var queue = new SyncQueue();
        queue.RequestAll();
        queue.RequestAll();
        queue.RequestAll();

        var first = await queue.WaitForRequest(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None);
        var second = await queue.WaitForRequest(TimeSpan.FromMilliseconds(50), TimeProvider.System, CancellationToken.None);

        Assert.True(first);
        Assert.False(second);
    }

    [Fact]
    public async Task Wait_without_request_times_out()
    {
        var queue = new SyncQueue();

        var requested = await queue.WaitForRequest(TimeSpan.FromMilliseconds(50), TimeProvider.System, CancellationToken.None);

        Assert.False(requested);
    }

    [Fact]
    public async Task Request_after_drain_is_seen()
    {
        var queue = new SyncQueue();
        queue.RequestAll();
        queue.Drain();
        queue.RequestAll();

        var requested = await queue.WaitForRequest(TimeSpan.FromSeconds(5), TimeProvider.System, CancellationToken.None);

        Assert.True(requested);
    }

    [Fact]
    public async Task Stopping_token_cancels_wait()
    {
        var queue = new SyncQueue();
        using var stopping = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => queue.WaitForRequest(TimeSpan.FromMinutes(1), TimeProvider.System, stopping.Token));
    }
}
