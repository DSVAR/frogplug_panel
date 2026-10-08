using FrogPlug.Server.Data;
using FrogPlug.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Server.Cli;

public static class ResetAdminCommand
{
    public static bool IsCommand(string[] arguments)
    {
        return arguments.Length > 0 && arguments[0].Equals("reset-admin", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<int> ExecuteAsync(AppDbContext db, string[] arguments, TextWriter? output = null)
    {
        output ??= Console.Out;
        if (arguments.Length != 3)
        {
            await output.WriteLineAsync("Использование: FrogPlug.Server reset-admin <логин> <новыйПароль>");
            return 1;
        }

        try
        {
            AdminAuthService.ValidatePassword(arguments[2]);
        }
        catch (ArgumentException exception)
        {
            await output.WriteLineAsync($"Ошибка: {exception.Message}");
            return 1;
        }

        var admin = await db.Admins.SingleOrDefaultAsync(item => item.Login == arguments[1]);
        if (admin is null)
        {
            await output.WriteLineAsync("Ошибка: администратор не найден");
            return 1;
        }

        admin.PasswordHash = new PasswordHasher<Admin>().HashPassword(admin, arguments[2]);
        admin.TotpSecret = null;
        admin.LastTotpStep = null;
        admin.FailedAttempts = 0;
        admin.LockedUntil = null;
        db.AuditLogs.Add(new AuditLog
        {
            At = DateTime.UtcNow,
            AdminLogin = "cli",
            Action = AuditActions.AdminReset,
            Details = $"Сброшен пароль и 2FA администратора {admin.Login}"
        });
        await db.SaveChangesAsync();
        await output.WriteLineAsync("Пароль администратора сброшен, двухфакторная аутентификация отключена");
        return 0;
    }
}
