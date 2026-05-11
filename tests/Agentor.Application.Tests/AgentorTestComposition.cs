using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.Coordination;
using Agentor.Application.HumanReview;
using Agentor.Application.Options;
using Agentor.Application.Orchestration;
using Agentor.Infrastructure;
using Agentor.Infrastructure.Management;
using Microsoft.Extensions.Options;

namespace Agentor.Application.Tests;

internal sealed class ConstantPublicRunOptionsMonitor : IOptionsMonitor<AgentorPublicRunOptions>
{
    public ConstantPublicRunOptionsMonitor(AgentorPublicRunOptions value) => CurrentValue = value;

    public AgentorPublicRunOptions CurrentValue { get; }

    public AgentorPublicRunOptions Get(string? name) => CurrentValue;

    public IDisposable OnChange(Action<AgentorPublicRunOptions, string?> listener) => NullDisposable.Instance;

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}

internal static class AgentorTestComposition
{
    public static StartAgentRunHandler CreateStartAgentRunHandler(
        IAgentRunRepository repository,
        IPolicyEvaluator policyEvaluator,
        IToolRegistry toolRegistry,
        IClock clock,
        ToolExecutionOptions? toolExecutionOptions = null)
    {
        var opts = Microsoft.Extensions.Options.Options.Create(toolExecutionOptions ?? new ToolExecutionOptions());
        var pipeline = new ToolExecutionPipeline(clock, opts);
        var planStore = new InMemoryManagementPlanStore();
        var recipeStore = new InMemoryManagementRecipeStore();
        var skills = new InMemorySkillPackageCatalog();
        var guards = new StepGuardEvaluator();
        var executor = new SequentialAgentPlanExecutor(toolRegistry, policyEvaluator, pipeline, clock, guards, skills);
        var driver = new GovernedSingleToolRunDriver(repository, policyEvaluator, toolRegistry, pipeline, clock);
        var legacy = new LegacyFakeRunExecutor(driver);
        var orchestrator = new AgentRunOrchestrator(
            repository,
            clock,
            executor,
            planStore,
            recipeStore,
            skills,
            legacy,
            driver);
        var publicMon = new ConstantPublicRunOptionsMonitor(
            new AgentorPublicRunOptions { TreatMissingExecutionSelectorAsLegacyFakeTool = true });
        return new StartAgentRunHandler(orchestrator, publicMon);
    }

    public static SequentialAgentPlanExecutor CreateSequentialPlanExecutor(
        IToolRegistry toolRegistry,
        IPolicyEvaluator policyEvaluator,
        IClock clock,
        ToolExecutionOptions? toolExecutionOptions = null,
        IStepGuardEvaluator? guardEvaluator = null,
        ISkillPackageCatalog? skillCatalog = null)
    {
        var opts = Microsoft.Extensions.Options.Options.Create(toolExecutionOptions ?? new ToolExecutionOptions());
        var pipeline = new ToolExecutionPipeline(clock, opts);
        return new SequentialAgentPlanExecutor(
            toolRegistry,
            policyEvaluator,
            pipeline,
            clock,
            guardEvaluator ?? new StepGuardEvaluator(),
            skillCatalog ?? new EmptySkillPackageCatalog());
    }

    public static ExecuteAgentPlanHandler CreateExecuteAgentPlanHandler(
        IAgentRunRepository repository,
        IToolRegistry toolRegistry,
        IPolicyEvaluator policyEvaluator,
        IClock clock,
        ToolExecutionOptions? toolExecutionOptions = null,
        IStepGuardEvaluator? guardEvaluator = null,
        ISkillPackageCatalog? skillCatalog = null)
    {
        var executor = CreateSequentialPlanExecutor(toolRegistry, policyEvaluator, clock, toolExecutionOptions, guardEvaluator, skillCatalog);
        return new ExecuteAgentPlanHandler(executor, repository);
    }

    public static ApplyHumanReviewDecisionHandler CreateApplyHumanReviewDecisionHandler(
        IAgentRunRepository repository,
        IPolicyEvaluator policyEvaluator,
        IToolRegistry toolRegistry,
        IClock clock,
        ICurrentActorAccessor actorAccessor,
        ToolExecutionOptions? toolExecutionOptions = null,
        ISkillPackageCatalog? skillCatalog = null)
    {
        var opts = Microsoft.Extensions.Options.Options.Create(toolExecutionOptions ?? new ToolExecutionOptions());
        var pipeline = new ToolExecutionPipeline(clock, opts);
        var traceWriter = new ReviewTraceWriter(clock);
        var policyReeval = new ReviewPolicyReevaluationService(policyEvaluator);
        var planExecutor = CreateSequentialPlanExecutor(toolRegistry, policyEvaluator, clock, toolExecutionOptions, skillCatalog: skillCatalog);
        var planResume = new PlanResumeOrchestrator(toolRegistry, policyReeval, pipeline, planExecutor, clock, traceWriter);
        var continuation = new ReviewedToolContinuationService(toolRegistry, policyReeval, pipeline, clock, traceWriter, planResume, planExecutor);
        var applicator = new HumanReviewDecisionApplicator(clock, actorAccessor);
        return new ApplyHumanReviewDecisionHandler(repository, applicator, continuation);
    }
}
