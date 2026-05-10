using Agentor.Contracts;

namespace Agentor.Application.Queries;

public static class AuditExportFormatParser
{
    public static bool TryParse(string? raw, out AuditExportFormatKind kind, out string? error)
    {
        error = null;
        var s = raw?.Trim();
        if (string.IsNullOrEmpty(s))
        {
            kind = AuditExportFormatKind.Canonical;
            return true;
        }

        switch (s.ToLowerInvariant())
        {
            case "canonical":
            case "json":
                kind = AuditExportFormatKind.Canonical;
                return true;
            case "pretty":
            case "prettyjson":
            case "pretty-json":
                kind = AuditExportFormatKind.Pretty;
                return true;
            case "redactionreport":
            case "redaction-report":
            case "redactions":
                kind = AuditExportFormatKind.RedactionReport;
                return true;
            case "hashonly":
            case "hash-only":
            case "sha256":
                kind = AuditExportFormatKind.HashOnly;
                return true;
            default:
                error = $"Unknown audit format '{raw}'.";
                kind = default;
                return false;
        }
    }
}
