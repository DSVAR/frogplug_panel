using FrogPlug.Core;
using FrogPlug.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Server.Services;

public sealed class NodeSyncService(
    AppDbContext db,
    IAgentClient agent,
    TimeProvider time,
    OpenFluxService? openFlux = null,
    SettingsService? settings = null,
    ITelegramNotifier? notifier = null)
{
    public Task SyncNode(int nodeId, CancellationToken cancellationToken = default)
    {
        return SyncNode(nodeId, false, cancellationToken);
    }

    public async Task SyncNode(
        int nodeId,
        bool notifyResult,
        CancellationToken cancellationToken = default)
    {
        var node = await db.Nodes.FirstOrDefaultAsync(item => item.Id == nodeId, cancellationToken);
        if (node is null || !node.IsEnabled)
        {
            return;
        }

        try
        {
            var config = await BuildConfig(node, cancellationToken);
            if (node.MaskMode == MaskMode.ForeignSite && string.IsNullOrWhiteSpace(node.RealitySni))
            {
                throw new InvalidOperationException("Выберите сайт для маскировки");
            }

            if (node.MaskMode == MaskMode.OwnDomain && string.IsNullOrWhiteSpace(node.Domain))
            {
                throw new InvalidOperationException("Укажите домен для маскировки");
            }

            // Сначала прокси и сервисы, затем конфигурация Xray.
            await agent.ApplyHaproxy(node, new HaproxyApplyRequest(node.MtprotoEnabled, node.MtprotoRouteHost), cancellationToken);
            if (node.MaskMode == MaskMode.OwnDomain)
            {
                var docsHost = settings is null ? null : await settings.GetDocsDomain();
                var panelDomain = settings is null ? null : await settings.GetPanelDomain();
                docsHost = string.Equals(node.Domain, panelDomain, StringComparison.OrdinalIgnoreCase)
                    ? docsHost
                    : null;
                await agent.ApplyCaddy(
                    node,
                    new CaddyApplyRequest(
                        node.Domain!,
                        node.XhttpPath,
                        XhttpEnabled: node.XhttpEnabled,
                        MtprotoHost: node.MtprotoEnabled ? node.MtprotoRouteHost : null,
                        TgWebEnabled: node.TgWebEnabled,
                        DocsHost: docsHost),
                    cancellationToken);
                await agent.ApplySite(node, new SiteApplyRequest(node.StubTemplate, node.StubTitle, node.CascadeUuid), cancellationToken);
            }

            if (openFlux is not null)
            {
                await ApplyOpenFlux(node, cancellationToken);
            }
            if (settings is not null)
            {
                await ApplyMtproto(node, cancellationToken);
            }
            await ApplyHysteria(node, cancellationToken);
            await ApplyTgWeb(node, cancellationToken);
            await agent.ApplyXrayConfig(node, config, cancellationToken);

            node.LastAppliedAt = UtcNow;
            node.LastApplyError = null;
            if (notifyResult && notifier is not null)
            {
                await notifier.Send($"Настройки применены на ноде {node.Name}", cancellationToken);
            }
        }
        catch (Exception exception)
        {
            node.LastApplyError = exception.Message;
            if (node.LastNotifiedApplyError != exception.Message)
            {
                node.LastNotifiedApplyError = exception.Message;
                if (notifier is not null)
                {
                await notifier.Send($"Ошибка применения на ноде {node.Name}: {exception.Message}", cancellationToken);
                }
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SyncAll(CancellationToken cancellationToken = default)
    {
        var ids = await db.Nodes.Where(item => item.IsEnabled).Select(item => item.Id).ToListAsync(cancellationToken);
        foreach (var id in ids)
        {
            await SyncNode(id, cancellationToken);
        }
    }

    public async Task<string> GetConfig(int nodeId, CancellationToken cancellationToken = default)
    {
        var node = await db.Nodes.FirstOrDefaultAsync(item => item.Id == nodeId, cancellationToken);
        if (node is null)
        {
            throw new KeyNotFoundException("Нода не найдена");
        }

        return await BuildConfig(node, cancellationToken);
    }

    private async Task<string> BuildConfig(Node node, CancellationToken cancellationToken)
    {
        var nodes = await db.Nodes.AsNoTracking().ToListAsync(cancellationToken);
        var users = await db.ClientUsers.AsNoTracking().ToListAsync(cancellationToken);
        var rules = await db.RoutingRules.AsNoTracking().ToListAsync(cancellationToken);
        var self = ToSpec(node);
        var allNodes = nodes.Select(ToSpec).ToArray();
        var activeUsers = users
            .Where(user => ClientUserService.GetStatus(user, UtcNow) == ClientStatus.Active)
            .Select(user => new UserSpec
            {
                Id = user.Id,
                VlessUuid = user.VlessUuid,
                AllowReality = user.AllowReality,
                AllowXhttp = user.AllowXhttp,
                AllowWhitelist = user.AllowWhitelist,
                AllowTgWeb = user.AllowTgWeb,
                AllowHysteria = user.AllowHysteria
            })
            .ToArray();
        var specs = rules.Select(rule => new RuleSpec
        {
            Order = rule.Order,
            Kind = rule.MatchType.ToString(),
            Value = rule.Value,
            Target = rule.Target,
            IsEnabled = rule.IsEnabled
        }).ToArray();
        var exitBalanceStrategy = settings is null
            ? "roundRobin"
            : await settings.GetExitBalanceStrategy();
        return XrayConfigBuilder.Build(self, allNodes, activeUsers, specs, exitBalanceStrategy);
    }

    private async Task ApplyHysteria(Node node, CancellationToken cancellationToken)
    {
        var users = (await db.ClientUsers.AsNoTracking()
            .Where(user => user.AllowHysteria)
            .ToListAsync(cancellationToken))
            .Where(user => ClientUserService.GetStatus(user, UtcNow) == ClientStatus.Active)
            .Select(user => new HysteriaUser(user.Id.ToString(), user.VlessUuid.ToString()))
            .ToArray();
        var enabled = node.HysteriaEnabled;
        var result = await agent.ApplyHysteria(
            node,
            new HysteriaApplyRequest(
                enabled,
                node.Domain ?? string.Empty,
                node.HysteriaPort,
                users,
                node.HysteriaObfsEnabled ? HysteriaLinks.ObfsPassword(node.CascadeUuid) : null,
                node.RealitySni),
            cancellationToken);
        if (result?.Success == true && !string.IsNullOrWhiteSpace(result.PinSha256))
        {
            node.HysteriaPinSha256 = result.PinSha256;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task ApplyOpenFlux(Node node, CancellationToken cancellationToken)
    {
        var channels = await db.OpenFluxChannels.Where(item => item.NodeId == node.Id && item.IsEnabled).ToListAsync(cancellationToken);
        foreach (var channel in channels)
        {
            try
            {
                var request = await openFlux!.ToRequest(channel);
                await agent.ApplyOpenFlux(node, request, cancellationToken);
                channel.LastAppliedAt = UtcNow;
                channel.LastApplyError = null;
            }
            catch (Exception exception)
            {
                channel.LastApplyError = exception.Message;
            }
        }
    }

    private async Task ApplyMtproto(Node node, CancellationToken cancellationToken)
    {
        var version = await settings!.GetMtgVersion();
        var sha256 = await settings.GetMtgSha256();
        if (node.MtprotoEnabled && node.MtprotoRouteHost is not null)
        {
            if (string.IsNullOrWhiteSpace(node.MtprotoSecret))
            {
                throw new InvalidOperationException("Не задан секрет MTProto");
            }

            var request = new MtprotoApplyRequest(
                true,
                node.MtprotoSecret,
                NodePorts.Mtproto,
                NodePorts.CaddyTls,
                version,
                sha256,
                node.MaskMode == MaskMode.OwnDomain ? "127.0.0.1" : node.MtprotoRouteHost);
            await agent.ApplyMtproto(node, request, cancellationToken);
            return;
        }

        var disabled = new MtprotoApplyRequest(
            false,
            null,
            NodePorts.Mtproto,
            NodePorts.CaddyTls,
            version,
            sha256);
        await agent.ApplyMtproto(node, disabled, cancellationToken);
    }

    private async Task ApplyTgWeb(Node node, CancellationToken cancellationToken)
    {
        var enabled = node.MaskMode == MaskMode.OwnDomain && node.TgWebEnabled &&
            !string.IsNullOrWhiteSpace(node.Domain) && !string.IsNullOrWhiteSpace(node.MtprotoSecret);
        await agent.ApplyTgWeb(node, new TgWebApplyRequest(
            enabled,
            node.Domain ?? string.Empty,
            node.MtprotoSecret ?? string.Empty), cancellationToken);
    }

    private static NodeSpec ToSpec(Node node)
    {
        return new NodeSpec
        {
            Id = node.Id,
            Name = node.Name,
            Address = node.Address,
            Country = node.Country,
            Roles = string.Join(',', Enum.GetValues<NodeRole>().Where(role => node.Role.HasFlag(role))),
            IsEnabled = node.IsEnabled,
            Domain = node.Domain,
            RealityEnabled = node.RealityEnabled,
            RealityPort = NodePorts.Reality,
            MaskMode = node.MaskMode,
            RealitySni = node.RealitySni ?? string.Empty,
            RealityFingerprint = node.RealityFingerprint,
            WhitelistExitNodeId = node.WhitelistExitNodeId,
            RealityPrivateKey = node.RealityPrivateKey,
            RealityPublicKey = node.RealityPublicKey,
            RealityShortId = node.RealityShortId,
            RealityServerName = node.RealityServerName,
            RealityDest = node.RealityDest,
            XhttpPath = node.XhttpPath,
            XhttpMode = node.XhttpMode,
            XhttpEnabled = node.XhttpEnabled,
            MtprotoHost = node.MtprotoHost,
            CascadeUuid = node.CascadeUuid
        };
    }

    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;
}
