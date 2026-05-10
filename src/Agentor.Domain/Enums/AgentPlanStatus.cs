namespace Agentor.Domain.Enums;

public enum AgentPlanStatus
{
    /// <summary>Plan exists but must not execute tools yet.</summary>
    Ready,

    Running,
    Completed,
    Failed,
    Cancelled,
    RequiresReview
}
