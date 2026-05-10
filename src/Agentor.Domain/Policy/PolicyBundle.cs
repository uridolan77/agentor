namespace Agentor.Domain.Policy;

/// <summary>
/// Versioned, auditable set of policy rules. Immutable after publication.
/// Create via <see cref="Create"/>; reconstitute from persistence via <see cref="Reconstitute"/>.
/// </summary>
public sealed class PolicyBundle
{
    private readonly List<PolicyRule> _rules;

    public Guid Id { get; }
    public string Name { get; }
    public PolicyBundleVersion Version { get; }
    public IReadOnlyList<PolicyRule> Rules => _rules.AsReadOnly();
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public bool IsPublished => PublishedAt.HasValue;

    private PolicyBundle(
        Guid id,
        string name,
        PolicyBundleVersion version,
        List<PolicyRule> rules,
        DateTimeOffset createdAt,
        DateTimeOffset? publishedAt)
    {
        Id = id;
        Name = name;
        Version = version;
        _rules = rules;
        CreatedAt = createdAt;
        PublishedAt = publishedAt;
    }

    public static PolicyBundle Create(
        Guid id,
        string name,
        PolicyBundleVersion version,
        IEnumerable<PolicyRule> rules,
        DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Bundle ID must not be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(name, nameof(name));
        ArgumentNullException.ThrowIfNull(version, nameof(version));

        var ruleList = rules.ToList();
        var duplicateIds = ruleList
            .GroupBy(r => r.Id)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (duplicateIds.Count > 0)
        {
            throw new ArgumentException(
                $"Duplicate rule IDs in bundle: {string.Join(", ", duplicateIds)}.", nameof(rules));
        }

        return new PolicyBundle(id, name, version, ruleList, createdAt, null);
    }

    /// <summary>Publish the bundle. After publication it is immutable — rules cannot be modified.</summary>
    public void Publish(DateTimeOffset publishedAt)
    {
        if (IsPublished)
        {
            throw new InvalidOperationException(
                $"Bundle '{Id}' is already published at {PublishedAt:O}. Published bundles are immutable.");
        }

        PublishedAt = publishedAt;
    }

    /// <summary>For persistence reconstitution only. Does not re-validate rules.</summary>
    public static PolicyBundle Reconstitute(
        Guid id,
        string name,
        PolicyBundleVersion version,
        IEnumerable<PolicyRule> rules,
        DateTimeOffset createdAt,
        DateTimeOffset? publishedAt) =>
        new(id, name, version, rules.ToList(), createdAt, publishedAt);
}
