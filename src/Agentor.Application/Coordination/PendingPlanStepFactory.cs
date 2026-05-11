using Agentor.Domain;
using Agentor.Domain.Governance;

namespace Agentor.Application.Coordination;

internal static class PendingPlanStepFactory
{
    public static PendingPlanStep FromAgentPlanStep(AgentPlanStep s) =>
        new(
            s.Id,
            s.SourceStepId,
            s.OrderIndex,
            s.ToolKey,
            s.Kind,
            s.OnFailure,
            s.InputBinding?.Parameters,
            s.OutputBinding,
            s.InvokedSkillKey,
            s.InvokedSkillVersion);

    public static AgentPlanStep ToAgentPlanStep(PendingPlanStep p) =>
        new(
            p.PlanStepId,
            p.SourceStepId,
            p.OrderIndex,
            p.Kind,
            p.ToolKey ?? string.Empty,
            guard: null,
            p.StaticInputParameters is null ? null : new StepInputBinding(p.StaticInputParameters),
            p.OutputBinding,
            p.OnFailure,
            compensationHook: null,
            p.InvokedSkillKey,
            p.InvokedSkillVersion);
}
