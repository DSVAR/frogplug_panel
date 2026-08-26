using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.RegularExpressions;
using FrogPlug.Core;
using FrogPlug.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Server.Services;

public sealed record NodeInput(
    string Name,
    string Address,
    string Country,
    NodeRole Role,
    string? Domain = null,
    bool IsEnabled = true,
    int RealityPort = 8443,
    int XhttpPort = 443,
    string? AgentUrl = null,
    string? AgentCertSha256 = null,
    string? XrayVersion = null,
    string? Notes = null,
    string RealityServerName = "",
    string RealityDest = "",
    bool RealityEnabled = true,
    bool XhttpEnabled = true,
    MaskMode MaskMode = MaskMode.OwnDomain,
    string? RealitySni = null,
    string? MtprotoHost = null,
    string StubTemplate = StubSiteTemplates.PersonalBlog,
    string? StubTitle = null,
    int? WhitelistExitNodeId = null,
    bool TgWebEnabled = false,
    bool HysteriaEnabled = false,
    int HysteriaPort = NodePorts.Hysteria,
    string RealityFingerprint = "chrome",
    string XhttpMode = "auto",
    bool HysteriaObfsEnabled = true,
    string? XhttpPath = null);

public sealed class NodeService(AppDbContext db, TimeProvider time, AuditService audit, SyncQueue? syncQueue = null)
{
    public Task<List<Node>> List()
    {
        return db.Nodes.OrderBy(item => item.Name).ToListAsync();
    }

