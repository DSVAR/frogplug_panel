using System.ComponentModel.DataAnnotations;
using FrogPlug.Core;
using FrogPlug.Server.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Server.Services;

public static class AuditActions
{
    public const string LoginOk = "login ok";
    public const string LoginFail = "login fail";
    public const string Logout = "logout";
    public const string AdminCreate = "admin create";
    public const string AdminDeactivate = "admin deactivate";
    public const string PasswordChange = "password change";
    public const string TotpEnable = "totp enable";
    public const string TotpDisable = "totp disable";
    public const string TotpFail = "totp fail";
    public const string AdminReset = "admin reset";
    public const string ClientCreate = "client.create";
    public const string ClientUpdate = "client.update";
    public const string ClientDelete = "client.delete";
    public const string ClientToggle = "client.toggle";
    public const string ClientToken = "client.token";
    public const string ClientUuid = "client.uuid";
    public const string ClientTrafficReset = "client.traffic.reset";
    public const string ClientExpiry = "client.expiry";
    public const string NodeCreate = "node.create";
    public const string NodeUpdate = "node.update";
    public const string NodeDelete = "node.delete";
    public const string NodeKeys = "node.keys";
    public const string NodeToken = "node.token";
    public const string NodeAgentUrl = "node.agent.url";
    public const string NodeFingerprintTrust = "node.fingerprint.trust";
    public const string NodeFingerprintReset = "node.fingerprint.reset";
    public const string RoutingCreate = "routing.create";
    public const string RoutingUpdate = "routing.update";
    public const string RoutingDelete = "routing.delete";
    public const string RoutingMove = "routing.move";
    public const string RoutingToggle = "routing.toggle";
    public const string SettingsUpdate = "settings.update";
    public const string BackupDownload = "backup.download";
    public const string OpenFluxCreate = "openflux.create";
    public const string OpenFluxUpdate = "openflux.update";
    public const string OpenFluxDelete = "openflux.delete";
    public const string OpenFluxRotateKey = "openflux.rotate_key";
    public const string OpenFluxApply = "openflux.apply";
    public const string MtprotoEnable = "mtproto.enable";
    public const string MtprotoDisable = "mtproto.disable";
    public const string MtprotoSecret = "mtproto.secret";
    public const string MtprotoPorts = "mtproto.ports";
}

public static class AuditLabels
{
    private static readonly Dictionary<string, string> Labels = new()
    {
        [AuditActions.LoginOk] = "Вход",
        [AuditActions.LoginFail] = "Неудачный вход",
        [AuditActions.Logout] = "Выход",
        [AuditActions.AdminCreate] = "Создан администратор",
        [AuditActions.AdminDeactivate] = "Администратор отключён",
        [AuditActions.PasswordChange] = "Смена пароля",
        [AuditActions.ClientCreate] = "Создан пользователь",
        [AuditActions.ClientUpdate] = "Изменён пользователь",
        [AuditActions.ClientDelete] = "Удалён пользователь",
        [AuditActions.ClientToggle] = "Изменён статус пользователя",
        [AuditActions.ClientToken] = "Перевыпущен токен пользователя",
        [AuditActions.ClientUuid] = "Перевыпущен UUID пользователя",
        [AuditActions.ClientTrafficReset] = "Сброшен трафик пользователя",
        [AuditActions.ClientExpiry] = "Продлён срок пользователя",
        [AuditActions.NodeCreate] = "Создана нода",
        [AuditActions.NodeUpdate] = "Изменена нода",
        [AuditActions.NodeDelete] = "Удалена нода",
        [AuditActions.NodeKeys] = "Перевыпущены ключи Reality",
        [AuditActions.NodeToken] = "Перевыпущен токен агента",
        [AuditActions.NodeAgentUrl] = "Изменён URL агента",
        [AuditActions.NodeFingerprintTrust] = "Подтверждён отпечаток агента",
        [AuditActions.NodeFingerprintReset] = "Сброшен отпечаток агента",
        [AuditActions.RoutingCreate] = "Создано правило маршрутизации",
        [AuditActions.RoutingUpdate] = "Изменено правило маршрутизации",
        [AuditActions.RoutingDelete] = "Удалено правило маршрутизации",
        [AuditActions.RoutingMove] = "Изменён порядок правил",
        [AuditActions.RoutingToggle] = "Изменён статус правила",
        [AuditActions.SettingsUpdate] = "Изменены настройки"
    };

