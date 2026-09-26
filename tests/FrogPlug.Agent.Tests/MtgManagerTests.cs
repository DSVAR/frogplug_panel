using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using FrogPlug.Agent;
using FrogPlug.Agent.Services;
using FrogPlug.Core;
using Microsoft.Extensions.Options;
using Xunit;

namespace FrogPlug.Agent.Tests;

public sealed class MtgManagerTests
{
    [Fact]
    public async Task Empty_sha_is_refused()
    {
        var test = Create(Archive());
        var result = await test.Manager.ApplyAsync(Request(sha: string.Empty), default);

        Assert.False(result.Success);
        Assert.Empty(test.Runner.Calls);
    }

    [Fact]
    public async Task Mismatched_sha_is_refused()
    {
        var test = Create(Archive());
        var result = await test.Manager.ApplyAsync(Request(sha: new string('0', 64)), default);

        Assert.False(result.Success);
        Assert.DoesNotContain("/etc/mtg.toml", test.Files.Text.Keys);
    }

    [Fact]
    public async Task Config_is_written_after_verified_download()
    {
        var archive = Archive();
        var test = Create(archive);
        var result = await test.Manager.ApplyAsync(Request(sha: Hash(archive)), default);

        Assert.True(result.Success);
        Assert.Equal(
            MtgConfigBuilder.Build(Request().Secret!, 2443, 443),
            test.Files.Text["/etc/mtg.toml"]);
        Assert.Contains("systemctl", test.Runner.Calls.Select(call => call.FileName));
    }

    [Fact]
    public async Task Disable_stops_and_disables_service()
    {
        var test = Create(Archive());
        var result = await test.Manager.ApplyAsync(Request(enabled: false), default);

        Assert.True(result.Success);
        Assert.Contains(
            test.Runner.Calls,
            call => call.Arguments.SequenceEqual(["stop", "mtg"]));
        Assert.Contains(
            test.Runner.Calls,
            call => call.Arguments.SequenceEqual(["disable", "mtg"]));
    }

    [Fact]
    public async Task Docker_mode_uses_bundled_binary_without_download()
    {
        var files = new TestFileSystem();
        var runner = new TestRunner();
        var http = new HttpClient(new BytesHandler(Archive()));
        var manager = new MtgManager(
            files,
            runner,
            http,
            new SupervisorServiceController(runner),
            Options.Create(new AgentOptions { ServiceMode = "docker" }));

        var result = await manager.ApplyAsync(Request(sha: string.Empty), default);

        Assert.True(result.Success);
        Assert.Equal(MtgConfigBuilder.Build(Request().Secret!, 2443, 443), files.Text["/etc/mtg.toml"]);
        Assert.DoesNotContain(runner.Calls, call => call.FileName == "chmod" && call.Arguments.Contains("755"));
        Assert.Contains(runner.Calls, call => call.Arguments.SequenceEqual(["start", "mtg"]));
    }

    [Fact]
    public async Task Docker_mode_restarts_when_config_already_exists()
    {
        var files = new TestFileSystem();
        files.Text["/etc/mtg.toml"] = "old";
        var runner = new TestRunner();
        var manager = new MtgManager(
            files,
            runner,
            new HttpClient(new BytesHandler(Archive())),
            new SupervisorServiceController(runner),
            Options.Create(new AgentOptions { ServiceMode = "docker" }));

        var result = await manager.ApplyAsync(Request(sha: string.Empty), default);

        Assert.True(result.Success);
        Assert.Contains(runner.Calls, call => call.Arguments.SequenceEqual(["restart", "mtg"]));
    }

    private static TestContext Create(byte[] archive)
    {
        var files = new TestFileSystem();
        var runner = new TestRunner();
        var http = new HttpClient(new BytesHandler(archive));
        return new TestContext(new MtgManager(files, runner, http), files, runner);
    }

    private static MtprotoApplyRequest Request(bool enabled = true, string? sha = null)
    {
        return new MtprotoApplyRequest(
            enabled,
            "ee000000000000000000000000000000006e6f64652e6578616d706c652e636f6d",
            2443,
            443,
            "v2.3.0",
            sha ?? new string('a', 64));
    }

    private static byte[] Archive()
    {
        using var result = new MemoryStream();
        using (var gzip = new GZipStream(result, CompressionLevel.SmallestSize, leaveOpen: true))
        using (var writer = new TarWriter(gzip, leaveOpen: true))
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, "mtg")
            {
                DataStream = new MemoryStream([1, 2, 3])
            };
            writer.WriteEntry(entry);
        }

        return result.ToArray();
    }

    private static string Hash(byte[] value)
    {
        return Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    }

    private sealed record TestContext(MtgManager Manager, TestFileSystem Files, TestRunner Runner);

    private sealed class BytesHandler(byte[] content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            });
        }
    }

    private sealed class TestRunner : ICommandRunner
    {
        public List<(string FileName, IReadOnlyList<string> Arguments)> Calls { get; } = [];

        public Task<CommandResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((fileName, arguments));
            return Task.FromResult(new CommandResult(0, "inactive", string.Empty));
        }
    }

    private sealed class TestFileSystem : IFileSystem
    {
        public Dictionary<string, string> Text { get; } = [];
        public Dictionary<string, byte[]> Bytes { get; } = [];

        public bool FileExists(string path) => Bytes.ContainsKey(path) || Text.ContainsKey(path);
        public Task<string> ReadTextAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(Text[path]);
        public Task WriteTextAsync(string path, string content, CancellationToken cancellationToken = default)
        {
            Text[path] = content;
            return Task.CompletedTask;
        }

        public void CopyFile(string source, string destination, bool overwrite) { }
        public void MoveFile(string source, string destination, bool overwrite) { }
        public void DeleteFile(string path) { }
        public void CreateDirectory(string path) { }
        public string GetTempFileName() => "/tmp/mtg";
        public byte[] ReadAllBytes(string path) => Bytes[path];
        public void WriteAllBytes(string path, byte[] data) => Bytes[path] = data;
        public long GetFileLength(string path) => Bytes[path].LongLength;
    }
}
