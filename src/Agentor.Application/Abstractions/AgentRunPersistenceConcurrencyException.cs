namespace Agentor.Application.Abstractions;

/// <summary>
/// Thrown when an EF-backed save detects an optimistic concurrency conflict for an agent run aggregate.
/// </summary>
public sealed class AgentRunPersistenceConcurrencyException : Exception
{
    public AgentRunPersistenceConcurrencyException(Guid runId)
        : base("The agent run was modified by another writer. Reload and retry.")
    {
        RunId = runId;
    }

    public Guid RunId { get; }
}
