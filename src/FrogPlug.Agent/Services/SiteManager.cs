using FrogPlug.Core;

namespace FrogPlug.Agent.Services;

public sealed class SiteManager(IFileSystem files, IServiceController services, TimeProvider? clock = null)
{
    private const string DirectoryPath = "/var/lib/frogplug/site";

    public async Task ApplyAsync(SiteApplyRequest request, CancellationToken cancellationToken)
    {
        files.CreateDirectory(DirectoryPath);
        var template = StubSiteTemplates.All.Contains(request.Template, StringComparer.Ordinal)
            ? request.Template
            : StubSiteTemplates.PersonalBlog;
        // Сайт «растёт» сам: новые записи появляются по текущей дате.
        var today = DateOnly.FromDateTime((clock ?? TimeProvider.System).GetUtcNow().UtcDateTime);
        var siteFiles = StubSiteTemplates.BuildSite(template, request.Title ?? string.Empty, request.NodeId, today);
        var changed = false;
        foreach (var file in siteFiles)
        {
            var path = Path.Combine(DirectoryPath, file.Key);
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                files.CreateDirectory(directory);
            }

            changed |= await WriteIfChangedAsync(path, file.Value, cancellationToken);
        }

        // tproxy-server держит сайт в памяти и читает его только при старте.
        if (changed && (await services.IsActiveAsync("tgweb", cancellationToken)).ExitCode == 0)
        {
            await services.RestartAsync("tgweb", cancellationToken);
        }
    }

    private async Task<bool> WriteIfChangedAsync(string path, string content, CancellationToken cancellationToken)
    {
        if (await FileContent.EqualsNormalizedAsync(files, path, content, cancellationToken))
        {
            return false;
        }

        await files.WriteTextAsync(path, content, cancellationToken);
        return true;
    }
}
