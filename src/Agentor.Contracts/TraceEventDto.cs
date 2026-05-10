using Agentor.Domain.Enums;

namespace Agentor.Contracts;

public sealed record TraceEventDto(
    Guid Id,
    TraceEventKind Kind,
    string Message,
    DateTimeOffset OccurredAt,
    IReadOnlyDictionary<string, string> Data);
