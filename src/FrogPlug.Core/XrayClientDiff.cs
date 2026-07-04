using System.Text.Json;
using System.Text.Json.Nodes;

namespace FrogPlug.Core;

public sealed record XrayClientChange(
    string Tag,
    IReadOnlyList<string> RemovedEmails,
    IReadOnlyList<JsonObject> AddedClients);

public sealed record XrayClientDiffResult(
    bool IsClientOnlyChange,
    IReadOnlyList<XrayClientChange> Changes);

public static class XrayClientDiff
{
    public static XrayClientDiffResult Compare(string current, string next)
    {
        try
        {
            var oldRoot = JsonNode.Parse(current)?.AsObject() ?? throw new JsonException();
            var newRoot = JsonNode.Parse(next)?.AsObject() ?? throw new JsonException();
            var oldInbounds = ReadInbounds(oldRoot);
            var newInbounds = ReadInbounds(newRoot);
            var changes = CompareInbounds(oldInbounds, newInbounds);
            RemoveClients(oldRoot);
            RemoveClients(newRoot);
            var onlyClients = JsonNode.DeepEquals(oldRoot, newRoot);
            return new XrayClientDiffResult(onlyClients, changes);
        }
        catch (JsonException)
        {
            return new XrayClientDiffResult(false, []);
        }
    }

    private static IReadOnlyList<XrayClientChange> CompareInbounds(
        IReadOnlyDictionary<string, JsonObject> oldInbounds,
        IReadOnlyDictionary<string, JsonObject> newInbounds)
    {
        var result = new List<XrayClientChange>();
        foreach (var tag in oldInbounds.Keys.Union(newInbounds.Keys, StringComparer.Ordinal))
        {
            if (!oldInbounds.TryGetValue(tag, out var oldInbound) ||
                !newInbounds.TryGetValue(tag, out var newInbound))
            {
                return [];
            }

            var oldClients = ReadClients(oldInbound);
            var newClients = ReadClients(newInbound);
            if (oldClients.Keys.Intersect(newClients.Keys, StringComparer.Ordinal)
                .Any(email => !JsonNode.DeepEquals(oldClients[email], newClients[email])))
            {
                return [];
            }

            var removed = oldClients.Keys.Except(newClients.Keys, StringComparer.Ordinal).ToArray();
            var added = newClients.Keys.Except(oldClients.Keys, StringComparer.Ordinal)
                .Select(email => newClients[email])
                .ToArray();
            if (removed.Length > 0 || added.Length > 0)
            {
                result.Add(new XrayClientChange(tag, removed, added));
            }
        }

        return result;
    }

    private static Dictionary<string, JsonObject> ReadInbounds(JsonObject root)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        if (root["inbounds"] is not JsonArray inbounds)
        {
            return result;
        }

        foreach (var item in inbounds)
        {
            if (item is not JsonObject inbound || inbound["tag"]?.GetValue<string>() is not { } tag)
            {
                continue;
            }

            result[tag] = inbound;
        }

        return result;
    }

    private static Dictionary<string, JsonObject> ReadClients(JsonObject inbound)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        if (inbound["settings"]?["clients"] is not JsonArray clients)
        {
            return result;
        }

        foreach (var item in clients)
        {
            if (item is not JsonObject client || client["email"]?.GetValue<string>() is not { } email)
            {
                continue;
            }

            result[email] = client;
        }

        return result;
    }

    private static void RemoveClients(JsonObject root)
    {
        if (root["inbounds"] is not JsonArray inbounds)
        {
            return;
        }

        foreach (var item in inbounds)
        {
            if (item is JsonObject inbound && inbound["settings"] is JsonObject settings)
            {
                settings.Remove("clients");
            }
        }
    }
}
