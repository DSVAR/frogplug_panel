using FrogPlug.Server.Services;

namespace FrogPlug.Tests;

public sealed class AuditLabelsTests
{
    [Fact]
    public void Every_action_code_has_a_russian_label()
    {
        foreach (var code in AuditLabels.Codes)
        {
            Assert.False(string.IsNullOrWhiteSpace(AuditLabels.Get(code)));
            Assert.NotEqual(code, AuditLabels.Get(code));
        }
    }

    [Fact]
    public void Every_action_code_has_an_explicit_tone()
    {
        foreach (var code in AuditLabels.Codes)
        {
            Assert.True(AuditLabels.HasExplicitTone(code), code);
        }
    }

    [Theory]
    [InlineData(AuditActions.ClientDelete, AuditTone.Danger)]
    [InlineData(AuditActions.LoginFail, AuditTone.Danger)]
    [InlineData(AuditActions.ClientToken, AuditTone.Warning)]
    [InlineData(AuditActions.LoginOk, AuditTone.Success)]
    [InlineData(AuditActions.SettingsUpdate, AuditTone.Info)]
    public void Known_actions_have_expected_tones(string code, AuditTone expected)
    {
        Assert.Equal(expected, AuditLabels.Tone(code));
    }

    [Fact]
    public void Unknown_code_is_preserved()
    {
        Assert.Equal("unknown", AuditLabels.Get("unknown"));
    }
}
