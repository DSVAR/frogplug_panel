using System.Net;
using System.Security.Cryptography;
using System.Text;
using FrogPlug.Agent;
using FrogPlug.Agent.Services;
using FrogPlug.Core;
using Microsoft.Extensions.Options;
using Xunit;

namespace FrogPlug.Agent.Tests;

public sealed class OpenFluxManagerTests
{
    [Fact]
    public async Task Empty_sha_is_refused_with_clear_message()
    {
        var manager = Create("installer");
        var result = await manager.Manager.ApplyAsync(Request(sha: string.Empty), default);
        Assert.False(result.Success);
        Assert.Contains("SHA-256", result.Error);
    }

    [Fact]
    public async Task Mismatched_sha_is_refused()
    {
        var manager = Create("installer");
        var result = await manager.Manager.ApplyAsync(Request(sha: new string('0', 64)), default);
        Assert.False(result.Success);
        Assert.Empty(manager.Runner.Calls);
    }

    [Fact]
    public void Config_content_is_exact_and_keeps_secrets_out_of_output()
    {
        var request = Request(cookies: "{\"cookie\":\"secret\"}");
        var config = OpenFluxManager.BuildConfig(request);
        var expected = string.Join(
            "\n",
            [
                "channel=demo",
                "key=" + new string('a', 64),
                "port=8445",
                "autoupdate=no",
                "mode=l4",
                "mailru=https://cloud.mail.ru/public/ab/cdef",
                "cookies=eyJjb29raWUiOiJzZWNyZXQifQ==",
                string.Empty
            ]);

        Assert.Equal(expected, config);
    }

    [Fact]
    public async Task Returned_output_never_contains_key_or_cookies()
    {
        var installer = "installer";
        var manager = Create(installer);
        manager.Runner.Output = "key=" + new string('a', 64) + "\ncookies=secret";
        var result = await manager.Manager.ApplyAsync(Request(sha: Hash(installer)), default);
        Assert.DoesNotContain(new string('a', 64), result.Output);
        Assert.DoesNotContain("cookies=secret", result.Output);
    }

