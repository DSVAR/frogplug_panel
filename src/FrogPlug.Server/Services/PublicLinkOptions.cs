namespace FrogPlug.Server.Services;

public sealed class PublicLinkOptions
{
    public PublicLinkOptions(string panelPath, string subscriptionPath)
    {
        PanelPath = panelPath;
        SubscriptionPath = subscriptionPath;
    }

    public string PanelPath { get; }
    public string SubscriptionPath { get; }

    public string GetSubscriptionBaseUrl(string baseUrl)
    {
        if (string.IsNullOrEmpty(SubscriptionPath))
        {
            return baseUrl.TrimEnd('/');
        }

        var uri = new Uri(baseUrl.TrimEnd('/'), UriKind.Absolute);
        var path = uri.AbsolutePath.TrimEnd('/');
        if (!string.IsNullOrEmpty(PanelPath) && path.EndsWith(PanelPath, StringComparison.Ordinal))
        {
            path = path[..^PanelPath.Length];
        }

        var builder = new UriBuilder(uri)
        {
            Path = $"{path.TrimEnd('/')}{SubscriptionPath}"
        };
        return builder.Uri.AbsoluteUri.TrimEnd('/');
    }
}
