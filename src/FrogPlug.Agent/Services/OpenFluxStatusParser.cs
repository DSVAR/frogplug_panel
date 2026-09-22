using System.Text.Json;
using FrogPlug.Core;

namespace FrogPlug.Agent.Services;

public static class OpenFluxStatusParser
{
    public static OpenFluxStatus Parse(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var channels = document.RootElement.TryGetProperty("channels", out var values)
                ? values.EnumerateArray().Select(ToStatus).ToArray()
                : [];
            return new OpenFluxStatus(true, channels);
        }
        catch (JsonException)
        {
            return new OpenFluxStatus(true, []);
        }
    }

    private static OpenFluxChannelStatus ToStatus(JsonElement element)
    {
        var channel = element.GetProperty("channel").GetString() ?? string.Empty;
        var state = element.GetProperty("state").GetString() ?? "unknown";
        var port = element.TryGetProperty("port", out var value) ? value.GetInt32() : 0;
        return new OpenFluxChannelStatus(channel, state, port);
    }
}
