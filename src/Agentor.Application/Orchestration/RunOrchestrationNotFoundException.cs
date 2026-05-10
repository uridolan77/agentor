namespace Agentor.Application.Orchestration;

/// <summary>
/// Thrown when a referenced orchestration artifact (plan, recipe, skill package, etc.) does not exist;
/// mapped to HTTP 404 by the API.
/// </summary>
public sealed class RunOrchestrationNotFoundException : Exception
{
    public RunOrchestrationNotFoundException(string reasonCode, string message)
        : base(message)
    {
        ReasonCode = reasonCode;
    }

    /// <summary>Stable machine-readable code for clients (serialized as the API error <c>Error</c> field).</summary>
    public string ReasonCode { get; }
}
