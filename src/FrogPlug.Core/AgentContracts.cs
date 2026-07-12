using System.Text.Json.Serialization;

namespace FrogPlug.Core;

public sealed record AgentConfig(
    string Token,
    string Listen = "127.0.0.1:9080",
    string? PanelUrl = null,
    string[]? AllowedIps = null);

public sealed record XrayConfigRequest(string ConfigJson);

public sealed record XrayUpdateRequest(
    string Version,
    string? Arch = null,
    string? DownloadUrl = null,
    string? Sha256 = null);

public sealed record CaddyApplyRequest(
    string Domain,
    string XhttpPath,
    int XhttpUpstreamPort = 10443,
    string? AcmeEmail = null,
    bool XhttpEnabled = true,
    string? MtprotoHost = null,
    bool TgWebEnabled = false);

public sealed record HaproxyApplyRequest(bool MtprotoEnabled, string? MtprotoHost);

public sealed record SiteApplyRequest(string Template, string? Title, Guid NodeId);

public sealed record AgentStatus(
    string Version,
    string Hostname,
    long UptimeSeconds,
    double LoadAverage,
    long MemoryBytes,
    long DiskFreeBytes,
    ComponentStatus Xray,
    ComponentStatus Caddy,
    ComponentStatus Mtg,
    OpenFluxStatus Openflux,
    double CpuPercent = 0,
    double MemoryUsedPercent = 0,
    string? Architecture = null);

public sealed record ComponentStatus(bool Installed, bool Active, string? Version = null);

public sealed record OpenFluxStatus(bool Installed, OpenFluxChannelStatus[] Channels);

public sealed record OpenFluxChannelStatus(string Channel, string State, int Port);

public sealed record OpenFluxApplyRequest(
    string Channel,
    string Key,
    int Port,
    string? VyandexUrl,
    string? YandexUrl,
    string? MailruUrl,
    string? TelemostUrl,
    string? BoardsUrl,
    string? AvitoUrl,
    string? CupsUrl,
    string? CookiesJson,
    bool UseXrayUpstream,
    string OpenFluxRef,
    string InstallerSha256,
    string? MaxToken = null,
    long? MaxUid = null);

public sealed record OpenFluxRemoveRequest(string Channel);

public sealed record MtprotoApplyRequest(
    bool Enabled,
    string? Secret,
    int Port,
    int FrontingPort,
    string Version,
    string? Sha256,
    string? FrontingHost = null);

public sealed record TgWebApplyRequest(
    bool Enabled,
    string Hostname,
    string Secret,
    int BackendPort = NodePorts.TelegramWebMtproxy);

public sealed record HysteriaApplyRequest(
    bool Enabled,
    string Domain,
    int Port,
    HysteriaUser[] Users,
    string? ObfsPassword = null,
    string? RealitySni = null);

public sealed record HysteriaApplyResult(bool Success, string? Error = null, string? PinSha256 = null);

public sealed record TrafficStats(Dictionary<string, TrafficStat> Users);

public sealed record TrafficStat(long Up, long Down);

public static class AgentParsers
{
    public static string? ParseDigest(string text)
    {
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var marker = "SHA2-256=";
            var index = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            var value = line[(index + marker.Length)..].Trim().Split(' ', '\t')[0];
            if (value.Length == 64)
            {
                return value.ToLowerInvariant();
            }
        }
        return null;
    }

    public static string? ParseSha256Sum(string text)
    {
        var first = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (first is null) return null;
        var value = first.Trim().Split(' ', '\t')[0];
        return value.Length == 64 && value.All(Uri.IsHexDigit) ? value.ToLowerInvariant() : null;
    }

    /// <summary>
    /// Разбирает вывод `xray api statsquery -pattern "user>>>"`.
    /// Имена вида user>>>{email}>>>traffic>>>uplink|downlink; int64 в protojson приходит строкой.
    /// </summary>
    public static TrafficStats ParseStats(string json)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        var result = new Dictionary<string, TrafficStat>(StringComparer.OrdinalIgnoreCase);
        if (!doc.RootElement.TryGetProperty("stat", out var items))
        {
            return new TrafficStats(result);
        }

        if (items.ValueKind != System.Text.Json.JsonValueKind.Array)
        {
            return new TrafficStats(result);
        }

        foreach (var item in items.EnumerateArray())
        {
            AddStat(result, item);
        }

        return new TrafficStats(result);
    }

    private static void AddStat(Dictionary<string, TrafficStat> result, System.Text.Json.JsonElement item)
    {
        if (!item.TryGetProperty("name", out var nameElement))
        {
            return;
        }

        var parts = (nameElement.GetString() ?? string.Empty).Split(">>>");
        if (parts.Length != 4 || parts[0] != "user" || parts[1].Length == 0)
        {
            return;
        }

        var value = ReadInt64(item, "value");
        var current = result.GetValueOrDefault(parts[1], new TrafficStat(0, 0));
        if (parts[3] == "uplink")
        {
            result[parts[1]] = current with { Up = current.Up + value };
        }
        else if (parts[3] == "downlink")
        {
            result[parts[1]] = current with { Down = current.Down + value };
        }
    }

    private static long ReadInt64(System.Text.Json.JsonElement item, string property)
    {
        if (!item.TryGetProperty(property, out var element))
        {
            return 0;
        }

        if (element.ValueKind == System.Text.Json.JsonValueKind.String)
        {
            return long.TryParse(element.GetString(), out var parsed) ? parsed : 0;
        }

        return element.GetInt64();
    }
}
