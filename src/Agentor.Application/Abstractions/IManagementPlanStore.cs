using Agentor.Domain;

namespace Agentor.Application.Abstractions;

public interface IManagementPlanStore
{
    IReadOnlyList<AgentPlan> List();

    AgentPlan? Get(Guid id);

    bool TryAdd(AgentPlan plan);
}
