# Integration compatibility matrix

This table summarizes how Agentor integration ports behave across **Fake**, **Http**, and **Disabled** adapter modes. HTTP paths are relative to each family’s configured `BaseUrl`.

## Legend

| Mode | Behavior |
|------|----------|
| **Fake** | In-process deterministic implementation; no network; suitable for tests and local dev. |
| **Http** | JSON HTTP client (`IHttpClientFactory` named client); expects remote contract described below. |
| **Disabled** | Port throws or returns a stable “disabled” outcome where applicable; readiness may still report `Ready: true` with `detail: disabled` (see integration status docs). |

## Athanor (`IKnowledgeStateClient`)

| Operation | Fake | Http | Disabled |
|-----------|------|------|----------|
| Latest snapshot | Seeded per `projectId` | `GET v1/projects/{projectId}/snapshots/latest` → DTO or **404 → null** | Throws / disabled |
| Canonical lookup | From seeded snapshot entries | `GET v1/projects/{projectId}/canonical/{key}` → DTO or **404 → null** | Disabled |
| Evidence search | Seeded hits | `GET v1/projects/{projectId}/evidence/search?query=` → array (non-2xx → error) | Disabled |
| Candidate submit | In-memory | `POST v1/projects/{projectId}/runs/{runId}/candidates` | Disabled |
| Review queue | In-memory | `POST v1/projects/{projectId}/candidates/{candidateId}/review-queue` body `{ actorId }` | Disabled |

**Required HTTP endpoints (when mode = Http):** the five URL patterns above under the configured Athanor base URL.

**Unsupported / intentionally absent:** there is **no** Canonize, promote, or merge-into-canon route on this port (`IKnowledgeStateClient`); canonization remains an Athanor-side concern outside Agentor’s adapter surface.

**Known limitations:** Agentor does not retry Athanor reads beyond the host `HttpClient` policy; error bodies are truncated in thrown `HttpRequestException` messages for safety.

## Conexus (`IModelGatewayClient`)

| Operation | Fake | Http | Disabled |
|-----------|------|------|----------|
| Model complete | Deterministic echo + token estimates | `POST v1/model/complete` with `ModelCallRequestDto` JSON | Throws |

**Request pass-through:** `prompt`, `modelId`, optional `promptProfileRef` / `modelProfileRef`, and optional **declared** `declaredCostUnits` / `declaredLatencyMs` are serialized on the wire when present (declared values are **pre-execution intent** for policy and routing hints; not provider SDK enforcement).

**Telemetry → manifest:** successful `conexus.model-complete` tool outputs are aggregated into `RunManifestModelTelemetry` in Application (`ModelCallTelemetryAggregator`); Domain stores aggregates only.

**Known limitations:** no OpenAI/Anthropic (or other) provider SDKs in Agentor; only this HTTP JSON contract or Fake/Disabled adapters.

## MCP (`IMcpRegistryClient`)

| Operation | Fake | Http | Disabled |
|-----------|------|------|----------|
| Server list | In-memory catalog | `GET v1/servers` | Disabled |
| Tool discovery | In-memory | `GET v1/servers/{serverId}/tools` | Disabled |
| Tool invoke | In-memory | `POST v1/servers/{serverId}/tools/{toolName}/invoke` | Disabled |

**Boundary:** MCP JSON-RPC wire types are **not** in `Agentor.Domain`; descriptors live under `Agentor.Application.Mcp`, and HTTP wire DTOs are private to `HttpMcpRegistryClient`.

**Tool registry:** MCP tools are registered under stable keys `McpToolKeys.Format(serverId, toolName)` (deterministic string format).

**Known limitations:** invalid `nominalRisk` strings in HTTP tool rows fall back to **Medium** risk.

## External agents (`IExternalAgentProtocolClient`)

| Operation | Fake | Http | Disabled |
|-----------|------|------|----------|
| Capability discovery | Fake / A2A fakes | `GET v1/capabilities?protocolKind={int}` | Disabled |
| Invocation | Fakes | `POST v1/invocations` | Disabled |

**Non-canon:** external outputs are treated as **non-canon evidence**; successful invoke tool output includes `nonCanon` / `isNonCanonEvidence` markers. Policy **Deny** or **RequiresReview** stops plan execution **before** the HTTP client or fake invoke path runs for that tool call.

**Known limitations:** protocol conformance beyond this JSON shape is not claimed by these adapters.

## Related documentation

- `docs/ATHANOR_INTEGRATION_BOUNDARY.md`
- `docs/CONEXUS_INTEGRATION_BOUNDARY.md`
- `docs/MCP_BOUNDARY.md`
- Phase 10 planning: `docs/planning/pr41-pr75/PHASE_10_REAL_SERVICE_ADAPTERS.md`
