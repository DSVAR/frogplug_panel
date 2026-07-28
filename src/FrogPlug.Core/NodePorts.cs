namespace FrogPlug.Core;

public static class NodePorts
{
    public const int Public = 443;
    public const int Reality = 4431;
    public const int Xhttp = 10443;
    public const int Mtproto = 2443;
    public const int TelegramWebMtproxy = 2398;
    public const int TelegramWebMtproxyStats = 2399;
    public const int CaddyTls = 8443;
    public const int Hysteria = 443;
    public const int Api = 10085;
    public const int Socks = 10808;
}

public enum MaskMode
{
    OwnDomain,
    ForeignSite
}
