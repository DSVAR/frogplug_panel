using System.ComponentModel.DataAnnotations;
using FrogPlug.Core;
using FrogPlug.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Server.Services;

public sealed record OpenFluxChannelInput(
    int NodeId,
    string Name,
    int Port,
    bool IsEnabled,
    string? VyandexUrl,
    string? YandexUrl,
    string? MailruUrl,
    string? TelemostUrl,
    string? BoardsUrl,
    string? AvitoUrl,
    string? CupsUrl,
    string? MaxToken,
    long? MaxUid,
    string? CookiesJson,
    bool UseXrayUpstream);

public sealed class OpenFluxService(AppDbContext db, IAgentClient agent, SettingsService settings, AuditService audit, TimeProvider time)
{
    public Task<List<OpenFluxChannel>> List()
    {
        return db.OpenFluxChannels.Include(item => item.Node).OrderBy(item => item.Name).ToListAsync();
    }

    public Task<List<OpenFluxChannel>> ListEnabled()
    {
        return db.OpenFluxChannels.Include(item => item.Node).Where(item => item.IsEnabled).ToListAsync();
    }

    public async Task<OpenFluxChannel> Create(OpenFluxChannelInput input)
    {
        Validate(input);
        await RequireNode(input.NodeId);
        await EnsureUnique(input.NodeId, input.Name, null);
        var channel = new OpenFluxChannel { NodeId = input.NodeId, Name = input.Name.Trim(), Port = input.Port, Key = SecureTokens.RandomHex(32) };
        Update(channel, input);
        db.OpenFluxChannels.Add(channel);
        await audit.Write(AuditActions.OpenFluxCreate, channel.Name);
        await db.SaveChangesAsync();
        return channel;
    }

    public async Task Update(int id, OpenFluxChannelInput input)
    {
        Validate(input);
        var channel = await Require(id);
        await RequireNode(input.NodeId);
        await EnsureUnique(input.NodeId, input.Name, id);
        Update(channel, input);
        await audit.Write(AuditActions.OpenFluxUpdate, channel.Name);
        await db.SaveChangesAsync();
    }

    public async Task RotateKey(int id)
    {
        var channel = await Require(id);
        channel.Key = SecureTokens.RandomHex(32);
        await audit.Write(AuditActions.OpenFluxRotateKey, channel.Name);
        await db.SaveChangesAsync();
    }

