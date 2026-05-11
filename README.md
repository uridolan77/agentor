# Agentor

Deterministic, observable, policy-governed agent execution runtime.

## What Agentor is

Agentor coordinates **runs**, **steps**, **tools**, **skills** (declared packages), **policy decisions**, **execution traces**, **run manifests**, evaluation hooks, and **integration ports** (Athanor-shaped knowledge access, Conexus-shaped model gateway, MCP registry, external-agent protocols). Every tool invocation is intended to flow through **policy evaluation** and **trace emission**.

See `AGENTS.md` for contributor rules and `docs/ARCHITECTURE.md` for layer boundaries.

## What Agentor is not

- Not a chatbot product, generic RAG system, or canonical knowledge engine  
- Not a replacement for **Athanor** (knowledge authority) or **Conexus** (model gateway)  
- Not an MCP marketplace or a thin wrapper around Microsoft Agent Framework, Semantic Kernel, LangGraph, AutoGen, CrewAI, or A2A — those remain **adapters** behind ports

## Current capabilities

- HTTP API (`Agentor.Api`) for starting runs, reading traces/steps, governance (review, audit export), management CRUD (recipes, plans, skills, policy profiles), operator surfaces, and integration status  
- Sequential plan execution with policy, skills, session memory bounds, human review suspension, and multi-step resume (in Application — see limitations for public defaults)  
- Enterprise-style **policy bundles** and profiles; runtime evaluator with allow / deny / requires-review  
- Durable queue, outbox, execution leases, EF persistence option (PostgreSQL), structured audit export and redaction tooling  
- Auth modes (**Fake** / **Header** / **Jwt**) with endpoint-level permission checks on sensitive routes (full uniform ASP.NET authorization for every route is still evolving — see `docs/security/auth-boundary.md`)

## Current limitations

For **ground truth** on behavior (public run routing, policy scopes, persistence, Jwt assumptions), read **`docs/REPO_TRUTH.md`**. Highlights:

- Public **`POST /api/v1/agent-runs`** routes through **`IAgentRunOrchestrator`**. Default **implicit legacy** remains available via **`Agentor:PublicRuns:TreatMissingExecutionSelectorAsLegacyFakeTool`** (see `appsettings.json`); turn it off in production to force explicit selectors.  
- **Durable queue** persistence replays orchestration selectors and structured **`ToolPayload`** (`tool_payload_json`) per **`EfRunQueueStore`** — see **`docs/operator/queue-payloads.md`**.  
- **Jwt** without **`JwtAuthority`** is only for **`JwtAcceptUnvalidatedBearerTokens`** lab paths; see **`docs/security/auth-boundary.md`** and **`docs/security/SECURITY_RELEASE_CHECKLIST.md`**.

## Quickstart

Prerequisites: [.NET 9 SDK](https://dotnet.microsoft.com/download).

```powershell
dotnet restore Agentor.sln
dotnet build Agentor.sln --no-restore
dotnet test Agentor.sln --no-build
```

Run the API (from repo root):

```powershell
dotnet run --project src/Agentor.Api
```

Health check (with defaults, after the app listens):

```powershell
pwsh ./scripts/smoke.ps1 -BaseUrl http://localhost:8080
```

Release-oriented smoke (health, readiness, integrations, start run, trace, audit export, operator dashboard) — **`scripts/release-smoke.ps1`** (see **`docs/operator/release-smoke.md`**). OpenAPI and DTO contract snapshots — **`docs/api/API_CONTRACT_SNAPSHOT.md`**.

Docker and CI expectations are described in `docs/` and `.github/workflows/ci.yml`.

## API examples

Start a run (minimal body with default **implicit legacy** routing — see `Agentor:PublicRuns` in `appsettings.json` and `docs/REPO_TRUTH.md`):

```http
POST /api/v1/agent-runs HTTP/1.1
Content-Type: application/json

{
  "agentName": "demo-agent",
  "objective": "Hello"
}
```

Execute a specific governed tool (example: Conexus model completion in Fake integration mode):

```json
{
  "agentName": "demo-agent",
  "objective": "Summarize: hello",
  "toolKey": "conexus.model-complete"
}
```

List runs:

```http
GET /api/v1/agent-runs HTTP/1.1
```

Product-style aliases and governance endpoints are summarized in `docs/api/phase13-product-surface.md` and `docs/examples/phase13-workflows.md`.

## Architecture

```text
Api → Application + Contracts + Infrastructure
Infrastructure → Application + Domain
Application → Domain
Domain → (no outward dependencies)
```

Details: `docs/ARCHITECTURE.md`, `docs/COORDINATION_LAYER.md`, decisions under `decisions/`.

## Runtime model: Run → Step → Tool/Skill → Policy → Trace → Manifest

```text
Start run → steps → tool or skill invocation
  → policy decision (allow / deny / requires review)
  → trace events
  → run manifest (and eval hooks where configured)
```

Human review can suspend execution; approval paths can resume multi-step plans. Domain and Application tests document the behavior.

## Human review

Governed tools can yield **RequiresReview** (distinct from **Deny**). Operators apply decisions via governance APIs; see `docs/operator/review-workflow.md` and `docs/security/auth-boundary.md`.

## Integrations: Athanor, Conexus, MCP, external agents

- **Athanor**: knowledge state and provenance ports (`docs/ATHANOR_INTEGRATION_BOUNDARY.md`)  
- **Conexus**: model gateway port and governed model-call tools  
- **MCP**: registry/client as adapter surface (`docs/MCP_BOUNDARY.md`)  
- **External agents**: protocol-shaped clients behind tools  

Integration modes (Fake / Http / Disabled) and readiness: `docs/integrations/compatibility-matrix.md`, `GET /api/v1/integrations/status`.

## Development harness

Acceptance and verification state live under **`.agentor-harness/`** (`feature-list.json`, `verification-log.md`, `session-handoff.md`). Session closeout expects restore, build, test, and harness scripts — see `AGENTS.md`.

## Roadmap

High-level PR phases: `docs/ROADMAP.md`. Mid-term planning packs: **`docs/planning/pr76-125/Phase 23 - 31.md`**, **`docs/planning/pr76-125/Phase 32 - 40.md`**.

## History

The previous root README that described this repo as a **“Claude Code Package”** overlay is preserved as **`docs/history/PR1-PR40-package.md`**.
