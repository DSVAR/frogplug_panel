namespace FrogPlug.Agent.Services;

public sealed class SupervisorServiceController(ICommandRunner runner) : IServiceController
{
    public Task<CommandResult> ReloadAsync(string service, CancellationToken cancellationToken)
    {
        return RunAsync(["restart", service], cancellationToken);
    }

    public Task<CommandResult> RestartAsync(string service, CancellationToken cancellationToken)
    {
        return RunAsync(["restart", service], cancellationToken);
    }

    public Task<CommandResult> SignalAsync(string service, string signal, CancellationToken cancellationToken)
    {
        return RunAsync(["signal", signal, service], cancellationToken);
    }

    public Task<CommandResult> StartAsync(string service, CancellationToken cancellationToken)
    {
        return StartAfterUpdateAsync(service, cancellationToken);
    }

    public Task<CommandResult> StopAsync(string service, CancellationToken cancellationToken)
    {
        return RunAsync(["stop", service], cancellationToken);
    }

    public Task<CommandResult> IsActiveAsync(string service, CancellationToken cancellationToken)
    {
        return RunAsync(["status", service], cancellationToken);
    }

    public Task<CommandResult> ReloadManagerAsync(CancellationToken cancellationToken)
    {
        return RunAsync(["reread"], cancellationToken);
    }

    private Task<CommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        return runner.RunAsync("supervisorctl", arguments, TimeSpan.FromSeconds(30), cancellationToken);
    }

    private async Task<CommandResult> StartAfterUpdateAsync(string service, CancellationToken cancellationToken)
    {
        await RunAsync(["reread"], cancellationToken);
        await RunAsync(["update"], cancellationToken);
        return await RunAsync(["start", service], cancellationToken);
    }
}