    public async Task Delete(int id, CancellationToken cancellationToken = default)
    {
        var channel = await Require(id);
        var node = await RequireNode(channel.NodeId);
        await agent.RemoveOpenFlux(node, channel.Name, cancellationToken);

        db.OpenFluxChannels.Remove(channel);
        await audit.Write(AuditActions.OpenFluxDelete, channel.Name);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task Apply(int id, CancellationToken cancellationToken = default)
    {
        var channel = await Require(id);
        var node = await RequireNode(channel.NodeId);
        try
        {
            await agent.ApplyOpenFlux(node, await ToRequest(channel), cancellationToken);
            channel.LastAppliedAt = UtcNow;
            channel.LastApplyError = null;
        }
        catch (Exception exception)
        {
            channel.LastApplyError = exception.Message;
        }

        await audit.Write(AuditActions.OpenFluxApply, channel.Name);
        await db.SaveChangesAsync(cancellationToken);
    }

    public OpenFluxShareConfig GetShareConfig(OpenFluxChannel channel)
    {
        var transports = GetTransports(channel).ToArray();
        var direct = new OpenFluxTransport(
            "direct",
            Dial: $"{channel.Node!.Address}:{channel.Port}");
        var allTransports = new[] { direct }.Concat(transports).ToArray();
        return new OpenFluxShareConfig(
            channel.Name,
            true,
            null,
            channel.Key,
            GetContext(transports),
            allTransports);
    }

    public async Task<OpenFluxApplyRequest> ToRequest(OpenFluxChannel channel)
    {
        return new OpenFluxApplyRequest(channel.Name, channel.Key, channel.Port, channel.VyandexUrl, channel.YandexUrl, channel.MailruUrl,
            channel.TelemostUrl, channel.BoardsUrl, channel.AvitoUrl, channel.CupsUrl, channel.CookiesJson, channel.UseXrayUpstream,
            await settings.GetOpenFluxRef(), await settings.GetOpenFluxInstallerSha256() ?? string.Empty,
            channel.MaxToken, channel.MaxUid);
    }

    public static IReadOnlyList<OpenFluxTransport> GetTransports(OpenFluxChannel channel)
    {
        var transports = new List<OpenFluxTransport>();
        if (channel.VyandexUrl is not null)
        {
            transports.Add(new OpenFluxTransport("vyandex", Url: channel.VyandexUrl, Priority: 50));
        }

        if (channel.YandexUrl is not null)
        {
            transports.Add(new OpenFluxTransport("yandex", Url: channel.YandexUrl, Priority: 50));
        }

        if (channel.MailruUrl is not null)
        {
            transports.Add(new OpenFluxTransport("mailru", Url: channel.MailruUrl, Priority: 50));
        }

        if (channel.TelemostUrl is not null)
        {
            transports.Add(new OpenFluxTransport("telemost", Url: channel.TelemostUrl, Priority: 50));
        }

        if (channel.BoardsUrl is not null)
        {
            transports.Add(new OpenFluxTransport("boards", Url: channel.BoardsUrl, Priority: 50));
        }

        if (channel.AvitoUrl is not null)
        {
            transports.Add(new OpenFluxTransport("avito", Url: channel.AvitoUrl, Priority: 50));
        }

        if (channel.CupsUrl is not null)
        {
            transports.Add(new OpenFluxTransport("cupsonline", Url: channel.CupsUrl, Priority: 50));
        }

        if (channel.MaxToken is not null && channel.MaxUid is not null)
        {
            transports.Add(new OpenFluxTransport("oneme", Priority: 50));
        }

        return transports;
    }

    private static string? GetContext(IReadOnlyList<OpenFluxTransport> transports)
    {
        return transports.FirstOrDefault()?.Url;
    }

    private async Task<OpenFluxChannel> Require(int id)
    {
        return await db.OpenFluxChannels.Include(item => item.Node).FirstOrDefaultAsync(item => item.Id == id)
            ?? throw new KeyNotFoundException("Канал OpenFlux не найден.");
    }

    private async Task<Node> RequireNode(int id)
    {
        return await db.Nodes.FindAsync(id) ?? throw new ValidationException("Нода не найдена.");
    }

    private async Task EnsureUnique(int nodeId, string name, int? id)
    {
        if (await db.OpenFluxChannels.AnyAsync(item => item.NodeId == nodeId && item.Name == name.Trim() && item.Id != id))
        {
            throw new ValidationException("Канал с таким именем уже есть на этой ноде.");
        }
    }

    private static void Update(OpenFluxChannel channel, OpenFluxChannelInput input)
    {
        channel.NodeId = input.NodeId;
        channel.Name = input.Name.Trim();
        channel.Port = input.Port;
        channel.IsEnabled = input.IsEnabled;
        channel.Mode = "l4";
        channel.VyandexUrl = Empty(input.VyandexUrl);
        channel.YandexUrl = Empty(input.YandexUrl);
        channel.MailruUrl = Empty(input.MailruUrl);
        channel.TelemostUrl = Empty(input.TelemostUrl);
        channel.BoardsUrl = Empty(input.BoardsUrl);
        channel.AvitoUrl = Empty(input.AvitoUrl);
        channel.CupsUrl = Empty(input.CupsUrl);
        channel.MaxToken = Empty(input.MaxToken);
        channel.MaxUid = input.MaxUid;
        channel.CookiesJson = Empty(input.CookiesJson);
        channel.UseXrayUpstream = input.UseXrayUpstream;
    }

    private static void Validate(OpenFluxChannelInput input)
    {
        if (!OpenFluxChannelValidator.IsChannel(input.Name) || !OpenFluxChannelValidator.IsPort(input.Port))
        {
            throw new ValidationException("Недопустимы имя канала или порт OpenFlux.");
        }

        if (!IsValidTransportInput(input))
        {
            throw new ValidationException("Недопустимый транспорт OpenFlux.");
        }
    }

    private static bool IsValidTransportInput(OpenFluxChannelInput input)
    {
        return AnyTransport(input)
            && Valid(input.VyandexUrl, OpenFluxChannelValidator.IsVyandexUrl)
            && Valid(input.YandexUrl, OpenFluxChannelValidator.IsYandexUrl)
            && Valid(input.MailruUrl, OpenFluxChannelValidator.IsMailruUrl)
            && Valid(input.TelemostUrl, OpenFluxChannelValidator.IsTelemostUrl)
            && Valid(input.BoardsUrl, OpenFluxChannelValidator.IsBoardsUrl)
            && Valid(input.AvitoUrl, OpenFluxChannelValidator.IsRooms)
            && Valid(input.CupsUrl, OpenFluxChannelValidator.IsRooms)
            && ValidMax(input.MaxToken, input.MaxUid);
    }

    private static bool AnyTransport(OpenFluxChannelInput input)
    {
        var transportUrls = new[]
        {
            input.VyandexUrl,
            input.YandexUrl,
            input.MailruUrl,
            input.TelemostUrl,
            input.BoardsUrl,
            input.AvitoUrl,
            input.CupsUrl,
            input.MaxToken
        };
        return transportUrls.Any(value => !string.IsNullOrWhiteSpace(value))
            || input.MaxUid is not null;
    }

    private static bool ValidMax(string? token, long? uid)
    {
        return string.IsNullOrWhiteSpace(token) && uid is null
            || !string.IsNullOrWhiteSpace(token) && uid is >= 1 and <= 9223372036854775807;
    }

    private static bool Valid(string? value, Func<string, bool> validate) => string.IsNullOrWhiteSpace(value) || validate(value);

    private static string? Empty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;
}
