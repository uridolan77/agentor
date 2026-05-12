using Ontogony.Observability;

namespace Agentor.Application.Observability;

/// <summary>
/// Async request correlation id for logs and integration HTTP.
/// Delegates to <see cref="OntogonyCorrelationContext"/> (canonical <c>X-Ontogony-Trace-Id</c>); legacy <c>X-Agentor-Trace-Id</c> is still accepted on ingress via Ontogony middleware.
/// </summary>
public static class AgentorCorrelationContext
{
    public static string? Current => OntogonyCorrelationContext.TraceId;

    public static IDisposable Push(string traceId) => OntogonyCorrelationContext.Push(traceId);
}
