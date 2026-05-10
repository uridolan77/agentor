using System.Collections.Concurrent;
using Agentor.Application.Abstractions;
using Agentor.Domain;

namespace Agentor.Application.Services;

public sealed class AgentRunIdempotencyService
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> KeyLocks = new(StringComparer.Ordinal);

    private readonly IAgentRunIdempotencyLedger _ledger;
    private readonly IAgentRunRepository _repository;

    public AgentRunIdempotencyService(IAgentRunIdempotencyLedger ledger, IAgentRunRepository repository)
    {
        _ledger = ledger;
        _repository = repository;
    }

    public async Task<AgentRunIdempotencyResult> ExecuteAsync(
        string idempotencyKey,
        string requestFingerprint,
        Func<Task<AgentRun>> startNewRun,
        CancellationToken cancellationToken)
    {
        var sem = KeyLocks.GetOrAdd(idempotencyKey, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var existing = await _ledger.GetAsync(idempotencyKey, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                if (!string.Equals(existing.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
                {
                    return AgentRunIdempotencyResult.ConflictResult(
                        existing.RequestFingerprint,
                        requestFingerprint);
                }

                var replay = await _repository.GetAsync(existing.AgentRunId, cancellationToken).ConfigureAwait(false);
                if (replay is null)
                {
                    return AgentRunIdempotencyResult.ConflictResult(
                        existing.RequestFingerprint,
                        requestFingerprint);
                }

                return AgentRunIdempotencyResult.CompletedResult(replay);
            }

            var run = await startNewRun().ConfigureAwait(false);
            await _ledger.SaveAsync(idempotencyKey, requestFingerprint, run.Id, cancellationToken).ConfigureAwait(false);
            return AgentRunIdempotencyResult.CompletedResult(run);
        }
        finally
        {
            sem.Release();
        }
    }
}