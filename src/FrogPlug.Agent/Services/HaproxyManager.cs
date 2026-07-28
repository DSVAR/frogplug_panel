using FrogPlug.Core;

namespace FrogPlug.Agent.Services;

public sealed class HaproxyManager(
    ICommandRunner runner,
    IFileSystem files,
    IServiceController services)
{
    private const string ConfigPath = "/etc/haproxy/haproxy.cfg";

    public HaproxyManager(ICommandRunner runner, IFileSystem files)
        : this(runner, files, new SystemdServiceController(runner))
    {
    }

    public async Task<(bool Success, string Error)> ApplyAsync(
        HaproxyApplyRequest request,
        CancellationToken cancellationToken)
    {
        var content = HaproxyConfigBuilder.Build(request.MtprotoEnabled, request.MtprotoHost);
        if (await FileContent.EqualsNormalizedAsync(files, ConfigPath, content, cancellationToken))
        {
            return (true, string.Empty);
        }

        var temporary = ConfigPath + ".new";
        var backup = ConfigPath + ".bak";
        await files.WriteTextAsync(temporary, content, cancellationToken);
        var validation = await runner.RunAsync(
            "haproxy",
            ["-c", "-f", temporary],
            TimeSpan.FromSeconds(20),
            cancellationToken);
        if (validation.ExitCode != 0)
        {
            files.DeleteFile(temporary);
            return (false, validation.StandardError);
        }

        if (files.FileExists(ConfigPath))
        {
            files.CopyFile(ConfigPath, backup, true);
        }

        files.MoveFile(temporary, ConfigPath, true);
        var reload = await services.SignalAsync("haproxy", "USR2", cancellationToken);
        if (reload.ExitCode == 0)
        {
            return (true, string.Empty);
        }

        if (files.FileExists(backup))
        {
            files.CopyFile(backup, ConfigPath, true);
            await services.SignalAsync("haproxy", "USR2", cancellationToken);
        }

        return (false, reload.StandardError);
    }
}
