using FrogPlug.Agent;
using FrogPlug.Agent.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace FrogPlug.Agent.Tests;

public sealed class XrayManagerTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "fp-agent-" + Guid.NewGuid().ToString("N"));
    private readonly FakeRunner _runner = new();

    public XrayManagerTests()
    {
        Directory.CreateDirectory(_directory);
    }

    private string ConfigPath => Path.Combine(_directory, "config.json");

    [Fact]
    public async Task Apply_valid_config_tests_replaces_and_restarts()
    {
        await File.WriteAllTextAsync(ConfigPath, "old");
        var manager = CreateManager();

        var (success, _) = await manager.ApplyAsync("new", CancellationToken.None);

        Assert.True(success);
        Assert.Equal("new", await File.ReadAllTextAsync(ConfigPath));
        Assert.Equal("old", await File.ReadAllTextAsync(ConfigPath + ".bak"));
        Assert.Equal(["xray run -test", "systemctl restart xray"], _runner.Commands);
    }

    [Fact]
    public async Task Apply_invalid_config_keeps_file_and_does_not_restart()
    {
        await File.WriteAllTextAsync(ConfigPath, "old");
        _runner.Fail("xray run -test", "invalid outbound");
        var manager = CreateManager();

        var (success, error) = await manager.ApplyAsync("broken", CancellationToken.None);

        Assert.False(success);
        Assert.Contains("invalid outbound", error);
        Assert.Equal("old", await File.ReadAllTextAsync(ConfigPath));
        Assert.False(File.Exists(XrayManager.TemporaryConfigPath(ConfigPath)));
        Assert.DoesNotContain("systemctl restart xray", _runner.Commands);
    }

    [Fact]
    public async Task Apply_restores_backup_when_restart_fails()
    {
        await File.WriteAllTextAsync(ConfigPath, "old");
        _runner.FailOnce("systemctl restart xray", "failed to start");
        var manager = CreateManager();

        var (success, error) = await manager.ApplyAsync("new", CancellationToken.None);

        Assert.False(success);
        Assert.Contains("failed to start", error);
        Assert.Equal("old", await File.ReadAllTextAsync(ConfigPath));
        Assert.Equal(["xray run -test", "systemctl restart xray", "systemctl restart xray"], _runner.Commands);
    }

    [Fact]
    public async Task Stats_with_reset_passes_reset_flag_and_parses_output()
    {
        _runner.Output = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "statsquery.json"));
        var manager = CreateManager();

        var stats = await manager.GetStatsAsync(true, CancellationToken.None);

        Assert.Contains("-reset", _runner.LastArguments);
        Assert.Equal(1000, stats.Users["u1"].Up);
        Assert.Equal(5000, stats.Users["u1"].Down);
        Assert.Equal(77, stats.Users["node2"].Down);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, true);
    }

    private XrayManager CreateManager()
    {
        var options = new AgentOptions
        {
            XrayBinary = "xray",
            XrayConfigPath = ConfigPath
        };
        return new XrayManager(Options.Create(options), _runner, new PhysicalFileSystem());
    }

    private sealed class FakeRunner : ICommandRunner
    {
        private readonly Dictionary<string, (string Error, bool Once)> _failures = new();

        public List<string> Commands { get; } = [];

        public IReadOnlyList<string> LastArguments { get; private set; } = [];

        public string Output { get; set; } = string.Empty;

        public void Fail(string command, string error)
        {
            _failures[command] = (error, false);
        }

        public void FailOnce(string command, string error)
        {
            _failures[command] = (error, true);
        }

        public Task<CommandResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            var key = fileName + " " + string.Join(' ', arguments.Take(2));
            Commands.Add(key);
            LastArguments = arguments;
            if (_failures.TryGetValue(key, out var failure))
            {
                if (failure.Once)
                {
                    _failures.Remove(key);
                }

                return Task.FromResult(new CommandResult(1, string.Empty, failure.Error));
            }

            return Task.FromResult(new CommandResult(0, Output, string.Empty));
        }
    }
}
