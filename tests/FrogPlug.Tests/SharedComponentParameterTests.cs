using System.Text.RegularExpressions;

namespace FrogPlug.Tests;

public sealed class SharedComponentParameterTests
{
    // Атрибут, которого нет среди [Parameter] компонента, роняет отрисовку всей интерактивной страницы.
    [Fact]
    public void Shared_components_are_used_only_with_declared_parameters()
    {
        var server = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "FrogPlug.Server"));
        var shared = Path.Combine(server, "Components", "Shared");
        var parameters = Directory.EnumerateFiles(shared, "*.razor").ToDictionary(
            path => Path.GetFileNameWithoutExtension(path),
            path => Regex.Matches(File.ReadAllText(path), @"\[Parameter[^\]]*\]\s*public\s+[^\s]+\s+(\w+)")
                .Select(match => match.Groups[1].Value)
                .ToHashSet(StringComparer.Ordinal));
        var attribute = new Regex(@"\s(@?[\w:-]+)\s*=", RegexOptions.Singleline);
        var offenders = new List<string>();
        foreach (var path in Directory.EnumerateFiles(server, "*.razor", SearchOption.AllDirectories)
                     .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
        {
            var text = File.ReadAllText(path);
            foreach (var (name, known) in parameters)
            {
                foreach (Match usage in Regex.Matches(text, $@"<{name}\b((?:[^>""]|""[^""]*"")*)/?>", RegexOptions.Singleline))
                {
                    foreach (Match attr in attribute.Matches(usage.Groups[1].Value))
                    {
                        var key = attr.Groups[1].Value;
                        if (key.StartsWith('@'))
                        {
                            key = key.StartsWith("@bind-", StringComparison.Ordinal) ? key["@bind-".Length..].Split(':')[0] : string.Empty;
                        }

                        if (key.Length > 0 && !known.Contains(key))
                        {
                            offenders.Add($"{Path.GetFileName(path)}: <{name} {key}>");
                        }
                    }
                }
            }
        }

        Assert.Empty(offenders);
    }
}