    public Task<Node?> Get(int id)
    {
        return db.Nodes.FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<Node> Create(NodeInput input)
    {
        Validate(input);
        await EnsureUniqueName(input.Name, null);
        var keys = RealityKeys.Generate();
        var node = new Node
        {
            Name = input.Name.Trim(),
            Address = input.Address.Trim(),
            Country = input.Country.Trim().ToUpperInvariant(),
            Role = input.Role,
            Domain = input.Domain,
            IsEnabled = input.IsEnabled,
            RealityEnabled = input.RealityEnabled,
            RealityPort = NodePorts.Reality,
            RealityPrivateKey = keys.PrivateKey,
            RealityPublicKey = keys.PublicKey,
            RealityShortId = SecureTokens.RandomHex(4),
            RealityServerName = string.Empty,
            RealityDest = string.Empty,
            MaskMode = input.MaskMode,
            RealitySni = input.MaskMode == MaskMode.OwnDomain
                ? NormalizeHost(input.Domain)
                : NormalizeHost(input.RealitySni),
            RealityFingerprint = input.RealityFingerprint,
            XhttpMode = input.XhttpMode,
            HysteriaObfsEnabled = input.HysteriaObfsEnabled,
            WhitelistExitNodeId = input.WhitelistExitNodeId,
            XhttpPath = string.IsNullOrWhiteSpace(input.XhttpPath)
                ? "/" + SecureTokens.RandomHex(6)
                : input.XhttpPath.Trim(),
            XhttpEnabled = input.XhttpEnabled,
            TgWebEnabled = input.MaskMode == MaskMode.OwnDomain &&
                input.TgWebEnabled &&
                !string.IsNullOrWhiteSpace(input.Domain),
            HysteriaEnabled = input.HysteriaEnabled,
            HysteriaPort = input.HysteriaPort,
            XhttpPort = NodePorts.Xhttp,
            MtprotoPort = NodePorts.Mtproto,
            MtprotoFrontingPort = NodePorts.CaddyTls,
            MtprotoHost = NormalizeHost(input.MtprotoHost),
            StubTemplate = SelectTemplate(input.StubTemplate),
            StubTitle = input.StubTitle?.Trim(),
            CascadeUuid = Guid.NewGuid(),
            AgentUrl = string.Empty,
            AgentToken = SecureTokens.RandomBase64Url(32),
            AgentCertSha256 = null,
            XrayVersion = input.XrayVersion,
            Notes = input.Notes,
            CreatedAt = UtcNow
        };
        if (node.TgWebEnabled && !string.IsNullOrWhiteSpace(node.Domain))
        {
            node.MtprotoEnabled = true;
            node.MtprotoSecret = MtprotoSecret.Generate(node.MtprotoRouteHost ?? node.Domain);
        }
        db.Nodes.Add(node);
        await audit.Write(AuditActions.NodeCreate, node.Name);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
        return node;
    }

    public async Task<Node> Update(int id, NodeInput input)
    {
        Validate(input);
        var node = await Require(id);
        await EnsureUniqueName(input.Name, id);
        node.Name = input.Name.Trim();
        node.Address = input.Address.Trim();
        node.Country = input.Country.Trim().ToUpperInvariant();
        node.Role = input.Role;
        node.Domain = input.Domain;
        node.IsEnabled = input.IsEnabled;
        node.RealityEnabled = input.RealityEnabled;
        node.RealityPort = NodePorts.Reality;
        node.XhttpEnabled = input.XhttpEnabled;
        node.RealityFingerprint = input.RealityFingerprint;
        node.XhttpMode = input.XhttpMode;
        node.HysteriaObfsEnabled = input.HysteriaObfsEnabled;
        if (!string.IsNullOrWhiteSpace(input.XhttpPath))
        {
            node.XhttpPath = input.XhttpPath.Trim();
        }
        node.TgWebEnabled = input.MaskMode == MaskMode.OwnDomain &&
            input.TgWebEnabled &&
            !string.IsNullOrWhiteSpace(input.Domain);
        node.HysteriaEnabled = input.HysteriaEnabled;
        node.HysteriaPort = input.HysteriaPort;
        node.XhttpPort = NodePorts.Xhttp;
        node.XrayVersion = input.XrayVersion;
        node.Notes = input.Notes;
        node.RealityServerName = string.Empty;
        node.RealityDest = string.Empty;
        node.MaskMode = input.MaskMode;
        node.RealitySni = input.MaskMode == MaskMode.OwnDomain
            ? NormalizeHost(input.Domain)
            : NormalizeHost(input.RealitySni);
        node.WhitelistExitNodeId = input.WhitelistExitNodeId;
        node.MtprotoHost = NormalizeHost(input.MtprotoHost);
        if (node.TgWebEnabled && string.IsNullOrWhiteSpace(node.MtprotoSecret) && !string.IsNullOrWhiteSpace(node.Domain))
        {
            node.MtprotoEnabled = true;
            node.MtprotoSecret = MtprotoSecret.Generate(node.MtprotoRouteHost ?? node.Domain);
        }

        // Домен зашит в секрет FakeTLS: при смене поддомена старый секрет перестал бы проходить маршрутизацию.
        if (!string.IsNullOrWhiteSpace(node.MtprotoSecret) && node.MtprotoRouteHost is { } routeHost &&
            !string.Equals(MtprotoSecret.Parse(node.MtprotoSecret), routeHost, StringComparison.OrdinalIgnoreCase))
        {
            node.MtprotoSecret = MtprotoSecret.Generate(routeHost);
        }
        node.StubTemplate = SelectTemplate(input.StubTemplate);
        node.StubTitle = input.StubTitle?.Trim();
        await audit.Write(AuditActions.NodeUpdate, node.Name);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
        return node;
    }

    public async Task Delete(int id)
    {
        var node = await Require(id);
        var target = $"node:{id}";
        var rules = await db.RoutingRules.Where(rule => rule.Target == target).ToListAsync();
        db.RoutingRules.RemoveRange(rules);
        db.Nodes.Remove(node);
        await audit.Write(AuditActions.NodeDelete, node.Name);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
    }

    public async Task RegenerateRealityKeys(int id)
    {
        var node = await Require(id);
        var keys = RealityKeys.Generate();
        node.RealityPrivateKey = keys.PrivateKey;
        node.RealityPublicKey = keys.PublicKey;
        node.RealityShortId = SecureTokens.RandomHex(4);
        await audit.Write(AuditActions.NodeKeys, node.Name);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
    }

    public async Task RegenerateShortId(int id)
    {
        var node = await Require(id);
        node.RealityShortId = SecureTokens.RandomHex(4);
        await audit.Write(AuditActions.NodeKeys, node.Name);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
    }

    public async Task RegenerateAgentToken(int id)
    {
        var node = await Require(id);
        node.AgentToken = SecureTokens.RandomBase64Url(32);
        await audit.Write(AuditActions.NodeToken, node.Name);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
    }

    public const string MtprotoHostRequired =
        "Для MTProto укажите отдельный поддомен (например, tg.ваш-домен) с A-записью на этот сервер";

    public async Task EnableMtproto(int id)
    {
        var node = await Require(id);
        var host = node.MtprotoRouteHost;
        if (host is null)
        {
            throw new ValidationException(MtprotoHostRequired);
        }

        node.MtprotoEnabled = true;
        if (node.MtprotoSecret is null ||
            !string.Equals(MtprotoSecret.Parse(node.MtprotoSecret), host, StringComparison.OrdinalIgnoreCase))
        {
            node.MtprotoSecret = MtprotoSecret.Generate(host);
        }
        await audit.Write(AuditActions.MtprotoEnable, node.Name);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
    }

    public async Task DisableMtproto(int id)
    {
        var node = await Require(id);
        node.MtprotoEnabled = false;
        await audit.Write(AuditActions.MtprotoDisable, node.Name);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
    }

    public async Task RegenerateMtprotoSecret(int id)
    {
        var node = await Require(id);
        var host = node.MtprotoRouteHost;
        if (host is null)
        {
            throw new ValidationException(MtprotoHostRequired);
        }

        node.MtprotoSecret = MtprotoSecret.Generate(host);
        await audit.Write(AuditActions.MtprotoSecret, node.Name);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
    }

    public async Task SetMtprotoPorts(int id, int port, int frontingPort)
    {
        var node = await Require(id);
        node.MtprotoPort = NodePorts.Mtproto;
        node.MtprotoFrontingPort = NodePorts.CaddyTls;
        await audit.Write(AuditActions.MtprotoPorts, node.Name);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
    }

    public Task<List<Node>> ListAsync()
    {
        return List();
    }

    public async Task<Node> CreateAsync(string name, string address, string country, NodeRole role)
    {
        return await Create(new NodeInput(name, address, country, role));
    }

    public async Task RegenerateKeysAsync(Node node)
    {
        await RegenerateRealityKeys(node.Id);
    }

    private async Task<Node> Require(int id)
    {
        return await Get(id) ?? throw new KeyNotFoundException("Нода не найдена");
    }

    private async Task EnsureUniqueName(string name, int? id)
    {
        var normalized = name.Trim().ToUpperInvariant();
        var exists = await db.Nodes.AnyAsync(item =>
            item.Name.ToUpper() == normalized && (id == null || item.Id != id));
        if (exists)
        {
            throw new ValidationException("Нода с таким именем уже существует");
        }
    }

    private static void Validate(NodeInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new ValidationException("Имя ноды обязательно");
        }

        if (!IPAddress.TryParse(input.Address, out _) &&
            !Regex.IsMatch(input.Address, "^(?=.{1,253}$)([A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\\.)*[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?$"))
        {
            throw new ValidationException("Адрес ноды должен быть IP-адресом или именем хоста");
        }

        if (!Regex.IsMatch(input.Country, "^[A-Za-z]{2}$"))
        {
            throw new ValidationException("Страна должна содержать две латинские буквы");
        }

        ValidatePort(input.RealityPort, "RealityPort");
        ValidatePort(input.XhttpPort, "XhttpPort");
        ValidatePort(input.HysteriaPort, "HysteriaPort");
        var fingerprints = new[] { "chrome", "firefox", "safari", "edge", "ios", "android", "random", "randomized" };
        if (!fingerprints.Contains(input.RealityFingerprint, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException("Недопустимый отпечаток TLS");
        }

        var xhttpModes = new[] { "auto", "packet-up", "stream-up", "stream-one" };
        if (!xhttpModes.Contains(input.XhttpMode, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException("Недопустимый режим XHTTP");
        }
        if (input.RealityPort == input.XhttpPort)
        {
            throw new ValidationException("Порты Reality и XHTTP должны различаться");
        }

        if (input.MaskMode == MaskMode.ForeignSite && !IsHostname(input.RealitySni))
        {
            throw new ValidationException("��� ������ ����� ����� ���������� Reality SNI");
        }

        if (!string.IsNullOrWhiteSpace(input.MtprotoHost) && !IsHostname(input.MtprotoHost))
        {
            throw new ValidationException("FakeTLS-����� ������ ���� ������ �����");
        }

        if (input.MaskMode == MaskMode.ForeignSite &&
            string.Equals(NormalizeHost(input.MtprotoHost), NormalizeHost(input.RealitySni), StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("FakeTLS-����� �� ������ ��������� � Reality SNI");
        }

        if (input.MaskMode == MaskMode.ForeignSite &&
            !string.IsNullOrWhiteSpace(input.MtprotoHost) &&
            string.Equals(NormalizeHost(input.MtprotoHost), NormalizeHost(input.Domain), StringComparison.OrdinalIgnoreCase))
        {
            throw new ValidationException("FakeTLS-����� �� ������ ��������� � ������� ����");
        }

        if (input.Role.HasFlag(NodeRole.WhitelistEntry) &&
            (input.MaskMode != MaskMode.ForeignSite || !IsHostname(input.RealitySni)))
        {
            throw new ValidationException("Для входа белых списков нужен чужой сайт с корректным SNI");
        }

        if (input.Role.HasFlag(NodeRole.WhitelistEntry) && input.WhitelistExitNodeId is null)
        {
            throw new ValidationException("Для входа белых списков нужен выходной узел");
        }
    }

    private static void ValidatePort(int port, string name)
    {
        if (port is < 1 or > 65535)
        {
            throw new ValidationException($"{name} должен быть от 1 до 65535");
        }
    }

    private static bool IsHostname(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && Uri.CheckHostName(value.Trim()) == UriHostNameType.Dns;
    }

    private static string? NormalizeHost(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim().TrimEnd('.').ToLowerInvariant();
    }

    private static string SelectTemplate(string template)
    {
        return StubSiteTemplates.All.Contains(template, StringComparer.Ordinal)
            ? template
            : StubSiteTemplates.All[Random.Shared.Next(StubSiteTemplates.All.Length)];
    }

    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;
}
