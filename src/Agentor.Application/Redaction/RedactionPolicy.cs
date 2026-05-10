using Agentor.Application.Options;

namespace Agentor.Application.Redaction;

public sealed record RedactionPolicy(
    IReadOnlyList<string> KeyNameSubstrings,
    string ReplacementToken = "[REDACTED]")
{
    public static RedactionPolicy FromAuditExportOptions(AuditExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var merged = SensitiveFieldCatalog.MergeWithConfigured(options.SensitiveKeySubstrings);
        return new RedactionPolicy(merged);
    }

    public static RedactionPolicy CatalogDefault { get; } =
        new(SensitiveFieldCatalog.MergeWithConfigured(null));
}