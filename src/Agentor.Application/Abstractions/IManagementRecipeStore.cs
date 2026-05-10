using Agentor.Domain;

namespace Agentor.Application.Abstractions;

public interface IManagementRecipeStore
{
    IReadOnlyList<AgentRecipe> List();

    AgentRecipe? Get(Guid id);

    bool TryAdd(AgentRecipe recipe);
}
