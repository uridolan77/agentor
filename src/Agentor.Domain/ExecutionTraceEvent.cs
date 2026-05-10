using Agentor.Domain.Enums;

namespace Agentor.Domain;

public sealed class ExecutionTraceEvent
{
    public ExecutionTraceEvent(
        Guid id,
        Guid runId,
        TraceEventKind kind,
        string message,
        DateTimeOffset occurredAt,
        IReadOnlyDictionary<string, string>? data = null)
    {
        Id = id;
        RunId = runId;
        Kind = kind;
        Message = message;
        OccurredAt = occurredAt;
        Data = data is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(data, StringComparer.OrdinalIgnoreCase);
    }

    public Guid Id { get; }

    public Guid RunId { get; }

    public TraceEventKind Kind { get; }

    public string Message { get; }

    public DateTimeOffset OccurredAt { get; }

    public IReadOnlyDictionary<string, string> Data { get; }
}
