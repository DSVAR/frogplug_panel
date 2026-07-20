using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using FrogPlug.Agent;
using FrogPlug.Agent.Services;
using Microsoft.Extensions.Options;
using Xunit;

namespace FrogPlug.Agent.Tests;

public sealed class XrayUpdaterTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "fp-updater-" + Guid.NewGuid().ToString("N"));

    public XrayUpdaterTests()
    {
        Directory.CreateDirectory(directory);
    }

    [Fact]
    public async Task Update_uses_panel_archive_and_checksum()
    {
        var archive = CreateArchive();
        var handler = new ArchiveHandler(archive, Digest(archive));
        var updater = CreateUpdater(handler);

        var result = await updater.UpdateAsync(
            "v25.10.15",
            null,
            "https://panel.example/agent/xray/v25.10.15/linux-x64",
            Digest(archive),
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(["https://panel.example/agent/xray/v25.10.15/linux-x64"], handler.Urls);
    }

    [Fact]
    public async Task Update_without_panel_address_uses_release_digest()
    {
        var archive = CreateArchive();
        var handler = new ArchiveHandler(archive, Digest(archive));
        var updater = CreateUpdater(handler);

        var result = await updater.UpdateAsync("v25.10.15", null, null, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(handler.Urls, url => url.EndsWith(".zip", StringComparison.Ordinal));
        Assert.Contains(handler.Urls, url => url.EndsWith(".zip.dgst", StringComparison.Ordinal));
    }

    public void Dispose()
    {
        Directory.Delete(directory, true);
    }

    private XrayUpdater CreateUpdater(ArchiveHandler handler)
    {
        var binary = Path.Combine(directory, "xray");
        File.WriteAllText(binary, "old");
        var options = Options.Create(new AgentOptions { XrayBinary = binary });
        var runner = new Runner();
        var manager = new XrayManager(options, runner, new PhysicalFileSystem());
        return new XrayUpdater(new Factory(handler), options, new PhysicalFileSystem(), runner, manager);
    }

    private static byte[] CreateArchive()
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("xray").Open());
            writer.Write("new");
        }

        return stream.ToArray();
    }

    private static string Digest(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();

    private sealed class Factory(ArchiveHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(handler);
        }
    }

    private sealed class ArchiveHandler(byte[] archive, string digest) : HttpMessageHandler
    {
        public List<string> Urls { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Urls.Add(request.RequestUri!.AbsoluteUri);
            HttpContent content = request.RequestUri.AbsoluteUri.EndsWith(".dgst", StringComparison.Ordinal)
                ? new StringContent("SHA2-256=" + digest)
                : new ByteArrayContent(archive);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }

    private sealed class Runner : ICommandRunner
    {
        public Task<CommandResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new CommandResult(0, "Xray", string.Empty));
        }
    }
}
