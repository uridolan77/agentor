using Agentor.Domain.Enums;

namespace Agentor.Domain;

/// <summary>
/// Centralized coordination state transition rules (PR20).
/// </summary>
public static class AgentStateMachine
{
    public static void EnsureRunCanMutate(AgentRun run)
    {
        if (run.Status is AgentRunStatus.Completed or AgentRunStatus.Failed or AgentRunStatus.RequiresReview)
        {
            throw new InvalidOperationException($"Run is terminal ({run.Status}) and cannot be mutated.");
        }
    }

    public static void EnsureRunCanComplete(AgentRun run)
    {
        if (run.Status != AgentRunStatus.Running)
        {
            throw new InvalidOperationException($"Run must be Running to complete. Current status: {run.Status}");
        }

        if (run.Steps.Count == 0)
        {
            throw new InvalidOperationException("Cannot complete a run with no steps.");
        }

        foreach (var step in run.Steps)
        {
            if (step.Status != AgentStepStatus.Completed)
            {
                throw new InvalidOperationException($"All run steps must be completed before completing the run. Step '{step.Name}' is {step.Status}.");
            }
        }
    }

    public static void EnsureRunCanFail(AgentRun run)
    {
        if (run.Status != AgentRunStatus.Running)
        {
            throw new InvalidOperationException($"Run must be Running to fail. Current status: {run.Status}");
        }
    }

    public static void EnsureRunCanEnterReview(AgentRun run)
    {
        if (run.Status != AgentRunStatus.Running)
        {
            throw new InvalidOperationException($"Run must be Running to enter review. Current status: {run.Status}");
        }
    }

    public static void EnsureStepCanMutate(AgentStep step)
    {
        if (step.Status is AgentStepStatus.Completed or AgentStepStatus.Failed or AgentStepStatus.RequiresReview)
        {
            throw new InvalidOperationException($"Step is terminal ({step.Status}) and cannot be mutated.");
        }
    }

    public static void EnsureToolCallCanMutate(ToolCall call)
    {
        if (call.Status != ToolCallStatus.Running)
        {
            throw new InvalidOperationException($"Tool call is not running ({call.Status}) and cannot be mutated.");
        }
    }

    public static void EnsurePlanStepExecutable(AgentPlanStep step)
    {
        if (step.Status is AgentPlanStepStatus.Skipped or AgentPlanStepStatus.Completed or AgentPlanStepStatus.Cancelled
            or AgentPlanStepStatus.Failed or AgentPlanStepStatus.RequiresReview)
        {
            throw new InvalidOperationException($"Plan step '{step.SourceStepId}' is {step.Status} and cannot execute.");
        }
    }

    public static AgentPlanStatus DerivePlanStatus(IReadOnlyList<AgentPlanStep> steps)
    {
        if (steps.Count == 0)
        {
            return AgentPlanStatus.Ready;
        }

        if (steps.Any(s => s.Status == AgentPlanStepStatus.RequiresReview))
        {
            return AgentPlanStatus.RequiresReview;
        }

        if (steps.Any(s => s.Status == AgentPlanStepStatus.Failed))
        {
            return AgentPlanStatus.Failed;
        }

        if (steps.All(s => s.Status is AgentPlanStepStatus.Completed or AgentPlanStepStatus.Skipped or AgentPlanStepStatus.Cancelled))
        {
            return AgentPlanStatus.Completed;
        }

        if (steps.Any(s => s.Status == AgentPlanStepStatus.Running))
        {
            return AgentPlanStatus.Running;
        }

        return AgentPlanStatus.Ready;
    }
}
