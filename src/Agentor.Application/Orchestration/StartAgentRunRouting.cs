using Agentor.Application;
using Agentor.Application.Commands;
using Agentor.Application.Options;
using Agentor.Domain.Enums;

namespace Agentor.Application.Orchestration;

public static class StartAgentRunRouting
{
    public static bool TryBuildRequest(
        StartAgentRunCommand command,
        AgentorPublicRunOptions options,
        out RunOrchestrationRequest? request,
        out IReadOnlyList<string>? errors)
    {
        var list = new List<string>();
        var planId = command.PlanId;
        var recipeId = command.RecipeId;
        var toolKey = string.IsNullOrWhiteSpace(command.ToolKey) ? null : command.ToolKey.Trim();
        var skillKey = string.IsNullOrWhiteSpace(command.SkillKey) ? null : command.SkillKey.Trim();
        var explicitMode = command.Mode;

        var selectorCount = (planId is not null ? 1 : 0)
            + (recipeId is not null ? 1 : 0)
            + (toolKey is not null ? 1 : 0)
            + (skillKey is not null ? 1 : 0);

        if (selectorCount > 1)
        {
            list.Add("Specify at most one of planId, recipeId, toolKey, or skillKey.");
        }

        if (explicitMode is RunExecutionMode.LegacyFakeTool && selectorCount > 0)
        {
            list.Add("mode LegacyFakeTool cannot be combined with planId, recipeId, toolKey, or skillKey.");
        }

        if (explicitMode is RunExecutionMode.Plan && planId is null)
        {
            list.Add("mode Plan requires planId.");
        }

        if (explicitMode is RunExecutionMode.Recipe && recipeId is null)
        {
            list.Add("mode Recipe requires recipeId.");
        }

        if (explicitMode is RunExecutionMode.Skill && skillKey is null)
        {
            list.Add("mode Skill requires skillKey.");
        }

        if (explicitMode is RunExecutionMode.SingleTool
            or RunExecutionMode.ModelCall
            or RunExecutionMode.McpTool
            or RunExecutionMode.ExternalAgent)
        {
            if (toolKey is null)
            {
                list.Add($"mode {explicitMode} requires toolKey.");
            }
        }

        if (list.Count > 0)
        {
            request = null;
            errors = list;
            return false;
        }

        RunExecutionMode resolvedMode;
        if (planId is not null)
        {
            resolvedMode = RunExecutionMode.Plan;
        }
        else if (recipeId is not null)
        {
            resolvedMode = RunExecutionMode.Recipe;
        }
        else if (toolKey is not null)
        {
            resolvedMode = explicitMode ?? InferToolMode(toolKey);
        }
        else if (skillKey is not null)
        {
            resolvedMode = RunExecutionMode.Skill;
        }
        else if (explicitMode is not null)
        {
            resolvedMode = explicitMode.Value;
            if (resolvedMode is not RunExecutionMode.LegacyFakeTool)
            {
                list.Add("Without planId, recipeId, toolKey, or skillKey, only mode LegacyFakeTool is valid.");
                request = null;
                errors = list;
                return false;
            }
        }
        else if (options.TreatMissingExecutionSelectorAsLegacyFakeTool)
        {
            resolvedMode = RunExecutionMode.LegacyFakeTool;
        }
        else
        {
            list.Add(
                "Specify planId, recipeId, toolKey, skillKey, or mode=LegacyFakeTool, or enable Agentor:PublicRuns:TreatMissingExecutionSelectorAsLegacyFakeTool for implicit legacy routing.");
            request = null;
            errors = list;
            return false;
        }

        request = new RunOrchestrationRequest(
            command.AgentName,
            command.Objective,
            command.TraceId,
            command.TenantId,
            command.WorkspaceId,
            command.ProjectId,
            command.KnowledgeScopeId,
            resolvedMode,
            recipeId,
            planId,
            toolKey,
            skillKey,
            command.ToolInput,
            command.ToolInputPayload);
        errors = null;
        return true;
    }

    private static RunExecutionMode InferToolMode(string toolKey)
    {
        if (string.Equals(toolKey, WellKnownToolKeys.ConexusModelComplete, StringComparison.OrdinalIgnoreCase))
        {
            return RunExecutionMode.ModelCall;
        }

        if (ExternalAgentToolKeys.IsExternalAgentTool(toolKey))
        {
            return RunExecutionMode.ExternalAgent;
        }

        if (McpToolKeys.IsMcpToolKey(toolKey))
        {
            return RunExecutionMode.McpTool;
        }

        return RunExecutionMode.SingleTool;
    }
}
