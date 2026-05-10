using Agentor.Domain.Enums;

namespace Agentor.Domain;

public sealed class PolicyDecision
{
    public PolicyDecision(
        Guid id,
        Guid runId,
        Guid stepId,
        PolicyDecisionOutcome outcome,
        string reasonCode,
        string reason,
        DateTimeOffset decidedAt)
    {
        Id = id;
        RunId = runId;
        StepId = stepId;
        Outcome = outcome;
        ReasonCode = reasonCode;
        Reason = reason;
        DecidedAt = decidedAt;
    }

    public Guid Id { get; }

    public Guid RunId { get; }

    public Guid StepId { get; }

    public PolicyDecisionOutcome Outcome { get; }

    public string ReasonCode { get; }

    public string Reason { get; }

    public DateTimeOffset DecidedAt { get; }

    public bool AllowsExecution => Outcome == PolicyDecisionOutcome.Allow;
}
