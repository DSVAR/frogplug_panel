using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FrogPlug.Core;
using FrogPlug.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Server.Services;

public sealed class SubscriptionService(AppDbContext db, SettingsService settings)
{
    // Сборка подписки для Happ.
    public async Task<(ClientUser? User, string Body, string ProfileTitle, string? SupportUrl, int UpdateHours, string? PublicBaseUrl)> Build(string token)
    {
        var user = await db.ClientUsers.FirstOrDefaultAsync(item => item.SubscriptionToken == token);
        var title = await settings.GetProfileTitle();
        var support = await settings.GetSupportUrl();
        var hours = await settings.GetSubscriptionUpdateHours();
        var baseUrl = await settings.GetPublicBaseUrl();
        if (user is null)
        {
            return (null, string.Empty, title, support, hours, baseUrl);
        }

        user.LastSubscriptionAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        if (!user.IsActiveAt(DateTime.UtcNow))
        {
            const string inactive = "vless://00000000-0000-0000-0000-000000000000@0.0.0.0:1?encryption=none#Подписка неактивна";
            return (user, inactive, title, support, hours, baseUrl);
        }
        var links = await GetLinks(user);
        var body = string.Join('\n', links.Select(item => item.Link));
        return (user, Convert.ToBase64String(Encoding.UTF8.GetBytes(body)), title, support, hours, baseUrl);
    }

    // Подписка для приложения OpenFlux.
    public async Task<IReadOnlyList<SubscriptionLinkEntry>> GetLinks(ClientUser user)
    {
        var nodes = await db.Nodes
            .Where(node => node.IsEnabled && node.Role.HasFlag(NodeRole.Entry))
            .OrderBy(node => node.Role.HasFlag(NodeRole.WhitelistEntry))
            .ThenBy(node => node.Name)
            .ToListAsync();
        var userSpec = new UserSpec { Id = user.Id, Name = user.Name, VlessUuid = user.VlessUuid };
        var links = new List<SubscriptionLinkEntry>();
        var entryCount = nodes.Count(node => node.RealityEnabled || node.XhttpEnabled || node.HysteriaEnabled);
        foreach (var node in nodes)
        {
            var whitelist = node.Role.HasFlag(NodeRole.WhitelistEntry);
            if (whitelist && !user.AllowWhitelist)
            {
                continue;
            }

            if (!whitelist && !user.AllowReality && !user.AllowXhttp)
            {
                continue;
            }

            var spec = ToSpec(node);
            if (node.RealityEnabled && (whitelist || user.AllowReality))
            {
                links.Add(new SubscriptionLinkEntry(
                    SubscriptionLinkProtocol.Reality,
                    node.Name,
                    node.Country,
                    VlessLinks.Reality(spec, userSpec, NodeRemarks.Reality(spec, user.Name, entryCount > 1))));
            }

            if (user.AllowXhttp && node.XhttpEnabled)
            {
                links.Add(new SubscriptionLinkEntry(
                    SubscriptionLinkProtocol.Xhttp,
                    node.Name,
                    node.Country,
                    VlessLinks.Xhttp(spec, userSpec, NodeRemarks.Xhttp(spec, user.Name, entryCount > 1))));
            }

            if (user.AllowHysteria && node.HysteriaEnabled)
            {
                links.Add(new SubscriptionLinkEntry(
                    SubscriptionLinkProtocol.Hysteria,
                    node.Name,
                    node.Country,
                    HysteriaLinks.Build(
                        user.Id.ToString(),
                        user.VlessUuid.ToString(),
                        node.Domain ?? node.Address,
                        node.HysteriaPort,
                        NodeRemarks.Hysteria(spec, user.Name, entryCount > 1),
                        node.HysteriaObfsEnabled ? HysteriaLinks.ObfsPassword(node.CascadeUuid) : null,
                        node.HysteriaPinSha256)));
            }
        }

        return links;
    }

    public async Task<OpenFluxSubscriptionResult> BuildOpenFlux(
        string token,
        IReadOnlySet<string> supportedTransports)
    {
        var user = await db.ClientUsers.FirstOrDefaultAsync(item => item.SubscriptionToken == token);
        var title = await settings.GetProfileTitle();
        var support = await settings.GetSupportUrl();
        var updateHours = await settings.GetSubscriptionUpdateHours();
        if (user is null)
        {
            return new OpenFluxSubscriptionResult(null, string.Empty);
        }

        user.LastSubscriptionAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var status = GetOpenFluxStatus(user, DateTime.UtcNow);
        var profiles = status == "active" && user.AllowWhitelist
            ? await GetOpenFluxProfiles(supportedTransports)
            : [];
        var document = new OpenFluxSubscriptionDocument(
            1,
            title,
            status,
            ToUnixTimeSeconds(user.ExpiresAt),
            updateHours,
            support,
            string.Empty,
            profiles);
        return new OpenFluxSubscriptionResult(
            user,
            JsonSerializer.Serialize(document));
    }

