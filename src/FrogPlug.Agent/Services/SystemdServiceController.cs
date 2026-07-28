namespace FrogPlug.Agent.Services;

public sealed class SystemdServiceController(ICommandRunner runner) : IServiceController
{
    public Task<CommandResult> ReloadAsync(string service, CancellationToken cancellationToken)
    {
        return RunAsync(["reload", service], cancellationToken);
    }

    public Task<CommandResult> RestartAsync(string service, CancellationToken cancellationToken)
    {
        return RunAsync(["restart", service], cancellationToken);
    }

    public Task<CommandResult> SignalAsync(string service, string signal, CancellationToken cancellationToken)
    {
        return ReloadAsync(service, cancellationToken);
    }

    public Task<CommandResult> StartAsync(string service, CancellationToken cancellationToken)
    {
        return RunAsync(["enable", "--now", service], cancellationToken);
    }

    public Task<CommandResult> StopAsync(string service, CancellationToken cancellationToken)
    {
        return StopAndDisableAsync(service, cancellationToken);
    }

    public Task<CommandResult> IsActiveAsync(string service, CancellationToken cancellationToken)
    {
        return RunAsync(["is-active", service], cancellationToken);
    }

    public Task<CommandResult> ReloadManagerAsync(CancellationToken cancellationToken)
    {
        return RunAsync(["daemon-reload"], cancellationToken);
    }

    private Task<CommandResult> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        return runner.RunAsync("systemctl", arguments, TimeSpan.FromSeconds(30), cancellationToken);
    }

    private async Task<CommandResult> StopAndDisableAsync(
        string service,
        CancellationToken cancellationToken)
    {
        var stopped = await RunAsync(["stop", service], cancellationToken);
        if (stopped.ExitCode != 0)
        {
            return stopped;
        }

        return await RunAsync(["disable", service], cancellationToken);
    }
}
