using FrogPlug.Server.Data;
using FrogPlug.Server.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Server.Cli;

public static class AddLocalNodeCommand
{
    public static bool IsCommand(string[] arguments)
    {
        return arguments.Length > 0 && arguments[0].Equals("add-local-node", StringComparison.OrdinalIgnoreCase);
    }

    public static async Task<int> ExecuteAsync(AppDbContext db, string[] arguments, TextWriter? output = null)
    {
        output ??= Console.Out;
        var name = Value(arguments, "--name");
        var domain = Value(arguments, "--domain");
        var address = Value(arguments, "--address");
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(domain) || string.IsNullOrWhiteSpace(address))
        {
            await output.WriteLineAsync("Использование: add-local-node --name ИМЯ --domain ДОМЕН --address IP");
            return 1;
        }

        var service = new NodeService(db, TimeProvider.System, new AuditService(db, new HttpContextAccessor()));
        var node = await service.Create(new NodeInput(name, address, "XX", NodeRole.Entry | NodeRole.Exit, Domain: domain));
        await output.WriteLineAsync(node.AgentToken);
        return 0;
    }

    private static string? Value(string[] arguments, string option)
    {
        var index = Array.IndexOf(arguments, option);
        return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
    }
}
