namespace FrogPlug.Server.Components.Shared;

public sealed class ToastService
{
    public event Action? Changed;
    public string? Message { get; private set; }
    public bool IsError { get; private set; }
    public void Success(string message) => Show(message, false);
    public void Error(string message) => Show(message, true);
    private void Show(string message, bool error) { Message = message; IsError = error; Changed?.Invoke(); }
}
