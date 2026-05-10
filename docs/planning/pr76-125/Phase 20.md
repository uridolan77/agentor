
---

# Phase 20 — Durable operational runtime

**PR96–PR100**

Purpose: harden background execution beyond in-memory queues and first-pass outbox.

## PR96 — Durable run queue abstraction

Add durable queue interface/implementation:

```text
IDurableRunQueue
RunQueueRecord
EfRunQueueStore
```

Acceptance:

```text
- Enqueue persists queue item.
- Worker can claim item.
- Queue survives process restart in tests.
```

## PR97 — Hosted run worker

Add:

```text
RunQueueHostedService
RunWorkerOptions
```

Acceptance:

```text
- Hosted worker disabled by default.
- Config enables worker.
- Worker respects leases.
```

## PR98 — Hosted outbox dispatcher

Add:

```text
OutboxHostedService
OutboxDispatchOptions
```

Acceptance:

```text
- Disabled by default.
- Dispatches pending outbox messages when enabled.
- Poison/retry behavior tested.
```

## PR99 — Atomic outbox claim and concurrency tokens

Tighten the current EF outbox.

Acceptance:

```text
- TryMarkDispatching uses atomic conditional update or concurrency token.
- Competing dispatchers cannot double-dispatch.
- Tests simulate contention.
```

## PR100 — Operational status endpoints

Add:

```text
GET /api/v1/ops/queue
GET /api/v1/ops/outbox
GET /api/v1/ops/leases
```

Acceptance:

```text
- Read-only.
- No secrets.
- Useful for operator dashboard.
```