    [Fact]
    public async Task Dropin_is_written_only_for_xray_upstream()
    {
        var installer = "installer";
        var manager = Create(installer);
        await manager.Manager.ApplyAsync(Request(sha: Hash(installer), useXray: false), default);
        Assert.DoesNotContain(manager.Files.Text.Keys, key => key.EndsWith("frogplug.conf", StringComparison.Ordinal));
        await manager.Manager.ApplyAsync(Request(sha: Hash(installer), useXray: true), default);
        Assert.Contains(manager.Files.Text.Keys, key => key.EndsWith("frogplug.conf", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Remove_calls_installer_remove_channel()
    {
        var manager = Create("installer");
        await manager.Manager.RemoveAsync("demo", default);
        Assert.Contains(
            manager.Runner.Calls,
            call => call.FileName == "bash"
                && call.Arguments.SequenceEqual(
                    ["/opt/openflux-node/node-install.sh", "remove", "demo"]));
    }

    [Fact]
    public async Task Docker_mode_writes_node_and_supervisor_configs()
    {
        var manager = CreateDocker();

        var result = await manager.Manager.ApplyAsync(Request(), default);

        Assert.True(result.Success);
        Assert.Contains(
            "Role = exit\nMode = l4",
            manager.Files.Text["/etc/openflux-node/demo/node.conf"]);
        Assert.Contains(
            "Listen = 127.0.0.1:8445",
            manager.Files.Text["/etc/openflux-node/demo/node.conf"]);
        Assert.Equal(
            new string('a', 64) + "\n",
            manager.Files.Text["/etc/openflux-node/demo/encryption-key"]);
        Assert.Contains(
            "--upstream-proxy 127.0.0.1:10808",
            manager.Files.Text["/etc/supervisor/conf.d/openflux-demo.conf"]);
        Assert.Contains(
            manager.Runner.Calls,
            call => call.FileName == "supervisorctl" && call.Arguments.SequenceEqual(["reread"]));
        Assert.Contains(
            manager.Runner.Calls,
            call => call.FileName == "supervisorctl" && call.Arguments.SequenceEqual(["update"]));
    }

    [Fact]
    public async Task Docker_mode_removal_deletes_program_and_updates_supervisor()
    {
        var manager = CreateDocker();
        manager.Files.Text["/etc/supervisor/conf.d/openflux-demo.conf"] = "program";
        manager.Files.Text["/etc/openflux-node/demo/node.conf"] = "config";
        manager.Files.Text["/etc/openflux-node/demo/encryption-key"] = "key";

        var result = await manager.Manager.RemoveAsync("demo", default);

        Assert.True(result.Success);
        Assert.DoesNotContain("/etc/supervisor/conf.d/openflux-demo.conf", manager.Files.Text.Keys);
        Assert.DoesNotContain("/etc/openflux-node/demo/node.conf", manager.Files.Text.Keys);
        Assert.Contains(
            manager.Runner.Calls,
            call => call.FileName == "supervisorctl" && call.Arguments.SequenceEqual(["reread"]));
        Assert.Contains(
            manager.Runner.Calls,
            call => call.FileName == "supervisorctl" && call.Arguments.SequenceEqual(["update"]));
    }

    private static TestManager Create(string installer)
    {
        var files = new TestFileSystem();
        var runner = new TestRunner();
        var http = new HttpClient(new TextHandler(installer));
        return new TestManager(new OpenFluxManager(files, runner, http), files, runner);
    }

    private static TestManager CreateDocker()
    {
        var files = new TestFileSystem();
        var runner = new TestRunner();
        var options = Options.Create(new AgentOptions { ServiceMode = "docker" });
        var manager = new OpenFluxManager(
            files,
            runner,
            new HttpClient(new TextHandler("installer")),
            new SupervisorServiceController(runner),
            options);
        return new TestManager(manager, files, runner);
    }

    private static OpenFluxApplyRequest Request(string? sha = null, bool useXray = true, string? cookies = null)
    {
        return new OpenFluxApplyRequest(
            "demo",
            new string('a', 64),
            8445,
            null,
            null,
            "https://cloud.mail.ru/public/ab/cdef",
            null,
            null,
            null,
            null,
            cookies,
            useXray,
            "v0.17.1",
            sha ?? new string('a', 64));
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record TestManager(OpenFluxManager Manager, TestFileSystem Files, TestRunner Runner);
    private sealed class TextHandler(string content) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken token)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(content)
            };
            return Task.FromResult(response);
        }
    }

    private sealed class TestRunner : ICommandRunner
    {
        public List<(string FileName, IReadOnlyList<string> Arguments)> Calls { get; } = [];

        public string Output { get; set; } = "{\"ok\":true}";

        public Task<CommandResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken token = default)
        {
            Calls.Add((fileName, arguments));
            return Task.FromResult(new CommandResult(0, Output, string.Empty));
        }
    }

    private sealed class TestFileSystem : IFileSystem
    {
        public Dictionary<string, string> Text { get; } = [];

        public bool FileExists(string path)
        {
            return Text.ContainsKey(path);
        }

        public Task<string> ReadTextAsync(string path, CancellationToken token = default)
        {
            return Task.FromResult(Text[path]);
        }

        public Task WriteTextAsync(string path, string content, CancellationToken token = default)
        {
            Text[path] = content;
            return Task.CompletedTask;
        }

        public void CopyFile(string source, string destination, bool overwrite)
        {
        }

        public void MoveFile(string source, string destination, bool overwrite)
        {
        }

        public void DeleteFile(string path)
        {
            Text.Remove(path);
        }

        public void CreateDirectory(string path)
        {
        }

        public string GetTempFileName()
        {
            return "/tmp/openflux-test";
        }

        public byte[] ReadAllBytes(string path)
        {
            return [];
        }

        public void WriteAllBytes(string path, byte[] data)
        {
        }

        public long GetFileLength(string path)
        {
            return 0;
        }
    }
}
