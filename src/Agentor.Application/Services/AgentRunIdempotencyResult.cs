using Agentor.Domain;

namespace Agentor.Application.Services;

public sealed class AgentRunIdempotencyResult
{
    private AgentRunIdempotencyResult(
        bool isConflict,
        AgentRun? run,
        string? storedFingerprint,
        string? requestFingerprint)
    {
        IsConflict = isConflict;
        Run = run;
        StoredFingerprint = storedFingerprint;
        RequestFingerprint = requestFingerprint;
    }

    public bool IsConflict { get; }

    public AgentRun? Run { get; }

    public string? StoredFingerprint { get; }

    public string? RequestFingerprint { get; }

    public static AgentRunIdempotencyResult CompletedResult(AgentRun run) =>
        new(false, run, null, null);

    public static AgentRunIdempotencyResult ConflictResult(string storedFingerprint, string requestFingerprint) =>
        new(true, null, storedFingerprint, requestFingerprint);
}