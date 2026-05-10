namespace Agentor.Domain.Policy;

/// <summary>
/// Named policy profile that can be bound to specific PolicyBundle versions and activated for runtime evaluation.
/// </summary>
public sealed class PolicyProfile
{
    private readonly List<PolicyProfileBinding> _bindings;

    public Guid Id { get; }
    public string Name { get; }
    public DateTimeOffset CreatedAt { get; }
    public IReadOnlyList<PolicyProfileBinding> Bindings => _bindings.AsReadOnly();

    /// <summary>The most-recently added binding, or null if the profile has no bundle binding yet.</summary>
    public PolicyProfileBinding? LatestBinding => _bindings.Count == 0 ? null : _bindings[^1];

    private PolicyProfile(Guid id, string name, DateTimeOffset createdAt, List<PolicyProfileBinding> bindings)
    {
        Id = id;
        Name = name;
        CreatedAt = createdAt;
        _bindings = bindings;
    }

    public static PolicyProfile Create(Guid id, string name, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Profile ID must not be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        return new PolicyProfile(id, name, createdAt, []);
    }

    public void BindToBundle(Guid bundleId, PolicyBundleVersion bundleVersion, DateTimeOffset boundAt)
    {
        _bindings.Add(new PolicyProfileBinding(bundleId, bundleVersion, boundAt));
    }

    /// <summary>For persistence reconstitution only.</summary>
    public static PolicyProfile Reconstitute(
        Guid id,
        string name,
        DateTimeOffset createdAt,
        IEnumerable<PolicyProfileBinding> bindings) =>
        new(id, name, createdAt, bindings.ToList());
}
