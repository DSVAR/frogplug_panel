using System.Net;
using System.Security.Cryptography;
using FrogPlug.Server.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace FrogPlug.Tests;

public sealed class XrayCoreStoreTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "fp-xray-" + Guid.NewGuid().ToString("N"));

    public XrayCoreStoreTests()
    {
        Directory.CreateDirectory(root);
    }

    [Fact]
    public async Task Ensure_verified_download_stores_archive()
    {
        var archive = new byte[] { 1, 2, 3 };
        var store = CreateStore(archive, Digest(archive));

        var core = await store.EnsureAsync("v25.10.15", "linux-x64");

        Assert.Equal(Digest(archive), core.Sha256);
        Assert.Equal(archive, await File.ReadAllBytesAsync(core.Path));
        Assert.NotNull(await store.GetAsync("v25.10.15", "linux-x64"));
    }

    [Fact]
    public async Task Ensure_wrong_digest_rejects_without_storing()
    {
        var store = CreateStore([1, 2, 3], new string('0', 64));

        await Assert.ThrowsAsync<InvalidDataException>(() => store.EnsureAsync("v25.10.15", "linux-x64"));

        Assert.Null(await store.GetAsync("v25.10.15", "linux-x64"));
    }

    [Theory]
    [InlineData("25.10.15", "linux-x64")]
    [InlineData("v25.10.15", "../linux-x64")]
    public async Task Ensure_rejects_invalid_version_or_rid(string version, string rid)
    {
        var store = CreateStore([1], Digest([1]));

        await Assert.ThrowsAsync<ArgumentException>(() => store.EnsureAsync(version, rid));
    }

    [Fact]
    public async Task List_imports_verified_bundled_core()
    {
        var archive = new byte[] { 5, 6, 7 };
        var bundled = Path.Combine(root, "bundled", "v25.10.15", "linux-x64");
        Directory.CreateDirectory(bundled);
        var file = Path.Combine(bundled, "Xray-linux-64.zip");
        await File.WriteAllBytesAsync(file, archive);
        await File.WriteAllTextAsync(file + ".sha256", Digest(archive));
        var store = CreateStore([1], Digest([1]), Path.Combine(root, "bundled"));

        var versions = await store.ListAsync();

        Assert.Contains(versions, item => item.Version == "v25.10.15" && item.Rids.Contains("linux-x64"));
    }

    public void Dispose()
    {
        Directory.Delete(root, true);
    }

    private XrayCoreStore CreateStore(byte[] archive, string digest, string? bundled = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["DataDirectory"] = Path.Combine(root, "data"),
            ["XrayDefaultVersion"] = "v25.10.15"
        };
        if (bundled is not null)
        {
            values["XrayCore:BundledPath"] = bundled;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var environment = new FakeEnvironment { ContentRootPath = root };
        return new XrayCoreStore(new FakeFactory(archive, digest), configuration, environment);
    }

    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private sealed class FakeFactory(byte[] archive, string digest) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return new HttpClient(new FakeHandler(archive, digest));
        }
    }

    private sealed class FakeEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "FrogPlug.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class FakeHandler(byte[] archive, string digest) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = request.RequestUri!.AbsoluteUri.EndsWith(".dgst", StringComparison.Ordinal)
                ? new StringContent("SHA2-256=" + digest)
                : new ByteArrayContent(archive);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
