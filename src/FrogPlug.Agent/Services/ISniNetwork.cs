using System.Net;

namespace FrogPlug.Agent.Services;

public interface ISniNetwork
{
    Task<SniProbeResult> ProbeAsync(IPAddress address, TimeSpan timeout, CancellationToken token);
}