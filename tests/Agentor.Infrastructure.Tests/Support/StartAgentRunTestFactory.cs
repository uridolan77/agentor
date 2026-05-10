using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.Coordination;
using Agentor.Application.Options;
using Agentor.Application.Orchestration;
using Agentor.Infrastructure.Management;
using Microsoft.Extensions.Options;

namespace Agentor.Infrastructure.Tests.Support;

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

internal static class StartAgentRunTestFactory
{
    public static StartAgentRunHandler CreateHandler(
        IAgentRunRepository repository,
        IPolicyEvaluator policyEvaluator,
        IToolRegistry toolRegistry,
        IClock clock)
    {
        var pipeline = new ToolExecutionPipeline(clock, Microsoft.Extensions.Options.Options.Create(new ToolExecutionOptions()));
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
}
