using System.Text;
using System.Text.Json;

namespace FrogPlug.Core;

public sealed record TunnelRequest(string Id, string Method, string Path, string? BodyJson);

public sealed record TunnelResponse(string Id, int Status, string? BodyJson, string? Error);

public sealed record TunnelHello(string AgentVersion, string Arch);

public static class TunnelSerializer
{
    public const int MaxMessageBytes = 8 * 1024 * 1024;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, Options);
    }

    public static T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, Options);
    }

    public static byte[] ToUtf8(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        if (bytes.Length > MaxMessageBytes)
        {
            throw new InvalidOperationException("Сообщение туннеля превышает 8 МБ");
        }

        return bytes;
    }
}