    private static readonly Dictionary<string, AuditTone> Tones = new()
    {
        [AuditActions.LoginOk] = AuditTone.Success,
        [AuditActions.LoginFail] = AuditTone.Danger,
        [AuditActions.Logout] = AuditTone.Neutral,
        [AuditActions.AdminCreate] = AuditTone.Success,
        [AuditActions.AdminDeactivate] = AuditTone.Danger,
        [AuditActions.PasswordChange] = AuditTone.Warning,
        [AuditActions.TotpEnable] = AuditTone.Success,
        [AuditActions.TotpDisable] = AuditTone.Warning,
        [AuditActions.TotpFail] = AuditTone.Danger,
        [AuditActions.AdminReset] = AuditTone.Warning,
        [AuditActions.ClientCreate] = AuditTone.Success,
        [AuditActions.ClientUpdate] = AuditTone.Info,
        [AuditActions.ClientDelete] = AuditTone.Danger,
        [AuditActions.ClientToggle] = AuditTone.Info,
        [AuditActions.ClientToken] = AuditTone.Warning,
        [AuditActions.ClientUuid] = AuditTone.Warning,
        [AuditActions.ClientTrafficReset] = AuditTone.Warning,
        [AuditActions.ClientExpiry] = AuditTone.Info,
        [AuditActions.NodeCreate] = AuditTone.Success,
        [AuditActions.NodeUpdate] = AuditTone.Info,
        [AuditActions.NodeDelete] = AuditTone.Danger,
        [AuditActions.NodeKeys] = AuditTone.Warning,
        [AuditActions.NodeToken] = AuditTone.Warning,
        [AuditActions.NodeAgentUrl] = AuditTone.Info,
        [AuditActions.NodeFingerprintTrust] = AuditTone.Info,
        [AuditActions.NodeFingerprintReset] = AuditTone.Warning,
        [AuditActions.RoutingCreate] = AuditTone.Success,
        [AuditActions.RoutingUpdate] = AuditTone.Info,
        [AuditActions.RoutingDelete] = AuditTone.Danger,
        [AuditActions.RoutingMove] = AuditTone.Info,
        [AuditActions.RoutingToggle] = AuditTone.Info,
        [AuditActions.SettingsUpdate] = AuditTone.Info,
        [AuditActions.BackupDownload] = AuditTone.Neutral,
        [AuditActions.OpenFluxCreate] = AuditTone.Success,
        [AuditActions.OpenFluxUpdate] = AuditTone.Info,
        [AuditActions.OpenFluxDelete] = AuditTone.Danger,
        [AuditActions.OpenFluxRotateKey] = AuditTone.Warning,
        [AuditActions.OpenFluxApply] = AuditTone.Info,
        [AuditActions.MtprotoEnable] = AuditTone.Success,
        [AuditActions.MtprotoDisable] = AuditTone.Warning,
        [AuditActions.MtprotoSecret] = AuditTone.Warning,
        [AuditActions.MtprotoPorts] = AuditTone.Info
    };

    static AuditLabels()
    {
        Labels[AuditActions.TotpEnable] = "Включена двухфакторная аутентификация";
        Labels[AuditActions.TotpDisable] = "Отключена двухфакторная аутентификация";
        Labels[AuditActions.TotpFail] = "Неверный код двухфакторной аутентификации";
        Labels[AuditActions.AdminReset] = "Сброшен доступ администратора";
        AddMtprotoLabels();
        Labels[AuditActions.OpenFluxCreate] = "Создан канал OpenFlux";
        Labels[AuditActions.OpenFluxUpdate] = "Изменён канал OpenFlux";
        Labels[AuditActions.OpenFluxDelete] = "Удалён канал OpenFlux";
        Labels[AuditActions.OpenFluxRotateKey] = "Сменён ключ OpenFlux";
        Labels[AuditActions.OpenFluxApply] = "Применён канал OpenFlux";
        Labels[AuditActions.BackupDownload] = "Скачана резервная копия базы";
    }

    private static void AddMtprotoLabels()
    {
        Labels[AuditActions.MtprotoEnable] = "Включён MTProto";
        Labels[AuditActions.MtprotoDisable] = "Отключён MTProto";
        Labels[AuditActions.MtprotoSecret] = "Обновлён секрет MTProto";
        Labels[AuditActions.MtprotoPorts] = "Изменены порты MTProto";
    }

    public static string Get(string code)
    {
        return Labels.TryGetValue(code, out var label) ? label : code;
    }

    public static AuditTone Tone(string code)
    {
        return Tones.TryGetValue(code, out var tone) ? tone : AuditTone.Neutral;
    }

    public static bool HasExplicitTone(string code)
    {
        return Tones.ContainsKey(code);
    }

