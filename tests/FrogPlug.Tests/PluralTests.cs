using FrogPlug.Server.Components.Shared;

namespace FrogPlug.Tests;

public sealed class PluralTests
{
    [Theory]
    [InlineData(1, "нода")]
    [InlineData(2, "ноды")]
    [InlineData(5, "нод")]
    [InlineData(11, "нод")]
    [InlineData(21, "нода")]
    [InlineData(24, "ноды")]
    [InlineData(0, "нод")]
    [InlineData(-1, "нода")]
    [InlineData(-2, "ноды")]
    [InlineData(-5, "нод")]
    [InlineData(10, "нод")]
    [InlineData(12, "нод")]
    [InlineData(14, "нод")]
    [InlineData(15, "нод")]
    [InlineData(20, "нод")]
    [InlineData(22, "ноды")]
    [InlineData(25, "нод")]
    [InlineData(101, "нода")]
    [InlineData(102, "ноды")]
    [InlineData(105, "нод")]
    [InlineData(111, "нод")]
    [InlineData(112, "нод")]
    [InlineData(115, "нод")]
    [InlineData(1000, "нод")]
    [InlineData(1001, "нода")]
    [InlineData(1002, "ноды")]
    public void RuUsesRussianPluralForms(int value, string expected)
    {
        var actual = Plural.Ru(value, "нода", "ноды", "нод");

        Assert.Equal(expected, actual);
    }
}
