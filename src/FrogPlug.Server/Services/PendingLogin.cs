namespace FrogPlug.Server.Services;

public sealed record PendingLogin(int AdminId, bool Remember, DateTime ExpiresAt);
