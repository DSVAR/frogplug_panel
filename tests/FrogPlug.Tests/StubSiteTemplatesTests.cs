using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class StubSiteTemplatesTests
{
    [Fact]
    public void Generates_identical_files_for_the_same_node()
    {
        var nodeId = Guid.Parse("2e8254f5-2cd0-45d8-8058-bba3a2f42914");

        var first = StubSiteTemplates.BuildSite(StubSiteTemplates.PersonalBlog, "Тестовый журнал", nodeId);
        var second = StubSiteTemplates.BuildSite(StubSiteTemplates.PersonalBlog, "Тестовый журнал", nodeId);

        Assert.Equal(first.OrderBy(item => item.Key), second.OrderBy(item => item.Key));
        Assert.Equal("Тестовый журнал", GetTitle(first["index.html"]));
    }

    [Fact]
    public void Generates_different_sites_for_different_nodes()
    {
        var first = StubSiteTemplates.BuildSite(StubSiteTemplates.Photography, string.Empty, Guid.NewGuid());
        var second = StubSiteTemplates.BuildSite(StubSiteTemplates.Photography, string.Empty, Guid.NewGuid());

        Assert.NotEqual(first["index.html"], second["index.html"]);
    }

    [Fact]
    public void Includes_static_site_documents_and_post_pages()
    {
        var site = StubSiteTemplates.BuildSite(StubSiteTemplates.ItConsulting, string.Empty, Guid.Empty);

        Assert.Contains("about.html", site.Keys);
        Assert.Contains("contacts.html", site.Keys);
        // tproxy-server не отдаёт каталоги с index.html и блокирует встроенные стили и скрипты.
        Assert.DoesNotContain(site.Keys, key => key.EndsWith("/index.html", StringComparison.Ordinal));
        Assert.All(site.Where(file => file.Key.EndsWith(".html", StringComparison.Ordinal)), file =>
        {
            Assert.DoesNotContain("<style", file.Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(" style=", file.Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<script", file.Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("/\"", file.Value.Replace("href=\"/\"", string.Empty), StringComparison.Ordinal);
        });
        Assert.Contains("assets/site.css", site.Keys);
        Assert.Contains("favicon.svg", site.Keys);
        Assert.Contains("sitemap.xml", site.Keys);
        Assert.Contains("rss.xml", site.Keys);
        Assert.Contains("Sitemap: /sitemap.xml", site["robots.txt"]);
        Assert.True(site.Keys.Count(key => key.StartsWith("notes/", StringComparison.Ordinal)) >= 6);
    }

    private static readonly DateOnly Today = new(2026, 11, 12);

    [Fact]
    public void Twenty_installs_get_twenty_different_home_pages()
    {
        var pages = Enumerable.Range(0, 20)
            .Select(_ => StubSiteTemplates.BuildSite(StubSiteTemplates.PersonalBlog, string.Empty, Guid.NewGuid(), Today)["index.html"])
            .Distinct(StringComparer.Ordinal)
            .Count();

        Assert.Equal(20, pages);
    }

    [Fact]
    public void Site_grows_over_time_without_rewriting_old_notes()
    {
        var seed = Guid.Parse("6f1d2c3b-4a59-4e6f-8a7b-9c0d1e2f3a4b");
        var spring = new DateOnly(2026, 3, 1);
        var now = StubSiteTemplates.BuildSite(StubSiteTemplates.PersonalBlog, string.Empty, seed, spring);
        var later = StubSiteTemplates.BuildSite(StubSiteTemplates.PersonalBlog, string.Empty, seed, spring.AddDays(60));
        var notes = (IReadOnlyDictionary<string, string> site) =>
            site.Keys.Where(key => key.StartsWith("notes/", StringComparison.Ordinal)).ToList();
        var date = (string key) => System.Text.RegularExpressions.Regex.Match(key, @"\d{4}-\d{2}-\d{2}").Value;

        Assert.True(notes(later).Count > notes(now).Count);
        // Меняется только последняя запись: у неё появляется ссылка «Следующая».
        var newest = notes(now).MaxBy(key => date(key))!;
        Assert.All(notes(now).Where(key => key != newest), key => Assert.Equal(now[key], later[key]));
    }

    [Fact]
    public void Sitemap_and_rss_are_valid_xml()
    {
        var site = StubSiteTemplates.BuildSite(StubSiteTemplates.ItConsulting, string.Empty, Guid.NewGuid(), Today);

        var sitemap = System.Xml.Linq.XDocument.Parse(site["sitemap.xml"]);
        var rss = System.Xml.Linq.XDocument.Parse(site["rss.xml"]);

        Assert.NotEmpty(sitemap.Descendants().Where(element => element.Name.LocalName == "url"));
        Assert.NotEmpty(rss.Descendants("item"));
    }

    [Fact]
    public void Notes_have_navigation_related_posts_and_no_external_resources()
    {
        var site = StubSiteTemplates.BuildSite(StubSiteTemplates.PersonalBlog, string.Empty, Guid.NewGuid(), Today);
        var notes = site.Where(file => file.Key.StartsWith("notes/", StringComparison.Ordinal)).ToList();

        Assert.All(notes, note => Assert.Contains("class=\"post-nav\"><a ", note.Value.Replace("<span></span>", string.Empty)));
        Assert.Contains(notes, note => note.Value.Contains("class=\"related\"><article", StringComparison.Ordinal));
        Assert.All(site.Where(file => file.Key.EndsWith(".html", StringComparison.Ordinal)), file =>
        {
            Assert.DoesNotContain("src=\"http", file.Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("href=\"http", file.Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<form", file.Value, StringComparison.OrdinalIgnoreCase);
        });
        Assert.True(site.Values.Sum(content => System.Text.Encoding.UTF8.GetByteCount(content)) < 1_500_000);
    }

    private static string GetTitle(string html)
    {
        var start = html.IndexOf("<h1>", StringComparison.Ordinal) + 4;
        var end = html.IndexOf("</h1>", start, StringComparison.Ordinal);
        return html[start..end];
    }
}