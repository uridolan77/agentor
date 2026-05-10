# Phase 15 — secret redaction

Agentor treats **credential-shaped JSON** as unsafe for emission unless explicitly redacted.

## Types

- `SensitiveFieldCatalog` — default key-name substrings (`apiKey`, `secret`, `password`, `token`, `authorization`, `bearer`, `credential`) merged with `Agentor:AuditExport:SensitiveKeySubstrings`.
- `RedactionPolicy` — merged substring list + replacement token (default `[REDACTED]`).
- `RedactionResult` — count of redacted properties and diagnostic paths (RFC 6901-style segments).
- `JsonRedaction.Apply(JsonNode, RedactionPolicy)` — recursive walk; replaces values where **property names** contain configured substrings (case-insensitive).

## Where it runs

- **Audit export** (`GET .../audit-export`) uses `RedactionPolicy.FromAuditExportOptions(...)`.
- **Evaluation CI JSON** (`evaluation-report.json` from `EvaluationReportGenerator.BuildJson`) uses `RedactionPolicy.CatalogDefault` so harness artifacts never carry credential-shaped keys.

## Why key-based (not value scanning)

Deterministic behavior, stable hashes for audit packets, and predictable operator configuration without scanning arbitrary payloads for regex secrets (which is brittle and incomplete).

## Tests

`tests/Agentor.Application.Tests/Redaction/JsonRedactionTests.cs` and audit export tests in `GetRunAuditExportQueryHandlerTests.cs`.