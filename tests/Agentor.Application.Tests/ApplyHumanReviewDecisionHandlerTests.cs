using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.HumanReview;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Domain.Governance;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.ExternalAgents;
using Agentor.Infrastructure.Mcp;
using Xunit;

namespace Agentor.Application.Tests;

public sealed class ApplyHumanReviewDecisionHandlerTests
{
    private sealed class StubPolicyEvaluator : IPolicyEvaluator
    {
        public PolicyDecisionOutcome Outcome { get; init; } = PolicyDecisionOutcome.Allow;

        public Task<PolicyDecision> EvaluateToolCallAsync(PolicyEvaluationRequest request, CancellationToken cancellationToken)
        {
            var decision = new PolicyDecision(
                Guid.NewGuid(),
                request.RunId,
                request.StepId,
                Outcome,
                Outcome == PolicyDecisionOutcome.Deny ? "DENY" : "ALLOW",
                Outcome == PolicyDecisionOutcome.Deny ? "Denied after human approval." : "Allowed.",
                DateTimeOffset.UtcNow);
            return Task.FromResult(decision);
        }
    }

    private sealed class FixedActorAccessor(Guid actorId, ActorRole role = ActorRole.HumanOperator) : ICurrentActorAccessor
    {
        public ActorContext Current { get; } = new(actorId, "test-actor", role);
    }

    private static AgentRun CreateRunPendingHumanReview(DateTimeOffset now)
    {
        var profileId = Guid.NewGuid();
        var run = AgentRun.Start(profileId, "Agent", "Objective", "trace-hr", now);
        var step = run.StartStep("Step", now);
        var tool = ToolCall.Start(run.Id, step.Id, WellKnownToolKeys.Pr1FakeTool, new Dictionary<string, string>(), now);
        step.AddToolCall(tool);
        tool.MarkRequiresReview("needs review", now);
        step.MarkRequiresReview(now);
        run.EnterRequiresReview("needs review", now);
        return run;
    }

    [Fact]
    public async Task Approve_PostPolicyDeny_FailsRunAndDoesNotExecuteTool()
    {
        var repo = new InMemoryAgentRunRepository();
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var run = CreateRunPendingHumanReview(now);
        await repo.SaveAsync(run, CancellationToken.None);

        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake, new FakeModelGatewayClient(), new FakeMcpRegistryClient(), new FakeA2AExternalAgentClient());
        var stubPolicy = new StubPolicyEvaluator { Outcome = PolicyDecisionOutcome.Deny };
        var actor = new FixedActorAccessor(Guid.Parse("22222222-2222-4222-8222-222222222222"));
        var handler = AgentorTestComposition.CreateApplyHumanReviewDecisionHandler(
            repo, stubPolicy, registry, clock, actor);

        var result = await handler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Approve, null),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(AgentRunStatus.Failed, result!.Status);
        var step = result.Steps.Single();
        Assert.Equal(AgentStepStatus.Failed, step.Status);
        var tool = step.ToolCalls.Single();
        Assert.Equal(ToolCallStatus.Denied, tool.Status);
        Assert.Contains(step.PolicyDecisions, d => d.Outcome == PolicyDecisionOutcome.Deny);
    }

    [Fact]
    public async Task RequestChanges_SetsWorkflow_ToChangesRequested()
    {
        var repo = new InMemoryAgentRunRepository();
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var run = CreateRunPendingHumanReview(now);
        await repo.SaveAsync(run, CancellationToken.None);

        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake, new FakeModelGatewayClient(), new FakeMcpRegistryClient(), new FakeA2AExternalAgentClient());
        var stubPolicy = new StubPolicyEvaluator();
        var handler = AgentorTestComposition.CreateApplyHumanReviewDecisionHandler(
            repo,
            stubPolicy,
            registry,
            clock,
            new FixedActorAccessor(Guid.Parse("22222222-2222-4222-8222-222222222222")));

        var result = await handler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.RequestChanges, "Revise section A."),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(AgentRunStatus.RequiresReview, result!.Status);
        Assert.Equal(HumanReviewWorkflowStatus.ChangesRequested, result.ReviewWorkflowStatus);
    }

    [Fact]
    public async Task Escalate_ThenApprove_AsOperator_Throws()
    {
        var repo = new InMemoryAgentRunRepository();
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var run = CreateRunPendingHumanReview(now);
        await repo.SaveAsync(run, CancellationToken.None);

        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake, new FakeModelGatewayClient(), new FakeMcpRegistryClient(), new FakeA2AExternalAgentClient());
        var stubPolicy = new StubPolicyEvaluator();
        var actorId = Guid.Parse("22222222-2222-4222-8222-222222222222");

        var escalateHandler = AgentorTestComposition.CreateApplyHumanReviewDecisionHandler(
            repo, stubPolicy, registry, clock, new FixedActorAccessor(actorId));

        await escalateHandler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Escalate, "Needs governance."),
            CancellationToken.None);

        var approveHandler = AgentorTestComposition.CreateApplyHumanReviewDecisionHandler(
            repo, stubPolicy, registry, clock, new FixedActorAccessor(actorId));

        await Assert.ThrowsAsync<GovernanceApproverRequiredException>(() =>
            approveHandler.HandleAsync(
                new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Approve, null),
                CancellationToken.None));
    }

    [Fact]
    public async Task Escalate_ThenApprove_AsGovernanceApprover_ResumesRun()
    {
        var repo = new InMemoryAgentRunRepository();
        var clock = new SystemClock();
        var now = clock.UtcNow;
        var run = CreateRunPendingHumanReview(now);
        await repo.SaveAsync(run, CancellationToken.None);

        var fake = new FakeToolExecutor();
        var registry = ToolRegistry.CreateDefault(fake, new FakeModelGatewayClient(), new FakeMcpRegistryClient(), new FakeA2AExternalAgentClient());
        var stubPolicy = new StubPolicyEvaluator();
        var actorId = Guid.Parse("22222222-2222-4222-8222-222222222222");

        var escalateHandler = AgentorTestComposition.CreateApplyHumanReviewDecisionHandler(
            repo, stubPolicy, registry, clock, new FixedActorAccessor(actorId));

        await escalateHandler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Escalate, "Needs governance."),
            CancellationToken.None);

        var approveHandler = AgentorTestComposition.CreateApplyHumanReviewDecisionHandler(
            repo,
            stubPolicy,
            registry,
            clock,
            new FixedActorAccessor(actorId, ActorRole.HumanGovernanceApprover));

        var result = await approveHandler.HandleAsync(
            new ApplyHumanReviewDecisionCommand(run.Id, ReviewDecisionKind.Approve, null),
            CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(AgentRunStatus.Completed, result!.Status);
        Assert.Equal(HumanReviewWorkflowStatus.None, result.ReviewWorkflowStatus);
    }
}
