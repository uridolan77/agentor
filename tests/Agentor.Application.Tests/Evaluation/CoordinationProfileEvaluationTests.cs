using Agentor.Application;
using Agentor.Application.Abstractions;
using Agentor.Application.Coordination;
using Agentor.Application.Evaluation;
using Agentor.Domain;
using Agentor.Domain.Enums;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Conexus;
using Agentor.Infrastructure.ExternalAgents;
using Agentor.Infrastructure.Mcp;
using Xunit;

namespace Agentor.Application.Tests.Evaluation;

public sealed class CoordinationProfileEvaluationTests
{
    [Fact]
    public async Task Sequential_profile_matches_fixture_expectation()
    {
        var clock = new SystemClock();
        var def = LoadOneStepFixture();
        Assert.True(HarnessProfileMaterializer.TryCreateRunAndPlan(def, CoordinationEvaluationProfile.SequentialPipeline, clock.UtcNow, out var run, out var plan, out var err), err);
        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(WellKnownToolKeys.Pr1FakeTool, "t", "d", ToolRiskLevel.Low), new EchoExecutor());
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);
        var snap = await RunEvaluationHarness.ExecutePlanAsync(executor, run!, plan!, CancellationToken.None);
        AssertSnapshot(def.ExpectedSnapshot!, snap);
        Assert.Equal(AgentRunStatus.Completed, run!.Status);
    }

    [Fact]
    public async Task Skill_wrapped_profile_completes_with_skill_traces()
    {
        var clock = new SystemClock();
        var def = LoadOneStepFixture();
        Assert.True(HarnessProfileMaterializer.TryCreatePhase14EvalSkill(out var skill, out var v) && v.IsValid);
        Assert.True(HarnessProfileMaterializer.TryCreateRunAndPlan(def, CoordinationEvaluationProfile.SkillWrappedSequential, clock.UtcNow, out var run, out var plan, out var err), err);
        var catalog = new InMemorySkillPackageCatalog();
        catalog.Register(skill!);
        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(WellKnownToolKeys.Pr1FakeTool, "t", "d", ToolRiskLevel.Low), new EchoExecutor());
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock, skillCatalog: catalog);
        var snap = await RunEvaluationHarness.ExecutePlanAsync(executor, run!, plan!, CancellationToken.None);
        Assert.Equal(AgentRunStatus.Completed, snap.RunStatus);
        Assert.Contains(run!.Trace, e => e.Kind == TraceEventKind.SkillInvocationStarted);
        Assert.True(snap.TraceEventCount > def.ExpectedSnapshot!.TraceEventCount);
    }

    [Fact]
    public async Task Mcp_tool_bound_profile_completes()
    {
        var clock = new SystemClock();
        var def = LoadOneStepFixture();
        Assert.True(HarnessProfileMaterializer.TryCreateRunAndPlan(def, CoordinationEvaluationProfile.McpToolBoundPlan, clock.UtcNow, out var run, out var plan, out var err), err);
        var registry = ToolRegistry.CreateDefault(new FakeToolExecutor(), new FakeModelGatewayClient(), new FakeMcpRegistryClient(), new FakeA2AExternalAgentClient());
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);
        var snap = await RunEvaluationHarness.ExecutePlanAsync(executor, run!, plan!, CancellationToken.None);
        Assert.Equal(AgentRunStatus.Completed, snap.RunStatus);
        Assert.Equal(1, snap.ToolCallCount);
    }

    [Fact]
    public async Task External_agent_profile_completes_for_external_fixture()
    {
        var clock = new SystemClock();
        var def = LoadExternalFixture();
        Assert.True(HarnessProfileMaterializer.TryCreateRunAndPlan(def, CoordinationEvaluationProfile.ExternalAgentTool, clock.UtcNow, out var run, out var plan, out var err), err);
        var registry = ToolRegistry.CreateDefault(new FakeToolExecutor(), new FakeModelGatewayClient(), new FakeMcpRegistryClient(), new FakeA2AExternalAgentClient());
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);
        var snap = await RunEvaluationHarness.ExecutePlanAsync(executor, run!, plan!, CancellationToken.None);
        AssertSnapshot(def.ExpectedSnapshot!, snap);
    }

    [Fact]
    public async Task Review_gated_profile_stops_at_requires_review()
    {
        var clock = new SystemClock();
        var def = LoadOneStepFixture();
        Assert.True(HarnessProfileMaterializer.TryCreateRunAndPlan(def, CoordinationEvaluationProfile.ReviewGatedPlan, clock.UtcNow, out var run, out var plan, out var err), err);
        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(WellKnownToolKeys.Pr1HighRiskFakeTool, "t", "d", ToolRiskLevel.High), new EchoExecutor());
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions { MaxAutoApproveRisk = nameof(ToolRiskLevel.Low) }));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);
        var snap = await RunEvaluationHarness.ExecutePlanAsync(executor, run!, plan!, CancellationToken.None);
        Assert.Equal(AgentRunStatus.RequiresReview, snap.RunStatus);
        Assert.Equal(AgentRunStatus.RequiresReview, run!.Status);
    }

    [Fact]
    public async Task Coordination_metrics_derive_from_run_and_manifest()
    {
        var clock = new SystemClock();
        var def = LoadOneStepFixture();
        Assert.True(HarnessProfileMaterializer.TryCreateRunAndPlan(def, CoordinationEvaluationProfile.SequentialPipeline, clock.UtcNow, out var run, out var plan, out var err), err);
        var registry = new ToolRegistry();
        registry.Register(new ToolDefinition(WellKnownToolKeys.Pr1FakeTool, "t", "d", ToolRiskLevel.Low), new EchoExecutor());
        var policy = new RuntimePolicyEvaluator(registry, clock, Microsoft.Extensions.Options.Options.Create(new RuntimePolicyOptions()));
        var executor = AgentorTestComposition.CreateSequentialPlanExecutor(registry, policy, clock);
        await RunEvaluationHarness.ExecutePlanAsync(executor, run!, plan!, CancellationToken.None);
        var manifest = RunManifest.FromRun(run!, RunManifestModelTelemetry.Empty);
        var m = CoordinationEvaluationMetrics.FromArtifacts(run!, manifest);
        Assert.Equal(1.0, m.Reliability);
        Assert.Equal(1.0, m.Resolution);
        Assert.True(m.TraceEventCount > 0);
    }

    private static HarnessFixtureDefinition LoadOneStepFixture()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "fixtures", "eval");
        var reg = EvaluationFixtureRegistry.Load(Path.Combine(dir, "registry.json"), dir);
        return reg.LoadHarnessFixture("one-step-fake-tool");
    }

    private static HarnessFixtureDefinition LoadExternalFixture()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "fixtures", "eval");
        var reg = EvaluationFixtureRegistry.Load(Path.Combine(dir, "registry.json"), dir);
        return reg.LoadHarnessFixture("external-agent-one-call");
    }

    private static void AssertSnapshot(HarnessExpectedSnapshot exp, RunEvaluationSnapshot snap)
    {
        Assert.Equal(Enum.Parse<AgentRunStatus>(exp.RunStatus, ignoreCase: true), snap.RunStatus);
        Assert.Equal(exp.TraceEventCount, snap.TraceEventCount);
        Assert.Equal(exp.ToolCallCount, snap.ToolCallCount);
        Assert.Equal(exp.PlanStepCount, snap.PlanStepCount);
        Assert.Equal(exp.ExternalAgentInvocationCompletedCount, snap.ExternalAgentInvocationCompletedCount);
    }

    private sealed class EchoExecutor : IToolExecutor
    {
        public Task<ToolExecutionResult> ExecuteAsync(ToolExecutionRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ToolExecutionResult(true, new Dictionary<string, string>(), null));
    }
}
