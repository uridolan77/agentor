namespace Agentor.Application.Abstractions;

public enum AgentorPermission
{
    GovernanceReviewWrite,
    GovernanceReviewRead,
    PolicyBundleWrite,
    PolicyBundleRead,
    AuditRead,
    OpsRead,
    RunWrite,
    RunRead,
    TraceRead,
    QueueWrite,
    QueueRead,
    ManagementRead,
    ManagementWrite,
}

public sealed record AuthorizationDecision(bool Allowed, string? Reason = null)
{
    public static AuthorizationDecision Allow() => new(true);

    public static AuthorizationDecision Deny(string reason) => new(false, reason);
}

public interface IAuthorizationDecisionService
{
    AuthorizationDecision Authorize(ActorContext actor, AgentorPermission permission);
}
