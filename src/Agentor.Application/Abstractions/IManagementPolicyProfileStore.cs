using Agentor.Contracts;

namespace Agentor.Application.Abstractions;

public sealed record ManagedPolicyProfile(Guid Id, string Name, PolicyProfileRulesDto Rules, DateTimeOffset CreatedAt);

public interface IManagementPolicyProfileStore
{
    IReadOnlyList<ManagedPolicyProfile> List();

    ManagedPolicyProfile? Get(Guid id);

    ManagedPolicyProfile Add(string name, PolicyProfileRulesDto rules);
}
