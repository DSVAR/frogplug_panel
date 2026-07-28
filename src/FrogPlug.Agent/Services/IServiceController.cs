namespace FrogPlug.Agent.Services;

public interface IServiceController
{
    Task<CommandResult> ReloadAsync(string service, CancellationToken cancellationToken);

    Task<CommandResult> SignalAsync(string service, string signal, CancellationToken cancellationToken);

    Task<CommandResult> RestartAsync(string service, CancellationToken cancellationToken);

    Task<CommandResult> StartAsync(string service, CancellationToken cancellationToken);

    Task<CommandResult> StopAsync(string service, CancellationToken cancellationToken);

    Task<CommandResult> IsActiveAsync(string service, CancellationToken cancellationToken);

    Task<CommandResult> ReloadManagerAsync(CancellationToken cancellationToken);
}
