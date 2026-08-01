namespace FrogPlug.Core;

public static class WhitelistSniSuggestions
{
    public const string Default = "cdn.mos.ru\nstatic.gosuslugi.ru\ncdn.sbermarket.ru\nstatic.ozon.ru";

    public static IReadOnlyList<string> Parse(string? value)
    {
        return (value ?? Default).Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }
}
