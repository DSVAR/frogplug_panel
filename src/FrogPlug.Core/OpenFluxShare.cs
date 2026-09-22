using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FrogPlug.Core;

public sealed record OpenFluxTransport(
    string Type,
    string? Name = null,
    string? Url = null,
    int Priority = 0,
    string? Dial = null);

public sealed record OpenFluxShareConfig(
    string? Name,
    bool Negotiate,
    string? Codec,
    string? Secret,
    string? Context,
    IReadOnlyList<OpenFluxTransport> Transports);

public static class OpenFluxShare
{
    private const string Prefix = "openflux://v1/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Encoder = JavaScriptEncoder.Default,
        PropertyNamingPolicy = null
    };

    public static string Encode(OpenFluxShareConfig config)
    {
        Validate(config);
        var payload = JsonSerializer.SerializeToUtf8Bytes(ToPayload(config), JsonOptions);
        using var output = new MemoryStream();
        using (var writer = new DeflateStream(output, CompressionLevel.SmallestSize, true))
        {
            writer.Write(payload);
        }

        return Prefix + Base64UrlEncode(output.ToArray());
    }

    private static SharePayload ToPayload(OpenFluxShareConfig config)
    {
        return new SharePayload(
            config.Name,
            config.Negotiate,
            config.Codec,
            config.Secret,
            config.Context,
            config.Transports.Select(item => new TransportPayload(
                item.Type,
                item.Name,
                item.Url,
                item.Priority,
                item.Dial)).ToArray());
    }

    private static void Validate(OpenFluxShareConfig config)
    {
        if (config.Transports.Count == 0)
        {
            throw new ArgumentException("Нужен хотя бы один транспорт OpenFlux.", nameof(config));
        }

        if (config.Transports.Count > 1 && !config.Negotiate)
        {
            throw new ArgumentException("Несколько транспортов требуют согласованную сессию.", nameof(config));
        }

        if ((config.Negotiate || !string.IsNullOrEmpty(config.Secret)) && Utf16Length(config.Secret) < 16)
        {
            throw new ArgumentException("Секрет OpenFlux должен содержать не менее 16 UTF-16 символов.", nameof(config));
        }

        if (config.Codec is not null and not ("batched" or "legacy"))
        {
            throw new ArgumentException("Неизвестный кодек OpenFlux.", nameof(config));
        }

        foreach (var transport in config.Transports)
        {
            ValidateTransport(transport, config.Negotiate);
        }
    }

    private static void ValidateTransport(OpenFluxTransport transport, bool negotiate)
    {
        if (transport.Type is not ("yandex" or "vyandex" or "boards" or "mailru" or "cupsonline" or "avito" or "direct" or "telemost" or "oneme"))
        {
            throw new ArgumentException($"Неизвестный транспорт OpenFlux: {transport.Type}.");
        }

        if (transport.Type == "direct" && (string.IsNullOrWhiteSpace(transport.Dial) || !negotiate))
        {
            throw new ArgumentException("Direct требует адрес и согласованную сессию.");
        }
    }

    private static int Utf16Length(string? value) => value?.Length ?? 0;

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private sealed record SharePayload(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("negotiate")] bool Negotiate,
        [property: JsonPropertyName("codec")] string? Codec,
        [property: JsonPropertyName("secret")] string? Secret,
        [property: JsonPropertyName("context")] string? Context,
        [property: JsonPropertyName("transports")] TransportPayload[] Transports);

    private sealed record TransportPayload(
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("url")] string? Url,
        [property: JsonPropertyName("priority")] int Priority,
        [property: JsonPropertyName("dial")] string? Dial);
}
