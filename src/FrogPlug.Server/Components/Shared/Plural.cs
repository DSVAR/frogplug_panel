namespace FrogPlug.Server.Components.Shared;

public static class Plural
{
    public static string Ru(int value, string one, string few, string many)
    {
        var remainder = Math.Abs(value) % 100;
        if (remainder is >= 11 and <= 14)
        {
            return many;
        }

        return (remainder % 10) switch
        {
            1 => one,
            2 or 3 or 4 => few,
            _ => many
        };
    }
}
