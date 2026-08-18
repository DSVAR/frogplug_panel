using FrogPlug.Server.Data;
using FrogPlug.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Server.Cli;

public static class SetupLinkCommand
{
    public static bool IsCommand(string[] arguments)
    {
        return arguments.Length == 1 && arguments[0].Equals("setup-link", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<int> ExecuteAsync(AppDbContext db, string? host = null, TextWriter? output = null)
    {
        output ??= Console.Out;
        if (await db.Admins.AnyAsync())
        {
            await output.WriteLineAsync("Ошибка: первая настройка уже завершена");
            return 1;
        }

        var service = new FirstSetupService(db, TimeProvider.System);
        var token = await service.CreateFresh();
        await output.WriteLineAsync(await service.BuildLink(token, host));
        return 0;
    }
}
