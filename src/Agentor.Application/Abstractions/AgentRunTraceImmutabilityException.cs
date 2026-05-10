namespace Agentor.Application.Abstractions;

/// <summary>
/// Thrown when a persistence save would change an existing trace event, violating append-only audit semantics.
/// </summary>
public sealed class AgentRunTraceImmutabilityException : Exception
{
    public AgentRunTraceImmutabilityException(Guid runId, Guid traceEventId)
        : base("An existing execution trace event cannot be rewritten.")
    {
        RunId = runId;
        TraceEventId = traceEventId;
    }

    public Guid RunId { get; }

    public Guid TraceEventId { get; }
}
