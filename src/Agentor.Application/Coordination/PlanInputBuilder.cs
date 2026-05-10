using Agentor.Domain;

namespace Agentor.Application.Coordination;

/// <summary>
/// Builds tool-call input dictionaries for plan steps (objective, agent metadata, bound parameters, session scratch).
/// </summary>
internal static class PlanInputBuilder
{
    public static Dictionary<string, string> BuildToolStepInput(AgentRun run, AgentPlanStep ps)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["objective"] = run.Objective,
            ["agentName"] = run.AgentName
        };

        if (ps.InputBinding is not null)
        {
            foreach (var kv in ps.InputBinding.Parameters)
            {
                d[kv.Key] = kv.Value;
            }
        }

        foreach (var kv in run.SessionMemory)
        {
            d["session:" + kv.Key] = kv.Value;
        }

        return d;
    }
}
