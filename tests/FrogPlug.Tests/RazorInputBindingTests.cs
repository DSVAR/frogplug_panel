using System.Text.RegularExpressions;

namespace FrogPlug.Tests;

public sealed class RazorInputBindingTests
{
    // InputText/InputNumber/InputSelect без @bind-Value (или ValueExpression) падают при отрисовке
    // и роняют всю интерактивную страницу — кнопки перестают реагировать.
    [Fact]
    public void Blazor_inputs_always_have_binding()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "FrogPlug.Server"));
        var tag = new Regex(@"<(InputText|InputNumber|InputSelect|InputCheckbox|InputTextArea|InputDate)\b[^>]*>", RegexOptions.Singleline);
        var offenders = Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .SelectMany(path => tag.Matches(File.ReadAllText(path))
                .Where(match => !match.Value.Contains("@bind-Value") && !match.Value.Contains("ValueExpression"))
                .Select(match => $"{Path.GetFileName(path)}: {match.Value[..Math.Min(80, match.Value.Length)]}"))
            .ToList();

        Assert.Empty(offenders);
    }
}
