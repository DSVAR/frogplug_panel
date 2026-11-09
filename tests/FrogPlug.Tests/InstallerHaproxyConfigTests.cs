using FrogPlug.Core;

namespace FrogPlug.Tests;

public sealed class InstallerHaproxyConfigTests
{
    [Fact]
    public void Bootstrap_configuration_matches_builder_without_mtproto()
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../deploy/install.sh"));
        var script = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal);
        const string start = "cat > \"${NODE_CONFIG_DIR}/haproxy/haproxy.cfg\" <<'EOF'\n";
        var configuration = script.Split(start, StringSplitOptions.None)[1].Split("\nEOF", StringSplitOptions.None)[0] + "\n";

        Assert.Equal(HaproxyConfigBuilder.Build(false, null), configuration);
    }
}
