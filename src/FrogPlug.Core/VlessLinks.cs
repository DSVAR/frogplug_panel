namespace FrogPlug.Core;

public static class VlessLinks
{
    public static string Reality(NodeSpec node, UserSpec user, string remark)
    {
        var sni = Uri.EscapeDataString(RealitySni(node));
        return $"vless://{user.VlessUuid}@{node.Address}:{NodePorts.Public}?encryption=none&flow=xtls-rprx-vision&security=reality"
            + $"&sni={sni}&fp={Uri.EscapeDataString(node.RealityFingerprint)}&pbk={Uri.EscapeDataString(node.RealityPublicKey)}"
            + $"&sid={Uri.EscapeDataString(node.RealityShortId)}&type=tcp#{Uri.EscapeDataString(remark)}";
    }

    // XHTTP идёт поверх Reality: обычное TLS-рукопожатие к домену режет DPI, а Reality проходит.
    public static string Xhttp(NodeSpec node, UserSpec user, string remark)
    {
        var sni = Uri.EscapeDataString(RealitySni(node));
        return $"vless://{user.VlessUuid}@{node.Address}:{NodePorts.Public}?encryption=none&security=reality"
            + $"&sni={sni}&fp={Uri.EscapeDataString(node.RealityFingerprint)}&pbk={Uri.EscapeDataString(node.RealityPublicKey)}"
            + $"&sid={Uri.EscapeDataString(node.RealityShortId)}"
            + $"&type=xhttp&path={Uri.EscapeDataString(node.XhttpPath)}&mode={Uri.EscapeDataString(node.XhttpMode)}#{Uri.EscapeDataString(remark)}";
    }

    private static string RealitySni(NodeSpec node)
    {
        return node.MaskMode == MaskMode.OwnDomain
            ? node.Domain ?? string.Empty
            : node.RealitySni;
    }
}
