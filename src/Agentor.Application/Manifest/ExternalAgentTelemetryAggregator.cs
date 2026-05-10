using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Manifest;

public static class ExternalAgentTelemetryAggregator
{
    public static RunManifestExternalAgentTelemetry Aggregate(AgentRun run)
    {
        var n = run.Trace.Count(e => e.Kind == TraceEventKind.ExternalAgentInvocationCompleted);
        return new RunManifestExternalAgentTelemetry(n);
    }
}
