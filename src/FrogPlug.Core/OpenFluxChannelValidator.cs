using System.Text.RegularExpressions;

namespace FrogPlug.Core;

/// <summary>Проверки строк конфигурации OpenFlux node-install.sh v0.17.1.</summary>
public static partial class OpenFluxChannelValidator
{
    public static bool IsChannel(string value) => ChannelRegex().IsMatch(value);

    public static bool IsKey(string value) => KeyRegex().IsMatch(value);

    public static bool IsVyandexUrl(string value) => VyandexRegex().IsMatch(value);

    public static bool IsYandexUrl(string value) => YandexRegex().IsMatch(value);

    public static bool IsBoardsUrl(string value) => BoardsRegex().IsMatch(value);

    public static bool IsMailruUrl(string value) => MailruRegex().IsMatch(value);

    public static bool IsTelemostUrl(string value) => TelemostRegex().IsMatch(value);

    public static bool IsMaxLink(string value) => MaxRegex().IsMatch(value);

    public static bool TryParseMaxLink(string value, out string uid, out string token)
    {
        var match = MaxRegex().Match(value);
        uid = match.Success ? match.Groups["uid"].Value : string.Empty;
        token = match.Success ? match.Groups["token"].Value : string.Empty;
        return match.Success;
    }

    public static bool IsRooms(string value) => value.Length is >= 8 and <= 4096 && RoomsRegex().IsMatch(value);

    public static bool IsPort(int value) => value is >= 1024 and <= 65535;

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,30}$", RegexOptions.CultureInvariant)]
    private static partial Regex ChannelRegex();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex KeyRegex();

    [GeneratedRegex("^https://(docs|disk)\\.yandex\\.(ru|com|by|kz|uz)/edit/d/[A-Za-z0-9_-]{16,200}$", RegexOptions.CultureInvariant)]
    private static partial Regex VyandexRegex();

    [GeneratedRegex("^https://(docs|disk)\\.yandex\\.(ru|com|by|kz|uz)/(edit/d/[A-Za-z0-9_-]{16,200}|i/[A-Za-z0-9_-]{6,64})$", RegexOptions.CultureInvariant)]
    private static partial Regex YandexRegex();

    [GeneratedRegex("^https://boards\\.yandex\\.ru/whiteboard/\\?hash=[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex BoardsRegex();

    [GeneratedRegex("^https://cloud\\.mail\\.ru/public/[A-Za-z0-9_-]{2,64}/[A-Za-z0-9_-]{2,128}$", RegexOptions.CultureInvariant)]
    private static partial Regex MailruRegex();

    [GeneratedRegex("^https://telemost\\.yandex\\.ru/j/[0-9]{6,32}$", RegexOptions.CultureInvariant)]
    private static partial Regex TelemostRegex();

    [GeneratedRegex("^max:(?<uid>[1-9][0-9]{0,18}):(?<token>[^\\s]{1,4096})$", RegexOptions.CultureInvariant)]
    private static partial Regex MaxRegex();

    [GeneratedRegex("^[A-Za-z0-9_-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex RoomsRegex();
}
