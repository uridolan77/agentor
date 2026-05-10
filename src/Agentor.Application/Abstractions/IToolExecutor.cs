namespace Agentor.Application.Abstractions;

public interface IToolExecutor
{
    Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken);
}

public sealed record ToolExecutionRequest(
    Guid RunId,
    Guid StepId,
    string ToolKey,
    IReadOnlyDictionary<string, string> Input);

public sealed record ToolExecutionResult(
    bool Success,
    IReadOnlyDictionary<string, string> Output,
    string? ErrorMessage = null);
