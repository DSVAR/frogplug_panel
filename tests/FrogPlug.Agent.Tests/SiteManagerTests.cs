using FrogPlug.Agent.Services;
using FrogPlug.Core;
using Xunit;

namespace FrogPlug.Agent.Tests;

public sealed class SiteManagerTests
{
    private static readonly SiteApplyRequest Request =
        new(StubSiteTemplates.PersonalBlog, null, Guid.Parse("11111111-2222-3333-4444-555555555555"));

    [Fact]
    public async Task Changed_site_restarts_running_tgweb_once()
    {
        var files = new HaproxyManagerTests.MemoryFiles();
        var services = new HaproxyManagerTests.RecordingServices();
        var manager = new SiteManager(files, services);

        await manager.ApplyAsync(Request, CancellationToken.None);
        await manager.ApplyAsync(Request, CancellationToken.None);

        // Первый раз файлы новые — tproxy перечитывает сайт; повторно файлы те же — без перезапуска.
        Assert.Equal(["active tgweb", "restart tgweb"], services.Calls);
        Assert.Contains(files.Text.Keys, key => Path.GetFileName(key) == "about.html");
    }
}
