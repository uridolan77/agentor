using Agentor.Application.Abstractions;

namespace Agentor.Api.Security;

public sealed class RoleBasedAuthorizationDecisionService : IAuthorizationDecisionService
{
    public AuthorizationDecision Authorize(ActorContext actor, AgentorPermission permission)
    {
        if (actor.Role == ActorRole.System)
        {
            return AuthorizationDecision.Allow();
        }

        var allowed = actor.Role switch
        {
            ActorRole.HumanOperator => true,
            ActorRole.Service => permission is AgentorPermission.PolicyBundleRead
                or AgentorPermission.AuditRead
                or AgentorPermission.GovernanceReviewRead,
            _ => false
        };

        return allowed
            ? AuthorizationDecision.Allow()
            : AuthorizationDecision.Deny($"Actor role '{actor.Role}' is not allowed to perform '{permission}'.");
    }
}
