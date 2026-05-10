using Agentor.Api.Security;
using Agentor.Application.Abstractions;

namespace Agentor.Api.Tests;

public sealed class RoleBasedAuthorizationDecisionServiceTests
{
    private static readonly Guid ActorId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    [Fact]
    public void Authorize_Allows_HumanOperator_ForGovernanceReviewWrite()
    {
        var sut = new RoleBasedAuthorizationDecisionService();
        var actor = new ActorContext(ActorId, "human", ActorRole.HumanOperator);

        var decision = sut.Authorize(actor, AgentorPermission.GovernanceReviewWrite);

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Authorize_Denies_Service_ForPolicyBundleWrite()
    {
        var sut = new RoleBasedAuthorizationDecisionService();
        var actor = new ActorContext(ActorId, "svc", ActorRole.Service);

        var decision = sut.Authorize(actor, AgentorPermission.PolicyBundleWrite);

        Assert.False(decision.Allowed);
        Assert.Contains("not allowed", decision.Reason);
    }

    [Fact]
    public void Authorize_Allows_Service_ForAuditRead()
    {
        var sut = new RoleBasedAuthorizationDecisionService();
        var actor = new ActorContext(ActorId, "svc", ActorRole.Service);

        var decision = sut.Authorize(actor, AgentorPermission.AuditRead);

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Authorize_Allows_Service_ForGovernanceReviewRead()
    {
        var sut = new RoleBasedAuthorizationDecisionService();
        var actor = new ActorContext(ActorId, "svc", ActorRole.Service);

        var decision = sut.Authorize(actor, AgentorPermission.GovernanceReviewRead);

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Authorize_Allows_System_ForOpsRead()
    {
        var sut = new RoleBasedAuthorizationDecisionService();
        var actor = new ActorContext(ActorId, "sys", ActorRole.System);

        var decision = sut.Authorize(actor, AgentorPermission.OpsRead);

        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Authorize_Denies_Service_ForOpsRead()
    {
        var sut = new RoleBasedAuthorizationDecisionService();
        var actor = new ActorContext(ActorId, "svc", ActorRole.Service);

        var decision = sut.Authorize(actor, AgentorPermission.OpsRead);

        Assert.False(decision.Allowed);
    }

    [Fact]
    public void Authorize_Allows_HumanOperator_ForOpsRead()
    {
        var sut = new RoleBasedAuthorizationDecisionService();
        var actor = new ActorContext(ActorId, "human", ActorRole.HumanOperator);

        var decision = sut.Authorize(actor, AgentorPermission.OpsRead);

        Assert.True(decision.Allowed);
    }
}
