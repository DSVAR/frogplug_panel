using System.Text.RegularExpressions;

namespace FrogPlug.Tests;

public sealed class RelativeLinksTests
{
    // Панель живёт под секретным префиксом: ссылки от корня сайта («/users») уводят мимо него.
    [Fact]
    public void Razor_links_do_not_start_from_site_root()
    {
        var root = FindComponentsDirectory();
        var pattern = new Regex("(href|action)=\"/", RegexOptions.CultureInvariant);
        var offenders = Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories)
            .SelectMany(file => File.ReadLines(file)
                .Where(line => pattern.IsMatch(line))
                .Select(line => $"{Path.GetFileName(file)}: {line.Trim()}"))
            .ToList();

        Assert.Empty(offenders);
    }

    private static string FindComponentsDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "FrogPlug.Server", "Components");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Не найдена папка Components");
    }
}
