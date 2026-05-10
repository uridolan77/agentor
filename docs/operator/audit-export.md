# Operator guide — audit export and audit packet

Audit exports are **redacted, deterministic JSON** snapshots of run execution evidence. They are suitable for retention and integrity checks when combined with the content hash header.

## Endpoints

| Route | Permission | Notes |
|-------|------------|-------|
| `GET /api/v1/agent-runs/{runId}/audit-export` | AuditRead | Canonical governance path. |
| `GET /api/v1/runs/{runId}/audit-packet` | AuditRead | Alias of the same handler/payload family. |

Both return `Content-Type: application/json; charset=utf-8` and expose:

`X-Agentor-Audit-Content-SHA256`: uppercase hex SHA-256 over **UTF-8 bytes of the canonical minified audit JSON** (after redaction).

## Format query (`format`)

Optional query parameter (case-insensitive):

| Value | Response body | Hash header |
|-------|----------------|------------|
| `canonical` (default) | Minified canonical audit JSON | SHA-256 of that JSON |
| `pretty` | Indented JSON with identical content | Same SHA-256 as canonical |
| `redactionReport` | Pretty JSON document listing redacted JSON pointer paths + counts | Same SHA-256 as canonical |
| `hashOnly` | Small JSON `{"schemaVersion":"agentor.audit.hashOnly.v1","runId":"...","contentSha256Hex":"..."}` | Matches embedded hex |

Unknown values return **400 Bad Request** with `AuditExportFormatInvalid`.

## Redaction

Sensitive **property names** are replaced with a fixed token (see `docs/developer/phase15-redaction.md`). The redaction report variant exposes `/`-style paths for operator visibility **without** echoing secret values.

## Practical checks

1. Export twice with `format=canonical` — bodies and headers should match byte-for-byte on stable stores.
2. Compare `format=pretty` header to `format=canonical` — headers must match even though bodies differ.
3. Use `format=hashOnly` for lightweight polling; verify bytes against a downloaded canonical export when needed.

No throughput or latency targets are claimed here; measure in your environment.