    private async Task<OpenFluxSubscriptionProfile[]> GetOpenFluxProfiles(
        IReadOnlySet<string> supportedTransports)
    {
        var channels = await db.OpenFluxChannels
            .Include(item => item.Node)
            .Where(item => item.IsEnabled && item.Node!.IsEnabled)
            .OrderBy(item => item.Name)
            .ToListAsync();
        return channels
            .Where(channel => SupportsTransport(channel, supportedTransports))
            .Select(channel => ToOpenFluxProfile(channel, supportedTransports))
            .ToArray();
    }

    private static OpenFluxSubscriptionProfile ToOpenFluxProfile(
        OpenFluxChannel channel,
        IReadOnlySet<string> supportedTransports)
    {
        var transports = OpenFluxService.GetTransports(channel)
            .Where(transport => supportedTransports.Contains(transport.Type))
            .ToArray();
        var direct = new OpenFluxTransport(
            "direct",
            Dial: $"{channel.Node!.Address}:{channel.Port}");
        var config = new OpenFluxShareConfig(
            channel.Name,
            true,
            null,
            channel.Key,
            transports.FirstOrDefault()?.Url,
            new[] { direct }.Concat(transports).ToArray());
        return new OpenFluxSubscriptionProfile(
            $"channel-{channel.Id}",
            ChannelName(channel),
            OpenFluxShare.Encode(config));
    }

    private static bool SupportsTransport(
        OpenFluxChannel channel,
        IReadOnlySet<string> supportedTransports)
    {
        return OpenFluxService.GetTransports(channel)
            .Any(transport => supportedTransports.Contains(transport.Type));
    }

    private static string ChannelName(OpenFluxChannel channel)
    {
        return string.IsNullOrWhiteSpace(channel.Node?.Name)
            ? channel.Name
            : $"{channel.Name} · {channel.Node.Name}";
    }

    private static string GetOpenFluxStatus(ClientUser user, DateTime nowUtc)
    {
        if (!user.IsEnabled || user.TrafficLimitBytes is { } limit
            && user.UsedUploadBytes + user.UsedDownloadBytes >= limit)
        {
            return "revoked";
        }

        return user.ExpiresAt is { } expires && expires <= nowUtc
            ? "expired"
            : "active";
    }

    private static long ToUnixTimeSeconds(DateTime? value)
    {
        return value is { } expires
            ? new DateTimeOffset(expires).ToUnixTimeSeconds()
            : 0;
    }

    private static NodeSpec ToSpec(Node node) => new()
    {
        Id = node.Id,
        Name = node.Name,
        Address = node.Address,
        Country = node.Country,
        Roles = node.Role.ToString(),
        IsEnabled = node.IsEnabled,
        Domain = node.Domain,
        RealityEnabled = node.RealityEnabled,
        RealityPort = node.RealityPort,
        MaskMode = node.MaskMode,
        RealitySni = node.RealitySni ?? string.Empty,
        WhitelistExitNodeId = node.WhitelistExitNodeId,
        RealityPrivateKey = node.RealityPrivateKey,
        RealityPublicKey = node.RealityPublicKey,
        RealityShortId = node.RealityShortId,
        RealityServerName = node.RealityServerName,
        RealityDest = node.RealityDest,
        XhttpPath = node.XhttpPath,
        XhttpEnabled = node.XhttpEnabled,
        CascadeUuid = node.CascadeUuid
    };

    private static string Remark(NodeSpec node, bool whitelist)
    {
        return whitelist
            ? $"🛡 Белые списки — {node.Name}"
            : $"🌍 {node.Country} {node.Name}";
    }

}

public enum SubscriptionLinkProtocol
{
    Reality,
    Xhttp,
    Hysteria
}

public sealed record SubscriptionLinkEntry(
    SubscriptionLinkProtocol Protocol,
    string NodeName,
    string CountryCode,
    string Link);

public sealed record OpenFluxSubscriptionResult(
    ClientUser? User,
    string Body);

public sealed record OpenFluxSubscriptionDocument(
    [property: JsonPropertyName("version")]
    int Version,
    [property: JsonPropertyName("title")]
    string Title,
    [property: JsonPropertyName("status")]
    string Status,
    [property: JsonPropertyName("expires")]
    long Expires,
    [property: JsonPropertyName("updateHours")]
    int UpdateHours,
    [property: JsonPropertyName("support")]
    string? Support,
    [property: JsonPropertyName("announce")]
    string Announce,
    [property: JsonPropertyName("profiles")]
    IReadOnlyList<OpenFluxSubscriptionProfile> Profiles);

public sealed record OpenFluxSubscriptionProfile(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("name")]
    string Name,
    [property: JsonPropertyName("link")]
    string Link);
