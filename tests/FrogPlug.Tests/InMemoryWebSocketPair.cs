using System.IO.Pipelines;
using System.Net.WebSockets;

namespace FrogPlug.Tests;

public sealed class InMemoryWebSocketPair : IAsyncDisposable
{
    private readonly DuplexStream panelStream;
    private readonly DuplexStream agentStream;

    public InMemoryWebSocketPair()
    {
        var panelToAgent = new Pipe();
        var agentToPanel = new Pipe();
        panelStream = new DuplexStream(agentToPanel.Reader.AsStream(), panelToAgent.Writer.AsStream());
        agentStream = new DuplexStream(panelToAgent.Reader.AsStream(), agentToPanel.Writer.AsStream());
        Panel = WebSocket.CreateFromStream(panelStream, true, null, TimeSpan.FromSeconds(30));
        Agent = WebSocket.CreateFromStream(agentStream, true, null, TimeSpan.FromSeconds(30));
    }

    public WebSocket Panel { get; }

    public WebSocket Agent { get; }

    public async ValueTask DisposeAsync()
    {
        Panel.Dispose();
        Agent.Dispose();
        await panelStream.DisposeAsync();
        await agentStream.DisposeAsync();
    }
}
