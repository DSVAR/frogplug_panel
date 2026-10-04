using FrogPlug.Server.Components.Shared;
using FrogPlug.Server.Services;
using System.Globalization;

namespace FrogPlug.Tests;

public sealed class TrafficChartDataTests
{
    [Fact]
    public void PointsScalesBothTrafficSeriesAndAxisUsesBytes()
    {
        var points = TrafficChartData.Points(
            [new TrafficHourPoint(new DateTime(2026, 1, 1), 50, 100), new TrafficHourPoint(new DateTime(2026, 1, 1, 1, 0, 0), 100, 0)],
            100,
            200,
            100);

        Assert.Equal(50, points[0].UpY);
        Assert.Equal(0, points[0].DownY);
        Assert.Equal(200, points[1].X);
        Assert.Equal("100 Б", TrafficChartData.Axis(100)[0]);
    }

    [Fact]
    public void RelativeDaysFormatsUpcomingAndExpiredDates()
    {
        var now = new DateTime(2026, 1, 1);
        Assert.Equal("через 1 день", Format.RelativeDays(now.AddHours(12), now));
        Assert.Equal("через 2 дней", Format.RelativeDays(now.AddDays(2), now));
        Assert.Equal("истёк", Format.RelativeDays(now.AddDays(-1), now));
    }

    [Fact]
    public void SmoothAndAreaPathsUseCubicBezierAndCloseAtBaseline()
    {
        var points = new[] { (0d, 20d), (10d, 10d), (20d, 15d) };

        var smooth = TrafficChartData.SmoothPath(points, value => value.ToString("0"));
        var area = TrafficChartData.AreaPath(points, 30, value => value.ToString("0"));

        Assert.Equal("M 0 20 C 5 20 5 10 10 10 C 15 10 15 15 20 15", smooth);
        Assert.Equal($"{smooth} L 20 30 L 0 30 Z", area);
    }

    [Fact]
    public void AxisUsesOneGigabyteMinimumScale()
    {
        var axis = TrafficChartData.Axis(TrafficChartData.MinimumScale);

        Assert.Equal("1 ГБ", axis[0]);
        Assert.Equal("512 МБ", axis[1]);
    }

    [Theory]
    [InlineData(0, 10, "M 0 10")]
    [InlineData(1, 5, "M 1 5")]
    [InlineData(0, 0, "M 0 0")]
    [InlineData(12.5, 7.5, "M 12.5 7.5")]
    [InlineData(-2, 4, "M -2 4")]
    [InlineData(100, 200, "M 100 200")]
    public void SmoothPathStartsAtTheFirstPoint(double x, double y, string expected)
    {
        var path = TrafficChartData.SmoothPath(
            [(x, y)],
            value => value.ToString("0.##", CultureInfo.InvariantCulture));

        Assert.Equal(expected, path);
    }

    [Fact]
    public void AxisAlwaysEndsAtZero()
    {
        var axis = TrafficChartData.Axis(4096);

        Assert.Equal("0 Б", axis[^1]);
    }
}
