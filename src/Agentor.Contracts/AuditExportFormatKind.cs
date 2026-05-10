namespace Agentor.Contracts;

/// <summary>Response variant for audit export / audit-packet (Phase 22 PR109). Canonical hash always applies to minified canonical audit JSON.</summary>
public enum AuditExportFormatKind
{
    Canonical = 0,
    Pretty = 1,
    RedactionReport = 2,
    HashOnly = 3
}
