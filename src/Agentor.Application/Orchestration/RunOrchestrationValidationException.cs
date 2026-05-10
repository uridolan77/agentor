using System.Collections.Generic;

namespace Agentor.Application.Orchestration;

/// <summary>
/// Thrown when <see cref="StartAgentRunRouting.TryBuildRequest"/> fails; mapped to HTTP 400 by the API.
/// </summary>
public sealed class RunOrchestrationValidationException : Exception
{
    public RunOrchestrationValidationException(IReadOnlyList<string> errors)
        : base(string.Join(" ", errors))
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}
