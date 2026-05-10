
---

# Phase 21 — Integration contract conformance

**PR101–PR105**

Purpose: move from “HTTP adapters exist” to “HTTP adapters are contract-tested against expected wire behavior.”

## PR101 — Athanor contract test pack

Add fake server / fake handler contract tests for:

```text
latest snapshot
canonical lookup
evidence search
candidate submit
review queue
error mapping
```

Acceptance:

```text
- 404/null behavior covered.
- Non-2xx error handling covered.
- No Canonize route exists.
```

## PR102 — Conexus contract test pack

Cover:

```text
model complete
telemetry fields
budget declaration pass-through
timeout/error mapping
```

Acceptance:

```text
- No provider SDKs.
- Model telemetry maps to manifest.
```

## PR103 — MCP contract test pack

Cover:

```text
server list
tool discovery
tool invoke
protocol error mapping
```

Acceptance:

```text
- MCP protocol types remain outside Domain.
- Tool registry mapping deterministic.
```

## PR104 — External-agent / A2A-style contract test pack

Cover:

```text
capability discovery
agent invocation
non-canon flag
error mapping
```

Acceptance:

```text
- External output remains non-canon.
- Deny/review still prevents invocation.
```

## PR105 — Integration compatibility matrix

Add:

```text
docs/integrations/compatibility-matrix.md
```

Acceptance:

```text
- Fake/HTTP/Disabled behavior table.
- Required endpoints per integration.
- Known unsupported features.
```