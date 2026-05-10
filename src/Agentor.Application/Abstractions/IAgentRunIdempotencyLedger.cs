namespace Agentor.Application.Abstractions;

public sealed record AgentRunIdempotencyEntry(string RequestFingerprint, Guid AgentRunId);

public interface IAgentRunIdempotencyLedger
{
    Task<AgentRunIdempotencyEntry?> GetAsync(string idempotencyKey, CancellationToken cancellationToken);

    Task SaveAsync(
        string idempotencyKey,
        string requestFingerprint,
        Guid agentRunId,
        CancellationToken cancellationToken);
}
