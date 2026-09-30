using System.ComponentModel.DataAnnotations;
using FrogPlug.Core;
using FrogPlug.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Server.Services;

public sealed class SettingsService(AppDbContext db)
{
    public async Task<string?> GetPublicBaseUrl()
    {
        return await Get("PublicBaseUrl");
    }

    public Task<string?> GetPanelDomain()
    {
        return Get("PanelDomain");
    }

    public Task<string> GetProfileTitle()
    {
        return Get("ProfileTitle").ContinueWith(task => task.Result ?? "VPN");
    }

    public Task<string?> GetSupportUrl()
    {
        return Get("SupportUrl");
    }

    public async Task<int> GetSubscriptionUpdateHours()
    {
        var value = await Get("SubscriptionUpdateHours");
        return int.TryParse(value, out var hours) ? Math.Clamp(hours, 1, 168) : 12;
    }

    public async Task<bool> GetDirectRuOnDevice()
    {
        var value = await Get("DirectRuOnDevice");
        return !bool.TryParse(value, out var enabled) || enabled;
    }

    public Task SetDirectRuOnDevice(bool value) => Set("DirectRuOnDevice", value.ToString());

    public async Task<bool> GetHappRoutingEnabled()
    {
        var value = await Get("HappRoutingEnabled");
        return !bool.TryParse(value, out var enabled) || enabled;
    }

    public Task SetHappRoutingEnabled(bool value) => Set("HappRoutingEnabled", value.ToString());

    public Task<string?> GetTelegramBotToken()
    {
        return Get("TelegramBotToken");
    }

    public Task<string?> GetTelegramChatId()
    {
        return Get("TelegramChatId");
    }

    public async Task<string> GetOpenFluxRef()
    {
        return await Get("OpenFluxRef") ?? "v0.17.1";
    }

    public Task<string?> GetOpenFluxInstallerSha256()
    {
        return Get("OpenFluxInstallerSha256");
    }

    public async Task<string> GetMtgVersion()
    {
        return await Get("MtgVersion") ?? "v2.3.0";
    }

    public Task<string?> GetMtgSha256()
    {
        return Get("MtgSha256");
    }

    public Task<string?> GetMtgArm64Sha256()
    {
        return Get("MtgArm64Sha256");
    }

    public Task SetPublicBaseUrl(string? value) => Set("PublicBaseUrl", value);

    public Task SetPanelDomain(string? value) => Set("PanelDomain", value);

    public Task SetProfileTitle(string? value) => Set("ProfileTitle", value);

    public Task SetSupportUrl(string? value) => Set("SupportUrl", value);

    public Task SetTelegramBotToken(string? value) => Set("TelegramBotToken", value);

    public Task SetTelegramChatId(string? value) => Set("TelegramChatId", value);

    public Task SetOpenFluxRef(string? value) => Set("OpenFluxRef", value);

    public Task SetOpenFluxInstallerSha256(string? value) => Set("OpenFluxInstallerSha256", value);

    public Task SetMtgVersion(string? value) => Set("MtgVersion", value);

    public Task SetMtgSha256(string? value) => Set("MtgSha256", value);

    public Task SetMtgArm64Sha256(string? value) => Set("MtgArm64Sha256", value);

    public async Task<string> GetWhitelistSniSuggestions()
    {
        return await Get("WhitelistSniSuggestions") ?? WhitelistSniSuggestions.Default;
    }

    public Task SetWhitelistSniSuggestions(string? value) => Set("WhitelistSniSuggestions", value);

    public async Task SetSubscriptionUpdateHours(int value)
    {
        if (value is < 1 or > 168)
        {
            throw new ValidationException("Интервал обновления должен быть от 1 до 168 часов");
        }

        await Set("SubscriptionUpdateHours", value.ToString());
    }

    private Task<string?> Get(string key)
    {
        return db.Settings.Where(item => item.Key == key).Select(item => item.Value).FirstOrDefaultAsync();
    }

    private async Task Set(string key, string? value)
    {
        var setting = await db.Settings.FindAsync(key);
        if (setting is null)
        {
            db.Settings.Add(new Setting { Key = key, Value = value });
        }
        else
        {
            setting.Value = value;
        }

        await db.SaveChangesAsync();
    }
}

