# Operator guide — human review workflow

This document describes how operators use the HTTP API to triage runs that stop in **RequiresReview**. Values shown are examples; substitute real hosts, TLS, and auth configuration for your deployment.

## Prerequisites

- Identity mode allows governance reads/writes for your operator principal (see `docs/security/auth-boundary.md`).
- Actor header mode requires `X-Agentor-Actor-Id` with a valid GUID on mutating governance calls.

## 1. Produce a RequiresReview run (deterministic API fixture)

The stock PR1 path executes `pr1.fake-tool`. To force **RequiresReview** without seeding the database, configure runtime policy so that tool requires review (for example via `Agentor:RuntimePolicy:ActiveProfile:RequiresReviewToolKeys` including `pr1.fake-tool`). Integration tests use this pattern in `ReviewInboxWorkflowApiTests`.

Then:

```http
POST /api/v1/agent-runs HTTP/1.1
Content-Type: application/json

{
  "agentName": "ReviewFixtureAgent",
  "objective": "Exercise governance inbox.",
  "traceId": "operator-review-demo"
}
```

Expect **202 Accepted** and a body whose `status` is **RequiresReview**.

## 2. Open the pending inbox

```http
GET /api/v1/reviews/pending?skip=0&take=50 HTTP/1.1
```

The response includes **items** (pending runs), **totalCount**, **skip**, and **take**. Each item exposes **reviewReason** (aligned with the run’s suspended reason text).

## 3. Apply a decision (alias surface)

```http
POST /api/v1/reviews/{runId}/decisions HTTP/1.1
X-Agentor-Actor-Id: {operator-guid}
Content-Type: application/json

{
  "kind": "Approve",
  "note": null
}
```

Supported kinds match governance semantics: **Approve**, **Reject**, **RequestChanges**, **Escalate**. The canonical endpoint `POST /api/v1/agent-runs/{runId}/human-review` behaves the same.

## 4. Verify inbox movement

- After **Approve** or **Reject**, the run leaves **RequiresReview** (typically **Completed** or **Failed**); it should disappear from the next `GET /reviews/pending` page (totalCount decreases).
- **RequestChanges** / **Escalate** leave the run reviewable; the item typically remains visible.

## Related docs

- Governance boundary: `docs/GOVERNANCE_BOUNDARY.md`
- Auth matrix: `docs/security/auth-boundary.md`
