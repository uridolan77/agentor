namespace Agentor.Application.Options;

public sealed class AuditExportOptions
{
    public const string SectionName = "Agentor:AuditExport";

    /// <summary>
    /// Property names containing any of these substrings (case-insensitive) are redacted in audit export.
    /// Values are merged with the default sensitive-key catalog (<c>SensitiveFieldCatalog</c>).
    /// </summary>
    public List<string> SensitiveKeySubstrings { get; set; } = ["apiKey", "secret", "password", "token"];
}
