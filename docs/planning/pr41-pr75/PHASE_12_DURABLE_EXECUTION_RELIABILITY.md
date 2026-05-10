# Phase 12 — Durable execution and reliability

## Purpose

Move Agentor from request-bound execution toward durable, queue-backed, recoverable execution.

## Doctrine

```text
Inline execution remains useful for tests.
Production execution should be queue-backed and idempotent.
Side effects should be outboxed.
Transport resilience is separate from tool-level retry semantics.
```

## PR56 — Background run queue

### Goal

Separate run acceptance from run execution.

### Suggested types

```text
IRunQueue
RunWorkItem
RunWorker
RunExecutionDispatcher
RunQueueOptions
```

### Acceptance

- API can enqueue run work.
- In-memory queue exists for tests.
- Inline mode remains available.
- Run state transitions remain valid under worker execution.

## PR57 — Durable outbox

### Goal

Record external side effects before dispatch.

### Suggested types

```text
OutboxMessage
OutboxStatus
OutboxDispatcher
OutboxMessageKind
OutboxAttempt
```

### Acceptance

- Athanor/Conexus/MCP/external-agent side effects can be outboxed.
- Dispatcher is idempotent.
- Retry and poison states are modeled.
- Tests cover dispatch success, retry, and poison.

## PR58 — Distributed idempotency and execution leases

### Goal

Prevent duplicate execution under multiple workers.

### Suggested types

```text
DistributedIdempotencyLedger
ExecutionLease
RunLock
LeaseStatus
```

### Acceptance

- Duplicate command does not double-execute.
- Competing workers cannot execute same run.
- Expired leases can be reclaimed safely.
- Tests cover duplicate, conflict, lease expiry, and lease renewal.

## PR59 — Transport resilience policies

### Goal

Add resilience for real HTTP adapters without duplicating ToolExecutionPipeline semantics.

### Suggested types

```text
TransportRetryPolicyOptions
CircuitBreakerOptions
BackoffStrategy
```

### Acceptance

- Applies to HTTP adapters only.
- ToolExecutionPipeline retry remains separate.
- Tests use fake HTTP handlers.
- Circuit-open behavior is visible in readiness/metrics.

## PR60 — Persistence hardening

### Goal

Persist all new Phase 9–12 runtime/governance/reliability entities.

### Acceptance

- EF mappings and migrations exist.
- Round-trip tests for policies, reviews, outbox, external calls, leases.
- Migration snapshot is clean.
- Existing in-memory test paths remain usable.

## Phase 12 exit criteria

- Queue-backed run execution exists.
- Durable outbox exists.
- Idempotency/leases are durable.
- Transport resilience exists.
- Persistence is round-trip tested.
