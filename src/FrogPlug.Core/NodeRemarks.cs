namespace FrogPlug.Core;

public static class NodeRemarks
{
    public static string Reality(NodeSpec node) => Reality(node, string.Empty, false);

    public static string Reality(NodeSpec node, string userName, bool includeNode)
    {
        return Build(node, userName, "Reality", includeNode);
    }

    public static string Xhttp(NodeSpec node) => Xhttp(node, string.Empty, false);

    public static string Xhttp(NodeSpec node, string userName, bool includeNode)
    {
        return Build(node, userName, "XHTTP", includeNode);
    }

    public static string Hysteria(NodeSpec node, string userName, bool includeNode)
    {
        return Build(node, userName, "Hysteria2", includeNode);
    }

    public static string Mtproto(NodeSpec node, string userName, bool includeNode)
    {
        return Build(node, userName, "MTProto", includeNode);
    }

    private static string Build(NodeSpec node, string userName, string protocol, bool includeNode)
    {
        var suffix = includeNode ? $" · {node.Name}" : string.Empty;
        return $"{Flag(node.Country)} {userName}{suffix} · {protocol}";
    }

    private static string Flag(string country)
    {
        if (country.Length != 2)
        {
            return "🌐";
        }

        return string.Concat(country.ToUpperInvariant().Select(letter =>
            char.ConvertFromUtf32(letter - 'A' + 0x1F1E6)));
    }
}
