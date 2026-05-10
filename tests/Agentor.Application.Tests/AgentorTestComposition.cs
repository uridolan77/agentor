using Agentor.Application.Abstractions;
using Agentor.Application.Commands;
using Agentor.Application.Coordination;
using Agentor.Infrastructure;
using Microsoft.Extensions.Options;

namespace Agentor.Application.Tests;

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
        return new StartAgentRunHandler(repository, policyEvaluator, toolRegistry, pipeline, clock);
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
}
