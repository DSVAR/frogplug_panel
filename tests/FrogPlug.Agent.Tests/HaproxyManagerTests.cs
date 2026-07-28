using FrogPlug.Agent;
using FrogPlug.Agent.Services;
using FrogPlug.Core;
using Xunit;

namespace FrogPlug.Agent.Tests;

public sealed class HaproxyManagerTests
{
    private const string ConfigPath = "/etc/haproxy/haproxy.cfg";

    [Fact]
    public async Task Unchanged_config_does_not_touch_haproxy()
    {
        var request = new HaproxyApplyRequest(false, null);
        var files = new MemoryFiles();
        files.Text[ConfigPath] = HaproxyConfigBuilder.Build(false, null).Replace("\n", "\r\n");
        var runner = new RecordingRunner();
        var services = new RecordingServices();

        var result = await new HaproxyManager(runner, files, services).ApplyAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Empty(runner.Calls);
        Assert.Empty(services.Calls);
    }

    [Fact]
    public async Task Changed_config_is_validated_and_reloaded_gracefully()
    {
        var files = new MemoryFiles();
        files.Text[ConfigPath] = HaproxyConfigBuilder.Build(false, null);
        var runner = new RecordingRunner();
        var services = new RecordingServices();
        var request = new HaproxyApplyRequest(true, "tg.example.com");

        var result = await new HaproxyManager(runner, files, services).ApplyAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(["haproxy -c -f /etc/haproxy/haproxy.cfg.new"], runner.Calls);
        Assert.Equal(["signal haproxy USR2"], services.Calls);
        Assert.Contains("tg.example.com", files.Text[ConfigPath]);
    }

    private sealed class RecordingRunner : ICommandRunner
    {
        public List<string> Calls { get; } = [];

        public Task<CommandResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            Calls.Add($"{fileName} {string.Join(' ', arguments)}");
            return Task.FromResult(new CommandResult(0, string.Empty, string.Empty));
        }
    }

    internal sealed class RecordingServices : IServiceController
    {
        public List<string> Calls { get; } = [];

        public Task<CommandResult> ReloadAsync(string service, CancellationToken cancellationToken) => Record($"reload {service}");

        public Task<CommandResult> SignalAsync(string service, string signal, CancellationToken cancellationToken) =>
            Record($"signal {service} {signal}");

        public Task<CommandResult> RestartAsync(string service, CancellationToken cancellationToken) => Record($"restart {service}");

        public Task<CommandResult> StartAsync(string service, CancellationToken cancellationToken) => Record($"start {service}");

        public Task<CommandResult> StopAsync(string service, CancellationToken cancellationToken) => Record($"stop {service}");

        public Task<CommandResult> IsActiveAsync(string service, CancellationToken cancellationToken) => Record($"active {service}");

        public Task<CommandResult> ReloadManagerAsync(CancellationToken cancellationToken) => Record("reload-manager");

        private Task<CommandResult> Record(string call)
        {
            Calls.Add(call);
            return Task.FromResult(new CommandResult(0, string.Empty, string.Empty));
        }
    }

    internal sealed class MemoryFiles : IFileSystem
    {
        public Dictionary<string, string> Text { get; } = [];

        public bool FileExists(string path) => Text.ContainsKey(path);

        public Task<string> ReadTextAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Text[path]);

        public Task WriteTextAsync(string path, string content, CancellationToken cancellationToken = default)
        {
            Text[path] = content;
            return Task.CompletedTask;
        }

        public void CopyFile(string source, string destination, bool overwrite) => Text[destination] = Text[source];

        public void MoveFile(string source, string destination, bool overwrite)
        {
            Text[destination] = Text[source];
            Text.Remove(source);
        }

        public void DeleteFile(string path) => Text.Remove(path);

        public void CreateDirectory(string path)
        {
        }

        public string GetTempFileName() => "/tmp/test";

        public byte[] ReadAllBytes(string path) => throw new NotSupportedException();

        public void WriteAllBytes(string path, byte[] data) => throw new NotSupportedException();

        public long GetFileLength(string path) => Text[path].Length;
    }
}
