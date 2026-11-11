using System.ComponentModel.DataAnnotations;
using FrogPlug.Core;
using FrogPlug.Server.Data;
using FrogPlug.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace FrogPlug.Tests;

public sealed class NodeAdvancedSettingsTests
{
    private static NodeSpec Spec() => new()
    {
        Address = "203.0.113.5",
        Domain = "vpn.example",
        RealityPublicKey = "pk",
        RealityShortId = "ab12",
        XhttpPath = "/p",
        MaskMode = MaskMode.OwnDomain,
        RealityFingerprint = "firefox",
        XhttpMode = "stream-one"
    };

    private static UserSpec User() => new() { VlessUuid = Guid.Parse("11111111-1111-1111-1111-111111111111") };

    [Fact]
    public void Links_use_chosen_fingerprint_and_xhttp_mode()
    {
        var reality = VlessLinks.Reality(Spec(), User(), "R");
        var xhttp = VlessLinks.Xhttp(Spec(), User(), "X");

        Assert.Equal(
            "vless://11111111-1111-1111-1111-111111111111@203.0.113.5:443?encryption=none&flow=xtls-rprx-vision&security=reality"
            + "&sni=vpn.example&fp=firefox&pbk=pk&sid=ab12&type=tcp#R",
            reality);
        Assert.Equal(
            "vless://11111111-1111-1111-1111-111111111111@203.0.113.5:443?encryption=none&security=reality"
            + "&sni=vpn.example&fp=firefox&pbk=pk&sid=ab12&type=xhttp&path=%2Fp&mode=stream-one#X",
            xhttp);
    }

    [Fact]
    public async Task Unknown_fingerprint_is_rejected()
    {
        await using var fixture = await ServiceFixture.CreateAsync();

        await Assert.ThrowsAsync<ValidationException>(() => fixture.Nodes.Create(
            new NodeInput("node", "example.com", "RU", NodeRole.Entry, RealityFingerprint: "netscape")));
    }

    [Fact]
    public async Task Unknown_xhttp_mode_is_rejected()
    {
        await using var fixture = await ServiceFixture.CreateAsync();

        await Assert.ThrowsAsync<ValidationException>(() => fixture.Nodes.Create(
            new NodeInput("node", "example.com", "RU", NodeRole.Entry, XhttpMode: "turbo")));
    }

    [Fact]
    public async Task Advanced_values_persist_after_reload()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var node = await fixture.Nodes.Create(new NodeInput(
            "node", "example.com", "RU", NodeRole.Entry,
            RealityFingerprint: "safari", XhttpMode: "packet-up", HysteriaObfsEnabled: false));

        fixture.Db.ChangeTracker.Clear();
        var reloaded = await fixture.Db.Nodes.SingleAsync(item => item.Id == node.Id);

        Assert.Equal("safari", reloaded.RealityFingerprint);
        Assert.Equal("packet-up", reloaded.XhttpMode);
        Assert.False(reloaded.HysteriaObfsEnabled);
    }

    [Fact]
    public async Task Regenerate_short_id_keeps_keys()
    {
        await using var fixture = await ServiceFixture.CreateAsync();
        var node = await fixture.Nodes.Create(new NodeInput("node", "example.com", "RU", NodeRole.Entry));
        var (oldShortId, oldKey) = (node.RealityShortId, node.RealityPrivateKey);

        await fixture.Nodes.RegenerateShortId(node.Id);

        Assert.NotEqual(oldShortId, node.RealityShortId);
        Assert.Equal(oldKey, node.RealityPrivateKey);
    }
}
