using Agentor.Domain;

namespace Agentor.Application.Abstractions;

public interface IToolExecutor
{
    Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken);
}

public sealed record ToolExecutionRequest(
    Guid RunId,
    Guid StepId,
    string ToolKey,
    ToolPayload Input);

public sealed record ToolExecutionResult(
    bool Success,
    ToolPayload Output,
    string? ErrorMessage = null);
