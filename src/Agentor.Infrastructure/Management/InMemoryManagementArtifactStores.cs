using System.Collections.Concurrent;
using Agentor.Application.Abstractions;
using Agentor.Contracts;
using Agentor.Domain;

namespace Agentor.Infrastructure.Management;

public sealed class InMemoryManagementRecipeStore : IManagementRecipeStore
{
    private readonly ConcurrentDictionary<Guid, AgentRecipe> _recipes = new();

    public IReadOnlyList<AgentRecipe> List() =>
        _recipes.Values
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Version.Value, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Id)
            .ToList();

    public AgentRecipe? Get(Guid id)
    {
        _recipes.TryGetValue(id, out var r);
        return r;
    }

    public bool TryAdd(AgentRecipe recipe) => _recipes.TryAdd(recipe.Id, recipe);
}

public sealed class InMemoryManagementPlanStore : IManagementPlanStore
{
    private readonly ConcurrentDictionary<Guid, AgentPlan> _plans = new();

    public IReadOnlyList<AgentPlan> List() =>
        _plans.Values
            .OrderByDescending(p => p.CreatedAt)
            .ThenBy(p => p.Id)
            .ToList();

    public AgentPlan? Get(Guid id)
    {
        _plans.TryGetValue(id, out var p);
        return p;
    }

    public bool TryAdd(AgentPlan plan) => _plans.TryAdd(plan.Id, plan);
}

public sealed class InMemoryManagementPolicyProfileStore : IManagementPolicyProfileStore
{
    private readonly ConcurrentDictionary<Guid, ManagedPolicyProfile> _profiles = new();

    public IReadOnlyList<ManagedPolicyProfile> List() =>
        _profiles.Values
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Id)
            .ToList();

    public ManagedPolicyProfile? Get(Guid id)
    {
        _profiles.TryGetValue(id, out var p);
        return p;
    }

    public ManagedPolicyProfile Add(string name, PolicyProfileRulesDto rules)
    {
        var id = Guid.NewGuid();
        var created = DateTimeOffset.UtcNow;
        var profile = new ManagedPolicyProfile(id, name.Trim(), rules, created);
        _profiles[id] = profile;
        return profile;
    }
}