    public static string CssClass(string code)
    {
        return Tone(code) switch
        {
            AuditTone.Danger => "chip-danger",
            AuditTone.Warning => "chip-warning",
            AuditTone.Success => "chip-success",
            AuditTone.Info => "chip-info",
            _ => string.Empty
        };
    }

    public static IReadOnlyCollection<string> Codes => Labels.Keys.ToArray();
}

public enum AuditTone
{
    Neutral,
    Danger,
    Warning,
    Success,
    Info
}

public sealed class AuditService(AppDbContext db, IHttpContextAccessor httpContextAccessor)
{
    public async Task Write(string action, string? details = null, string? loginOverride = null)
    {
        var login = loginOverride ?? httpContextAccessor.HttpContext?.User.Identity?.Name;
        db.AuditLogs.Add(new AuditLog
        {
            At = DateTime.UtcNow,
            AdminLogin = string.IsNullOrWhiteSpace(login) ? "system" : login,
            Action = action,
            Details = details
        });
        await db.SaveChangesAsync();
    }
}

public enum ClientStatus
{
    Active,
    Disabled,
    Expired,
    OverLimit
}

public sealed record ClientUserInput(
    string Name,
    string? Note,
    bool IsEnabled = true,
    DateTime? ExpiresAt = null,
    long? TrafficLimitBytes = null,
    bool AllowReality = true,
    bool AllowXhttp = true,
    bool AllowWhitelist = false,
    bool AllowMtproto = false,
    bool AllowTgWeb = false,
    bool AllowHysteria = false);

