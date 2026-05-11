using Agentor.Domain;

namespace Agentor.Application.Abstractions;

public enum ToolPipelineFailureKind
{
    None,
    ExecutorFailed,
    Timeout,
    Canceled
}

public sealed record ToolPipelineExecutionResult(
    bool Success,
    ToolPayload? Output,
    string? ErrorMessage,
    ToolPipelineFailureKind FailureKind,
    int AttemptsUsed,
    TimeSpan TotalDuration);

public interface IToolExecutionPipeline
{
    Task<ToolPipelineExecutionResult> ExecuteAsync(
        AgentRun run,
        Guid stepId,
        Guid toolCallId,
        IToolExecutor executor,
        ToolExecutionRequest request,
        CancellationToken cancellationToken);
}