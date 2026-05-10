namespace Agentor.Infrastructure.Persistence.Records;

public sealed class PolicyDecisionRecord
{
    public Guid Id { get; set; }
    public Guid RunId { get; set; }
    public Guid StepId { get; set; }
    public string Outcome { get; set; } = string.Empty;
    public string ReasonCode { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset DecidedAt { get; set; }

    public AgentStepRecord Step { get; set; } = null!;
}
