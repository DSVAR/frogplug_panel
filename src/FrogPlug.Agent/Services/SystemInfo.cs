using System.Globalization;

namespace FrogPlug.Agent.Services;

public sealed class SystemInfo
{
    public async Task<SystemSnapshot> ReadAsync(IFileSystem files, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux())
        {
            return new SystemSnapshot(Environment.MachineName, 0, 0, 0, 0, 0);
        }
        var uptime = await ReadUptimeAsync(files, cancellationToken);
        var load = await ReadLoadAsync(files, cancellationToken);
        var memory = await ReadMemoryAsync(files, cancellationToken);
        return new SystemSnapshot(Environment.MachineName, uptime, load, memory.Total, memory.Used, GetDiskFree());
    }

    private static async Task<long> ReadUptimeAsync(IFileSystem files, CancellationToken token)
    {
        var text = await files.ReadTextAsync("/proc/uptime", token);
        return long.TryParse(text.Split(' ')[0].Split('.')[0], out var value) ? value : 0;
    }

    public static long ParseUptime(string text)
    {
        return long.TryParse(text.Split(' ')[0].Split('.')[0], out var value) ? value : 0;
    }

    public static double ParseLoad(string text)
    {
        return double.TryParse(text.Split(' ')[0], CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    public static (long Total, long Used) ParseMemory(string text)
    {
        var values = text.Split('\n').Select(ParseMemoryLine).Where(x => x.Key != null).ToDictionary(x => x.Key!, x => x.Value);
        values.TryGetValue("MemTotal", out var total);
        values.TryGetValue("MemAvailable", out var available);
        return (total, Math.Max(0, total - available));
    }

    private static async Task<double> ReadLoadAsync(IFileSystem files, CancellationToken token)
    {
        var text = await files.ReadTextAsync("/proc/loadavg", token);
        return double.TryParse(text.Split(' ')[0], CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    private static async Task<(long Total, long Used)> ReadMemoryAsync(IFileSystem files, CancellationToken token)
    {
        var lines = await files.ReadTextAsync("/proc/meminfo", token);
        var values = lines.Split('\n').Select(ParseMemoryLine).Where(x => x.Key != null).ToDictionary(x => x.Key!, x => x.Value);
        values.TryGetValue("MemTotal", out var total);
        values.TryGetValue("MemAvailable", out var available);
        return (total, Math.Max(0, total - available));
    }

    private static KeyValuePair<string?, long> ParseMemoryLine(string line)
    {
        var parts = line.Split(':', 2);
        if (parts.Length != 2 || !long.TryParse(parts[1].Trim().Split(' ')[0], out var value))
        {
            return new KeyValuePair<string?, long>(null, 0);
        }
        return new KeyValuePair<string?, long>(parts[0], value * 1024);
    }

    private static long GetDiskFree()
    {
        try
        {
            return new DriveInfo("/").AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }
}

public sealed record SystemSnapshot(string Hostname, long UptimeSeconds, double LoadAverage, long MemoryBytes, long UsedMemoryBytes, long DiskFreeBytes);
