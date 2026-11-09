using FrogPlug.Server.Cli;
using FrogPlug.Server.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Tests;

public sealed class AddLocalNodeCommandTests
{
    [Fact]
    public async Task Creates_entry_exit_node_and_prints_agent_token()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using var db = new AppDbContext(options);
        await db.Database.EnsureCreatedAsync();
        var output = new StringWriter();

        var exitCode = await AddLocalNodeCommand.ExecuteAsync(
            db,
            ["add-local-node", "--name", "local", "--domain", "node.example.com", "--address", "203.0.113.10"],
            output);

        var node = await db.Nodes.SingleAsync();
        Assert.Equal(0, exitCode);
        Assert.Equal(NodeRole.Entry | NodeRole.Exit, node.Role);
        Assert.Equal(FrogPlug.Core.MaskMode.OwnDomain, node.MaskMode);
        Assert.Equal("node.example.com", node.Domain);
        Assert.Equal("203.0.113.10", node.Address);
        Assert.Equal(node.AgentToken, output.ToString().Trim());
    }
}
