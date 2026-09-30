using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace FrogPlug.Server.Services;

public sealed class TelegramNotifier(
    IHttpClientFactory httpClientFactory,
    SettingsService settings,
    ILogger<TelegramNotifier> logger) : ITelegramNotifier
{
    public async Task<bool> Send(string message, CancellationToken cancellationToken = default)
    {
        var token = await settings.GetTelegramBotToken();
        var chatId = await settings.GetTelegramChatId();
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(chatId))
        {
            return false;
        }

        try
        {
            var client = httpClientFactory.CreateClient(nameof(TelegramNotifier));
            var request = new TelegramMessage(chatId, Escape(message), "HTML");
            var response = await client.PostAsJsonAsync(
                $"https://api.telegram.org/bot{token}/sendMessage",
                request,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Telegram не принял уведомление, HTTP {StatusCode}", response.StatusCode);
            }

            return response.IsSuccessStatusCode;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Не удалось отправить Telegram-уведомление");
            return false;
        }
    }

    public static string Escape(string text)
    {
        return text
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal);
    }

    private sealed record TelegramMessage(string ChatId, string Text, string ParseMode);
}