public sealed class ClientUserService(AppDbContext db, TimeProvider time, AuditService audit, SyncQueue? syncQueue = null)
{
    public async Task<List<ClientUser>> List(string? search = null, ClientStatus? status = null)
    {
        var users = await db.ClientUsers.OrderBy(item => item.Name).ToListAsync();
        var now = UtcNow;
        return users
            .Where(item => string.IsNullOrWhiteSpace(search) || item.Name.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Where(item => status is null || GetStatus(item, now) == status)
            .ToList();
    }

    public Task<ClientUser?> Get(int id)
    {
        return db.ClientUsers.FirstOrDefaultAsync(item => item.Id == id);
    }

    public async Task<ClientUser> Create(ClientUserInput input)
    {
        Validate(input);
        await EnsureUniqueName(input.Name, null);
        var user = new ClientUser
        {
            Name = input.Name.Trim(),
            Note = input.Note,
            IsEnabled = input.IsEnabled,
            ExpiresAt = input.ExpiresAt,
            TrafficLimitBytes = input.TrafficLimitBytes,
            AllowReality = input.AllowReality,
            AllowXhttp = input.AllowXhttp,
            AllowWhitelist = input.AllowWhitelist,
            AllowMtproto = input.AllowMtproto,
            AllowTgWeb = input.AllowTgWeb,
            AllowHysteria = input.AllowHysteria,
            VlessUuid = Guid.NewGuid(),
            SubscriptionToken = SecureTokens.RandomBase64Url(32),
            CreatedAt = UtcNow
        };
        db.ClientUsers.Add(user);
        await audit.Write(AuditActions.ClientCreate, $"{user.Name}; {ProtocolDetails(user)}");
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
        return user;
    }

    public async Task<ClientUser> Update(int id, ClientUserInput input)
    {
        Validate(input);
        var user = await Require(id);
        await EnsureUniqueName(input.Name, id);
        user.Name = input.Name.Trim();
        user.Note = input.Note;
        user.IsEnabled = input.IsEnabled;
        user.ExpiresAt = input.ExpiresAt;
        user.TrafficLimitBytes = input.TrafficLimitBytes;
        user.AllowReality = input.AllowReality;
        user.AllowXhttp = input.AllowXhttp;
        user.AllowWhitelist = input.AllowWhitelist;
        user.AllowMtproto = input.AllowMtproto;
        user.AllowTgWeb = input.AllowTgWeb;
        user.AllowHysteria = input.AllowHysteria;
        await audit.Write(AuditActions.ClientUpdate, $"{user.Name}; {ProtocolDetails(user)}");
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
        return user;
    }

    public async Task Delete(int id)
    {
        var user = await Require(id);
        db.ClientUsers.Remove(user);
        await audit.Write(AuditActions.ClientDelete, user.Name);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
    }

    public async Task ToggleEnabled(int id)
    {
        var user = await Require(id);
        user.IsEnabled = !user.IsEnabled;
        await audit.Write(AuditActions.ClientToggle, user.Name);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
    }

    public async Task RegenerateToken(int id)
    {
        var user = await Require(id);
        user.SubscriptionToken = SecureTokens.RandomBase64Url(32);
        await audit.Write(AuditActions.ClientToken, user.Name);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
    }

    public async Task RegenerateUuid(int id)
    {
        var user = await Require(id);
        user.VlessUuid = Guid.NewGuid();
        await audit.Write(AuditActions.ClientUuid, user.Name);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
    }

    public async Task ResetTraffic(int id)
    {
        var user = await Require(id);
        user.UsedUploadBytes = 0;
        user.UsedDownloadBytes = 0;
        await audit.Write(AuditActions.ClientTrafficReset, user.Name);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
    }

    public async Task ExtendDays(int id, int days)
    {
        if (days <= 0)
        {
            throw new ValidationException("Количество дней должно быть положительным");
        }

        var user = await Require(id);
        var start = user.ExpiresAt.GetValueOrDefault(UtcNow);
        if (start < UtcNow)
        {
            start = UtcNow;
        }

        user.ExpiresAt = start.AddDays(days);
        await audit.Write(AuditActions.ClientExpiry, user.Name);
        await db.SaveChangesAsync();
        syncQueue?.RequestAll();
    }

    public static ClientStatus GetStatus(ClientUser user, DateTime nowUtc)
    {
        if (!user.IsEnabled)
        {
            return ClientStatus.Disabled;
        }

        if (user.ExpiresAt is not null && user.ExpiresAt <= nowUtc)
        {
            return ClientStatus.Expired;
        }

        if (user.TrafficLimitBytes is not null && user.UsedUploadBytes + user.UsedDownloadBytes >= user.TrafficLimitBytes)
        {
            return ClientStatus.OverLimit;
        }

        return ClientStatus.Active;
    }

    public Task<List<ClientUser>> ListAsync(string? search = null)
    {
        return List(search);
    }

    public async Task<ClientUser> CreateAsync(string name, string? note = null)
    {
        return await Create(new ClientUserInput(name, note));
    }

    public async Task ToggleAsync(ClientUser user)
    {
        await ToggleEnabled(user.Id);
    }

    public async Task RegenerateTokenAsync(ClientUser user)
    {
        await RegenerateToken(user.Id);
    }

    public async Task RegenerateUuidAsync(ClientUser user)
    {
        await RegenerateUuid(user.Id);
    }

    public async Task ResetTrafficAsync(ClientUser user)
    {
        await ResetTraffic(user.Id);
    }

    public async Task ExtendAsync(ClientUser user, int days)
    {
        await ExtendDays(user.Id, days);
    }

    private async Task<ClientUser> Require(int id)
    {
        return await Get(id) ?? throw new KeyNotFoundException("Пользователь не найден");
    }

    private async Task EnsureUniqueName(string name, int? id)
    {
        var normalized = name.Trim().ToUpperInvariant();
        var exists = await db.ClientUsers.AnyAsync(item =>
            item.Name.ToUpper() == normalized && (id == null || item.Id != id));
        if (exists)
        {
            throw new ValidationException("Пользователь с таким именем уже существует");
        }
    }

    private static void Validate(ClientUserInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 64)
        {
            throw new ValidationException("Имя должно содержать от 1 до 64 символов");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(input.Name.Trim(), "^[A-Za-z0-9А-Яа-яЁё _.-]+$"))
        {
            throw new ValidationException("Имя содержит недопустимые символы");
        }

        if (input.TrafficLimitBytes is < 0)
        {
            throw new ValidationException("Лимит трафика не может быть отрицательным");
        }

        if (!input.AllowReality && !input.AllowXhttp && !input.AllowWhitelist && !input.AllowMtproto && !input.AllowTgWeb && !input.AllowHysteria)
        {
            throw new ValidationException("Выберите хотя бы один протокол");
        }
    }

    private static string ProtocolDetails(ClientUser user)
    {
        var protocols = new List<string>();
        if (user.AllowReality)
        {
            protocols.Add("Reality");
        }

        if (user.AllowXhttp)
        {
            protocols.Add("XHTTP");
        }

        if (user.AllowWhitelist)
        {
            protocols.Add("OpenFlux");
        }

        if (user.AllowMtproto)
        {
            protocols.Add("MTProto");
        }

        if (user.AllowTgWeb)
        {
            protocols.Add("Telegram Web");
        }
        if (user.AllowHysteria)
        {
            protocols.Add("Hysteria2");
        }

        return $"протоколы: {string.Join(", ", protocols)}";
    }

    private DateTime UtcNow => time.GetUtcNow().UtcDateTime;
}



