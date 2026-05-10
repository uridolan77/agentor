using Agentor.Domain.Enums;

namespace Agentor.Contracts;

public sealed record ToolCallDto(
    Guid Id,
    string ToolKey,
    ToolCallStatus Status,
    IReadOnlyDictionary<string, string> Input,
    IReadOnlyDictionary<string, string> Output,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    string? ErrorMessage);
