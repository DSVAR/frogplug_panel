using FrogPlug.Core;

namespace FrogPlug.Agent.Services;

public sealed record SniProbeResult(SniCandidateInput? Candidate, string? RejectionReason)
{
    public static SniProbeResult Rejected(string reason)
    {
        return new SniProbeResult(null, reason);
    }
}