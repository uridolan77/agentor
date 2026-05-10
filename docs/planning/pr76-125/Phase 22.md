

---

# Phase 22 — Operator UX and workflow completeness

**PR106–PR110**

Purpose: make the API/operator surface usable for real review, monitoring, and debugging.

## PR106 — Review inbox completion

Close the current review-inbox limitation.

Acceptance:

```text
- API can produce a RequiresReview run fixture.
- Pending list shows it.
- Approve/reject/request changes through HTTP.
- Pending item disappears or changes state after decision.
```

## PR107 — Run timeline v2

Improve timeline grouping.

Acceptance:

```text
- Groups plan steps, skill invocations, inner tool calls, policy decisions, review decisions.
- Deterministic ordering.
- Tests cover a multi-step reviewed plan.
```

## PR108 — Operator dashboard v2

Add more useful read-only modules:

```text
queue
outbox
integration readiness
policy profile
quality warnings
deferred risks
```

Acceptance:

```text
- Dashboard remains read-only.
- No business logic lives only in dashboard.
```

## PR109 — Audit packet download variants

Add:

```text
JSON canonical
pretty JSON
redaction report
hash-only endpoint
```

Acceptance:

```text
- Same canonical hash.
- Redaction paths visible where allowed.
- No secrets exposed.
```

## PR110 — Operator docs and workflow examples

Add:

```text
docs/operator/review-workflow.md
docs/operator/debug-run.md
docs/operator/audit-export.md
```

Acceptance:

```text
- Step-by-step operator flows.
- Matches real endpoints.
- No overclaiming production SLOs.
```
