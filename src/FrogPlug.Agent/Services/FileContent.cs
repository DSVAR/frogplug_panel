using System.Text;

namespace FrogPlug.Agent.Services;

internal static class FileContent
{
    public static async Task<bool> EqualsNormalizedAsync(
        IFileSystem files,
        string path,
        string content,
        CancellationToken cancellationToken)
    {
        if (!files.FileExists(path))
        {
            return false;
        }

        var current = await files.ReadTextAsync(path, cancellationToken);
        return Normalize(current) == Normalize(content);
    }

    private static string Normalize(string value)
    {
        return value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
    }
}
