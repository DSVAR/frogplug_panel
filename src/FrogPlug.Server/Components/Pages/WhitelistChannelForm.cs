using System.ComponentModel.DataAnnotations;
using FrogPlug.Core;
using FrogPlug.Server.Data;
using FrogPlug.Server.Services;

namespace FrogPlug.Server.Components.Pages;

public sealed class ChannelForm
{
    public int NodeId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Port { get; set; } = 8445;

    public bool IsEnabled { get; set; } = true;

    public string Links { get; set; } = string.Empty;

    public string? CookiesJson { get; set; }

    public bool UseXrayUpstream { get; set; } = true;

    public OpenFluxChannelInput ToInput()
    {
        var links = ChannelLinkParser.Parse(Links);
        if (links.Errors.Count > 0)
        {
            throw new ValidationException(string.Join(" ", links.Errors));
        }

        return new OpenFluxChannelInput(
            NodeId,
            Name,
            Port,
            IsEnabled,
            links.VyandexUrl,
            links.YandexUrl,
            links.MailruUrl,
            links.TelemostUrl,
            links.BoardsUrl,
            links.AvitoUrl,
            links.CupsUrl,
            links.MaxToken,
            links.MaxUid,
            CookiesJson,
            UseXrayUpstream);
    }

    public static ChannelForm From(OpenFluxChannel item)
    {
        return new ChannelForm
        {
            NodeId = item.NodeId,
            Name = item.Name,
            Port = item.Port,
            IsEnabled = item.IsEnabled,
            Links = Join(item),
            CookiesJson = item.CookiesJson,
            UseXrayUpstream = item.UseXrayUpstream
        };
    }

    private static string Join(OpenFluxChannel item)
    {
        return string.Join(
            ' ',
            new[]
            {
                item.VyandexUrl,
                item.YandexUrl,
                item.MailruUrl,
                item.TelemostUrl,
                item.BoardsUrl,
                item.AvitoUrl is null ? null : $"avito:{item.AvitoUrl}",
                item.CupsUrl is null ? null : $"cupsonline:{item.CupsUrl}",
                item.MaxToken is null || item.MaxUid is null ? null : $"max:{item.MaxUid}:{item.MaxToken}"
            }.Where(value => value is not null));
    }
}
