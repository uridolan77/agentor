namespace Agentor.Application.Observability;

/// <summary>
/// Canonical log state keys for structured logging (no payload bodies).
/// </summary>
public static class AgentorLogFields
{
    public const string RunId = "runId";
    public const string RunTraceId = "runTraceId";
    public const string RequestTraceId = "requestTraceId";
    public const string ToolKey = "toolKey";
    public const string PolicyEffect = "policyEffect";
    public const string ReasonCode = "reasonCode";
    public const string IntegrationName = "integrationName";
    public const string Status = "status";
    public const string WorkItemId = "workItemId";
    public const string DurationMs = "durationMs";
    public const string AttemptsUsed = "attemptsUsed";
    public const string Message = "message";
    public const string OutboxMessageId = "outboxMessageId";
}
