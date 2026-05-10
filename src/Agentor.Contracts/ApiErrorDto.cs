namespace Agentor.Contracts;

public sealed record ApiErrorDto(
    string Error,
    string Message,
    string? TraceId = null,
    IReadOnlyList<string>? Errors = null);
