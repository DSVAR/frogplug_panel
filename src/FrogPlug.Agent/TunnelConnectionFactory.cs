using System.Net.WebSockets;

namespace FrogPlug.Agent;

public delegate Task<WebSocket> TunnelConnectionFactory(
    Uri uri,
    string token,
    bool allowInsecureCertificate,
    CancellationToken cancellationToken);
