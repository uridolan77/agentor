# Agentor harness progress

## Phase 12 + PR60.6 (2026-05-10)

Completed: PR60.6 hardens HTTP integration retries: ResilientIntegrationDelegatingHandler buffers request bodies once and sends a cloned HttpRequestMessage per attempt (safe POST/JSON replays). Added ResilientIntegrationDelegatingHandlerTests. Harness: phase **12**, harnessPass **PR60.6**; eature-list.json note punctuation normalized.

- Run queue remains **in-memory only** (no durable queue broker in this repo yet).
- Outbox: OutboxDispatcher exists for application-triggered dispatch; there is **no hosted background outbox worker** unless explicitly added later.

Next harness marker: Phase 13 product operator surface (not started in this pass).

## Phase 12 + PR60.5 (2026-05-10)

Completed: durable execution and reliability slice PR56 through PR60 is implemented in-tree; harness reconciled to phase **12**, harnessPass **PR60.5** (superseded by PR60.6 marker above).

- PR56: Run queue (RunQueueOptions, IRunQueue, InMemoryRunQueue, background worker), API POST/GET /api/v1/agent-runs/queued.
- PR57: Outbox models, IOutboxStore, OutboxDispatcher, in-memory + EF stores.
- PR58: IRunExecutionLeaseStore, IDistributedOperationLedger, in-memory + EF implementations.
- PR59: TransportResilienceRegistry, ResilientIntegrationDelegatingHandler, integration status exposure for HTTP resilience.
- PR60: EF entities and manual migration 20260512080000_Phase12Reliability; Sqlite round-trip tests for outbox, leases, ledger; ledger clears tracker after successful commit.
- PR60.5: Harness alignment, OutboxDispatcherTests, TransportResilienceRegistryTests, Phase12EfRoundTripTests, verification log.

## Phase 11 + PR55.5 (2026-05-10)

Completed: governance slice PR51 through PR55 is implemented in-tree; harness reconciled to phase 11, harnessPass PR55.5.

- PR51: Tenant/workspace/project/knowledge-scope value types on AgentRun and start path; ResolveAthanorProjectId prefers explicit project id over profile id; Athanor command handlers use resolved project id.
- PR52: RuntimePolicyOptions.ActiveProfile (PolicyProfileRules): deny/allow lists, risk ceiling, model-call budget caps, MCP and external-agent denied tool keys; evaluator prefers profile when present. Full versioned PolicyBundle remains deferred (acceptance item PR52-004 false).
- PR53: HumanReviewDecision, kinds, resolution status; ApplyHumanReviewDecision and handler for approve, reject, request-changes, escalate; multi-step executor semantics beyond single pending tool deferred (PR53-005 false).
- PR54: ICurrentActorAccessor and HeaderOrFakeActorAccessor: X-Agentor-Actor-Id when set; fixed local-dev fallback when header absent (documented in docs/GOVERNANCE_BOUNDARY.md).
- PR55: Deterministic audit export JSON, SHA-256 header, baseline key-name redaction.
- PR55.5: Harness files (current-pr.md, feature-list.json, acceptance rows), expanded tests, verification-log.md, docs/GOVERNANCE_BOUNDARY.md.

Follow-up: Phase 12 (PR56–PR60) completed in the Phase 12 + PR60.5 harness pass; PR60.6 retry hardening documented above.

## Phase 10 + PR50.5 (2026-05-10)

Completed: integration modes Fake/Http/Disabled; HTTP adapters for Athanor, Conexus, MCP, external agents; /health, /ready, GET /api/v1/integrations/status; readiness treats non-2xx as not ready; Disabled adapters report detail disabled; harness phase 10 / PR50.5.

## Earlier phases

See verification-log.md and git history for PR25.5 through Phase 9 harness batches.
