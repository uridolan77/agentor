using Agentor.Application.Abstractions;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Coordination;

public sealed class StepGuardEvaluator : IStepGuardEvaluator
{
    public StepGuardEvaluationResult Evaluate(AgentPlanStep step, PlanExecutionContext context)
    {
        var kind = step.Guard?.Kind ?? StepGuardKind.Always;
        return kind switch
        {
            StepGuardKind.Always => new StepGuardEvaluationResult(GuardedStepDecision.Execute, "GUARD_ALWAYS"),
            StepGuardKind.PreviousStepSucceeded => PreviousSucceeded(context),
            StepGuardKind.PreviousStepFailed => PreviousFailed(context),
            StepGuardKind.PreviousStepOutputExists => OutputExists(step, context),
            StepGuardKind.PreviousStepOutputEquals => OutputEquals(step, context),
            StepGuardKind.AllPreviousStepsSucceeded => AllSucceeded(context),
            _ => new StepGuardEvaluationResult(GuardedStepDecision.Execute, "GUARD_FALLTHROUGH")
        };
    }

    private static StepGuardEvaluationResult PreviousSucceeded(PlanExecutionContext ctx)
    {
        var last = ctx.Last;
        if (last is null)
        {
            return new StepGuardEvaluationResult(GuardedStepDecision.Skip, "GUARD_NO_HISTORY");
        }

        var ok = last.Status == AgentPlanStepStatus.Completed && last.ToolSucceeded;
        return new StepGuardEvaluationResult(
            ok ? GuardedStepDecision.Execute : GuardedStepDecision.Skip,
            ok ? "GUARD_PASS" : "GUARD_PREVIOUS_NOT_SUCCEEDED");
    }

    private static StepGuardEvaluationResult PreviousFailed(PlanExecutionContext ctx)
    {
        var last = ctx.Last;
        if (last is null)
        {
            return new StepGuardEvaluationResult(GuardedStepDecision.Skip, "GUARD_NO_HISTORY");
        }

        var failed = last.Status == AgentPlanStepStatus.Failed || !last.ToolSucceeded;
        return new StepGuardEvaluationResult(
            failed ? GuardedStepDecision.Execute : GuardedStepDecision.Skip,
            failed ? "GUARD_PASS" : "GUARD_PREVIOUS_NOT_FAILED");
    }

    private static StepGuardEvaluationResult AllSucceeded(PlanExecutionContext ctx)
    {
        if (ctx.History.Count == 0)
        {
            return new StepGuardEvaluationResult(GuardedStepDecision.Skip, "GUARD_NO_HISTORY");
        }

        var ok = ctx.AllExecutedSucceeded();
        return new StepGuardEvaluationResult(
            ok ? GuardedStepDecision.Execute : GuardedStepDecision.Skip,
            ok ? "GUARD_PASS" : "GUARD_NOT_ALL_SUCCEEDED");
    }

    private static StepGuardEvaluationResult OutputExists(AgentPlanStep step, PlanExecutionContext ctx)
    {
        var key = step.Guard!.OutputKey!.Trim();
        var refId = string.IsNullOrWhiteSpace(step.Guard.ReferenceStepId) ? null : step.Guard.ReferenceStepId.Trim();
        var target = refId ?? ctx.Last?.SourceStepId;
        if (target is null)
        {
            return new StepGuardEvaluationResult(GuardedStepDecision.Skip, "GUARD_NO_REFERENCE");
        }

        if (ctx.TryGetOutputForStep(target, key, out var v) && !string.IsNullOrEmpty(v))
        {
            return new StepGuardEvaluationResult(GuardedStepDecision.Execute, "GUARD_OUTPUT_EXISTS");
        }

        return new StepGuardEvaluationResult(GuardedStepDecision.Skip, "GUARD_OUTPUT_MISSING");
    }

    private static StepGuardEvaluationResult OutputEquals(AgentPlanStep step, PlanExecutionContext ctx)
    {
        var key = step.Guard!.OutputKey!.Trim();
        var expected = step.Guard.ExpectedOutputValue!.Trim();
        var refId = string.IsNullOrWhiteSpace(step.Guard.ReferenceStepId) ? null : step.Guard.ReferenceStepId.Trim();
        var target = refId ?? ctx.Last?.SourceStepId;
        if (target is null)
        {
            return new StepGuardEvaluationResult(GuardedStepDecision.Skip, "GUARD_NO_REFERENCE");
        }

        if (ctx.TryGetOutputForStep(target, key, out var v) && string.Equals(v, expected, StringComparison.Ordinal))
        {
            return new StepGuardEvaluationResult(GuardedStepDecision.Execute, "GUARD_OUTPUT_EQUALS");
        }

        return new StepGuardEvaluationResult(GuardedStepDecision.Skip, "GUARD_OUTPUT_NOT_EQUAL");
    }
}
