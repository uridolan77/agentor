# Agentor PR1–PR40 Index

## Phase 1 — Runtime kernel and API hardening
- PR01 — Runtime foundation stabilization
- PR02 — API contract hardening and DTO versioning
- PR03 — Run manifest determinism and trace hash
- PR04 — Validation, error model, and request tracing hardening
- PR05 — Configuration/options model and startup validation

## Phase 2 — Persistence, read models, and eval fixture baseline
- PR06 — EF Core persistence boundary
- PR07 — PostgreSQL persistence implementation
- PR08 — Run read model and query endpoints
- PR09 — Idempotency and command deduplication
- PR10 — Repository, integration, and eval fixture baseline

## Phase 3 — Tools and runtime policy
- PR11 — Tool registry and tool definitions
- PR12 — Runtime policy engine v1
- PR12.5 — Coordination layer and evaluation signature doctrine (docs/ADR only; arXiv:2605.03310)
- PR13 — Tool execution pipeline with timeout/retry/cancellation
- PR14 — Tool result envelopes and artifact references
- PR15 — Tool-call audit and policy denial surfaces

## Phase 4 — Plans, recipes, and execution orchestration
- PR16 — AgentPlan and AgentRecipe domain model
- PR17 — Sequential plan executor
- PR18 — Conditional and guarded step execution
- PR19 — Failure handling, retries, and compensation hooks
- PR20 — Run state machine hardening

## Phase 5 — Athanor integration
- PR21 — Athanor client port and fake implementation
- PR22 — Athanor read-only canonical state integration
- PR23 — Evidence search and provenance attachment
- PR24 — Candidate submission to Athanor
- PR25 — Review queue integration and non-canonization guard

## Phase 6 — Conexus integration
- PR26 — Conexus model gateway port and fake implementation
- PR27 — Model-call tool through Conexus
- PR28 — Prompt/model profile contract
- PR29 — Cost, latency, and budget policy
- PR30 — Model-call telemetry in run manifests

## Phase 7 — Skills, memory, and evaluation
- PR31 — Skill package model
- PR32 — Skill invocation pipeline
- PR33 — Session memory boundary
- PR34 — Evaluation harness and regression fixtures
- PR35 — Run quality gates and evaluation summaries

## Phase 8 — MCP, observability, and release
- PR36 — MCP client boundary and fake MCP registry
- PR37 — MCP tool discovery and binding
- PR38 — Observability: structured logs, metrics, traces
- PR39 — Deployment package, Docker, CI, and smoke tests
- PR40 — v0.1 release candidate hardening

## Post-v0.1
- PR41+ — A2A and external-agent protocol adapters
- PR42+ — Microsoft Agent Framework / Semantic Kernel adapters if justified
