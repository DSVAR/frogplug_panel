namespace FrogPlug.Server.Components.Shared;

public static class Format
{
    public static string Bytes(long value)
    {
        if (value < 1024)
        {
            return $"{value} Б";
        }

        var units = new[] { "КБ", "МБ", "ГБ", "ТБ" };
        var number = (double)value;
        var index = -1;
        do
        {
            number /= 1024;
            index++;
        } while (number >= 1024 && index < units.Length - 1);

        return $"{number:0.##} {units[index]}";
    }

    public static string Date(DateTime? utc)
    {
        return utc is null ? "бессрочно" : utc.Value.ToLocalTime().ToString("dd.MM.yyyy");
    }

    public static string DateOrNever(DateTime? utc)
    {
        return utc is null ? "никогда" : utc.Value.ToLocalTime().ToString("dd.MM.yyyy");
    }

    public static string RelativeDays(DateTime? utc, DateTime now)
    {
        if (utc is null)
        {
            return "бессрочно";
        }

        var days = (int)Math.Ceiling((utc.Value - now).TotalDays);
        return days switch
        {
            < 0 => "истёк",
            0 => "сегодня",
            1 => "через 1 день",
            _ => $"через {days} дней"
        };
    }
}
