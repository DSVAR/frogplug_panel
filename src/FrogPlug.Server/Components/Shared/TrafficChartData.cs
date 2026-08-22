using FrogPlug.Server.Services;

namespace FrogPlug.Server.Components.Shared;

public static class TrafficChartData
{
    public const long MinimumScale = 1024L * 1024 * 1024;

    public static IReadOnlyList<ChartPoint> Points(IEnumerable<TrafficHourPoint> values, long maximum, int width, int height)
    {
        var items = values.ToArray();
        if (items.Length == 0)
        {
            return [];
        }

        return items.Select((item, index) => new ChartPoint(
            index * width / (double)Math.Max(items.Length - 1, 1),
            height - item.UpBytes * height / Math.Max(maximum, 1),
            height - item.DownBytes * height / Math.Max(maximum, 1),
            item.Hour)).ToArray();
    }

    public static IReadOnlyList<string> Axis(long maximum)
    {
        return [Format.Bytes(maximum), Format.Bytes(maximum / 2), "0 Б"];
    }

    public static string SmoothPath(
        IEnumerable<(double X, double Y)> points,
        Func<double, string> coordinate)
    {
        var items = points.ToArray();
        if (items.Length == 0)
        {
            return string.Empty;
        }

        var path = $"M {coordinate(items[0].X)} {coordinate(items[0].Y)}";
        for (var index = 1; index < items.Length; index++)
        {
            var previous = items[index - 1];
            var current = items[index];
            var middle = (previous.X + current.X) / 2;
            path += $" C {coordinate(middle)} {coordinate(previous.Y)}"
                + $" {coordinate(middle)} {coordinate(current.Y)}"
                + $" {coordinate(current.X)} {coordinate(current.Y)}";
        }

        return path;
    }

    public static string AreaPath(
        IEnumerable<(double X, double Y)> points,
        double baseline,
        Func<double, string> coordinate)
    {
        var items = points.ToArray();
        if (items.Length == 0)
        {
            return string.Empty;
        }

        var line = SmoothPath(items, coordinate);
        return $"{line} L {coordinate(items[^1].X)} {coordinate(baseline)}"
            + $" L {coordinate(items[0].X)} {coordinate(baseline)} Z";
    }
}

public sealed record ChartPoint(double X, double UpY, double DownY, DateTime Hour);
