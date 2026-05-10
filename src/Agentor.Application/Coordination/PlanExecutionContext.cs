using Agentor.Domain;
using Agentor.Domain.Enums;

namespace Agentor.Application.Coordination;

public sealed record PlanStepExecutionSnapshot(
    Guid PlanStepId,
    string SourceStepId,
    AgentPlanStepStatus Status,
    bool ToolSucceeded,
    IReadOnlyDictionary<string, string>? ToolOutput);

public sealed class PlanExecutionContext
{
    public List<PlanStepExecutionSnapshot> History { get; } = new();

    public PlanStepExecutionSnapshot? Last => History.Count == 0 ? null : History[^1];

    public bool AllExecutedSucceeded()
    {
        var anyNonSkipped = false;
        foreach (var h in History)
        {
            if (h.Status == AgentPlanStepStatus.Skipped || h.Status == AgentPlanStepStatus.Cancelled)
            {
                continue;
            }

            anyNonSkipped = true;
            if (!h.ToolSucceeded || h.Status != AgentPlanStepStatus.Completed)
            {
                return false;
            }
        }

        return anyNonSkipped;
    }

    public bool TryGetOutputForStep(string sourceStepId, string outputKey, out string? value)
    {
        foreach (var h in History.AsEnumerable().Reverse())
        {
            if (!string.Equals(h.SourceStepId, sourceStepId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (h.ToolOutput is null || !h.ToolOutput.TryGetValue(outputKey, out var v))
            {
                value = null;
                return false;
            }

            value = v;
            return true;
        }

        value = null;
        return false;
    }
}
