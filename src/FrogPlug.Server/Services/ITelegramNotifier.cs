namespace FrogPlug.Server.Services;

public interface ITelegramNotifier
{
    Task<bool> Send(string message, CancellationToken cancellationToken = default);
}
