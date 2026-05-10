namespace Agentor.Application.Redaction;

public static class SensitiveFieldCatalog
{
    public static IReadOnlyList<string> DefaultKeyNameSubstrings { get; } =
    [
        "apiKey",
        "secret",
        "password",
        "token",
        "authorization",
        "bearer",
        "credential"
    ];

    public static IReadOnlyList<string> MergeWithConfigured(IEnumerable<string>? additionalSubstrings)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in DefaultKeyNameSubstrings)
        {
            if (!string.IsNullOrWhiteSpace(s))
            {
                set.Add(s.Trim());
            }
        }

        if (additionalSubstrings is not null)
        {
            foreach (var s in additionalSubstrings)
            {
                if (!string.IsNullOrWhiteSpace(s))
                {
                    set.Add(s.Trim());
                }
            }
        }

        return set.ToList();
    }
}