# Phase 10 — Real service adapters and integration modes

## Purpose

Move from fake-only integrations toward configurable real HTTP/service adapters while keeping fake/local development as the default.

## Doctrine

```text
Real transports live in Infrastructure.
Application owns ports.
Domain remains transport-free.
Provider SDKs do not enter Agentor core.
Fake mode remains first-class.
Disabled mode fails clearly.
```

## PR46 — Integration mode configuration

### Goal

Add explicit adapter modes for all major integration families.

### Suggested options

```text
Agentor:Integrations:Athanor:Mode = Fake | Http | Disabled
Agentor:Integrations:Conexus:Mode = Fake | Http | Disabled
Agentor:Integrations:Mcp:Mode = Fake | Http | Disabled
Agentor:Integrations:ExternalAgents:Mode = Fake | Http | Disabled
```

### Acceptance

- Options classes and validation exist.
- Invalid modes fail startup validation.
- Fake remains default for local/test.
- Disabled adapters fail with clear error envelopes.
- No real HTTP clients yet.

## PR47 — Real Athanor HTTP client adapter

### Goal

Implement `HttpKnowledgeStateClient` behind the existing `IKnowledgeStateClient` port.

### Acceptance

- Implements only the existing port.
- No `Canonize`/`Promote`/`MergeIntoCanon` API.
- Uses typed options: base URL, timeout, headers, retry policy reference.
- Tests use fake HTTP message handler.
- All Athanor outputs remain evidence/candidate/review metadata unless Athanor itself canonizes externally.

### Non-goals

- No real secret management beyond configuration shape.
- No production auth provider.

## PR48 — Real Conexus HTTP client adapter

### Goal

Implement `HttpModelGatewayClient` behind `IModelGatewayClient`.

### Acceptance

- No direct provider SDKs.
- No OpenAI/Anthropic calls from Agentor.
- Contract preserves `ModelCallRequestDto` and `ModelCallResultDto` semantics.
- Telemetry still flows into manifest aggregation.
- Tests use fake HTTP handler.

## PR49 — Real MCP transport adapter

### Goal

Implement a real MCP transport boundary behind the Phase 8 `IMcpRegistryClient`.

### Acceptance

- Real transport is Infrastructure-only.
- Discovered MCP tools still become Agentor `ToolDefinition`s.
- MCP result envelopes remain tool outputs.
- No MCP protocol types in Domain.
- Tests use fake transport/server handler.

## PR50 — Integration health and readiness endpoints

### Goal

Expose integration health without leaking secrets.

### Add

```text
GET /health
GET /ready
GET /api/v1/integrations/status
```

### Acceptance

- Liveness is independent from dependency readiness.
- Readiness reports adapter modes and dependency status.
- Secrets, tokens, headers, and raw connection strings are never exposed.
- Tests cover Fake, Disabled, and failing HTTP-mode dependency.

## Phase 10 exit criteria

- Integration modes are explicit.
- Real HTTP adapters exist behind stable ports.
- Fake and Disabled modes remain testable.
- Health/readiness surfaces exist.
- Domain remains integration/transport-free.
