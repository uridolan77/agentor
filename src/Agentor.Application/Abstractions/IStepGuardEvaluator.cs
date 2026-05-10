using Agentor.Application.Coordination;
using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Abstractions;

public sealed record StepGuardEvaluationResult(GuardedStepDecision Decision, string ReasonCode);

public interface IStepGuardEvaluator
{
    StepGuardEvaluationResult Evaluate(AgentPlanStep step, PlanExecutionContext context);
}
