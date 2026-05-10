using System.Linq;
using Agentor.Application.Abstractions;
using Agentor.Application.Coordination;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Orchestration;

public sealed class AgentRunOrchestrator : IAgentRunOrchestrator
{
    private readonly IAgentRunRepository _repository;
    private readonly IClock _clock;
    private readonly IAgentPlanExecutor _planExecutor;
    private readonly IManagementPlanStore _plans;
    private readonly IManagementRecipeStore _recipes;
    private readonly ISkillPackageCatalog _skills;
    private readonly LegacyFakeRunExecutor _legacyFake;
    private readonly GovernedSingleToolRunDriver _singleTool;

    public AgentRunOrchestrator(
        IAgentRunRepository repository,
        IClock clock,
        IAgentPlanExecutor planExecutor,
        IManagementPlanStore plans,
        IManagementRecipeStore recipes,
        ISkillPackageCatalog skills,
        LegacyFakeRunExecutor legacyFake,
        GovernedSingleToolRunDriver singleTool)
    {
        _repository = repository;
        _clock = clock;
        _planExecutor = planExecutor;
        _plans = plans;
        _recipes = recipes;
        _skills = skills;
        _legacyFake = legacyFake;
        _singleTool = singleTool;
    }

    public async Task<AgentRun> StartAsync(RunOrchestrationRequest request, CancellationToken cancellationToken)
    {
        switch (request.Mode)
        {
            case RunExecutionMode.LegacyFakeTool:
                return await _legacyFake.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

            case RunExecutionMode.SingleTool:
            case RunExecutionMode.ModelCall:
            case RunExecutionMode.McpTool:
            case RunExecutionMode.ExternalAgent:
                if (string.IsNullOrWhiteSpace(request.ToolKey))
                {
                    throw new InvalidOperationException("toolKey is required for this execution mode.");
                }

                return await _singleTool.ExecuteAsync(
                    request,
                    profilePurpose: "Orchestrated single-tool run.",
                    stepSummary: $"Execute tool '{request.ToolKey.Trim()}'.",
                    toolKey: request.ToolKey.Trim(),
                    cancellationToken).ConfigureAwait(false);

            case RunExecutionMode.Plan:
                return await ExecutePlanFromStoreAsync(request, cancellationToken).ConfigureAwait(false);

            case RunExecutionMode.Recipe:
                return await ExecuteRecipeAsync(request, cancellationToken).ConfigureAwait(false);

            case RunExecutionMode.Skill:
                return await ExecuteSkillAsync(request, cancellationToken).ConfigureAwait(false);

            default:
                throw new InvalidOperationException($"Unsupported execution mode '{request.Mode}'.");
        }
    }

    private async Task<AgentRun> ExecutePlanFromStoreAsync(RunOrchestrationRequest request, CancellationToken cancellationToken)
    {
        if (request.PlanId is null)
        {
            throw new InvalidOperationException("planId is required for plan execution.");
        }

        var template = _plans.Get(request.PlanId.Value);
        if (template is null)
        {
            throw new InvalidOperationException($"Plan '{request.PlanId:D}' was not found.");
        }

        var recipe = _recipes.Get(template.RecipeId);
        if (recipe is null)
        {
            throw new InvalidOperationException($"Recipe '{template.RecipeId:D}' for plan '{request.PlanId:D}' was not found.");
        }

        var plan = AgentPlan.Instantiate(recipe, Guid.NewGuid(), _clock.UtcNow, template.Topology);
        return await ExecuteMaterializedPlanAsync(request, plan, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AgentRun> ExecuteRecipeAsync(RunOrchestrationRequest request, CancellationToken cancellationToken)
    {
        if (request.RecipeId is null)
        {
            throw new InvalidOperationException("recipeId is required for recipe execution.");
        }

        var recipe = _recipes.Get(request.RecipeId.Value);
        if (recipe is null)
        {
            throw new InvalidOperationException($"Recipe '{request.RecipeId:D}' was not found.");
        }

        var plan = AgentPlan.Instantiate(recipe, Guid.NewGuid(), _clock.UtcNow);
        return await ExecuteMaterializedPlanAsync(request, plan, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AgentRun> ExecuteSkillAsync(RunOrchestrationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SkillKey))
        {
            throw new InvalidOperationException("skillKey is required for skill execution.");
        }

        var key = request.SkillKey.Trim();
        var match = _skills.ListRegisteredPackages()
            .Where(p => string.Equals(p.SkillKey, key, StringComparison.OrdinalIgnoreCase))
            .OrderBy(p => p.Version.Value, StringComparer.OrdinalIgnoreCase)
            .LastOrDefault();

        if (match is null)
        {
            throw new InvalidOperationException($"Skill '{key}' is not registered.");
        }

        var step = new RecipeStepDefinition(
            "skill-entry",
            0,
            RecipeStepKind.Skill,
            string.Empty,
            Guard: null,
            InputBinding: null,
            OutputBinding: null,
            OnFailure: FailureHandlingPolicy.FailFast,
            Compensation: null,
            InvokedSkillKey: match.SkillKey,
            InvokedSkillVersion: match.Version);

        if (!AgentRecipe.TryCreate(
                Guid.NewGuid(),
                "runtime.skill-wrap",
                AgentRecipeVersion.Parse("1.0.0"),
                CoordinationTopology.SequentialPipeline,
                [step],
                profileRef: null,
                out var recipe,
                out var validation))
        {
            var msg = string.Join("; ", validation.Issues.Select(i => i.Message));
            throw new InvalidOperationException($"Skill wrap recipe invalid: {msg}");
        }

        var plan = AgentPlan.Instantiate(recipe!, Guid.NewGuid(), _clock.UtcNow);
        return await ExecuteMaterializedPlanAsync(request, plan, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AgentRun> ExecuteMaterializedPlanAsync(
        RunOrchestrationRequest request,
        AgentPlan plan,
        CancellationToken cancellationToken)
    {
        var profile = AgentProfile.Create(
            string.IsNullOrWhiteSpace(request.AgentName) ? "Plan Agent" : request.AgentName.Trim(),
            "Orchestrated plan execution.",
            _clock.UtcNow);

        var traceId = string.IsNullOrWhiteSpace(request.TraceId)
            ? Guid.NewGuid().ToString("N")
            : request.TraceId.Trim();

        var scope = new AgentRunScope(
            request.TenantId,
            request.WorkspaceId,
            request.ProjectId,
            request.KnowledgeScopeId);

        var run = AgentRun.Start(profile.Id, profile.Name, request.Objective, traceId, _clock.UtcNow, scope);

        _ = await _planExecutor.ExecuteAsync(run, plan, cancellationToken).ConfigureAwait(false);
        await _repository.SaveAsync(run, cancellationToken).ConfigureAwait(false);
        return run;
    }
}
