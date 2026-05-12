using Ontogony.Contracts.Events;
using Ontogony.Observability;

namespace Agentor.Infrastructure.Http;

/// <summary>
/// Forwards the active Ontogony trace id as <c>X-Agentor-Trace-Id</c> on outbound integration calls when not already set,
/// so Athanor/Conexus paths that still key off the legacy header keep correlating during rollout.
/// </summary>
internal sealed class AgentorLegacyTraceHeaderHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var id = OntogonyCorrelationContext.TraceId;
        if (!string.IsNullOrWhiteSpace(id)
            && !request.Headers.Contains(OntogonyEventHeaders.LegacyAgentorTraceId))
        {
            request.Headers.TryAddWithoutValidation(OntogonyEventHeaders.LegacyAgentorTraceId, id);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
