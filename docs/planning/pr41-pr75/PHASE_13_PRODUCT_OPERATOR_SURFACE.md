# Phase 13 — Product/API and operator surface

## Purpose

Expose Agentor as an operable product surface rather than only a backend runtime kernel.

## Doctrine

```text
APIs expose runtime truth; UI does not invent business logic.
Read-only operator surfaces come before mutation-heavy dashboards.
Management APIs must validate but not execute plans by accident.
```

## PR61 — Recipe/plan/skill management APIs

### Goal

Add management APIs for declarative artifacts.

### Endpoints

```text
GET/POST /api/v1/recipes
GET /api/v1/recipes/{id}
GET/POST /api/v1/plans
GET /api/v1/plans/{id}
GET/POST /api/v1/skills
GET /api/v1/skills/{key}/{version}
GET/POST /api/v1/policy-profiles
```

### Acceptance

- Create/list/get endpoints exist.
- Validation errors are structured.
- Creating a recipe/plan/skill never executes it.
- Versioning semantics are explicit.

## PR62 — Run timeline and replay APIs

### Goal

Make run execution inspectable.

### Endpoints

```text
GET /api/v1/runs/{id}/timeline
GET /api/v1/runs/{id}/coordination-view
GET /api/v1/runs/{id}/audit-packet
```

### Acceptance

- Timeline orders trace events deterministically.
- Skill invocations group segments and inner tool calls.
- Policy/review/tool/model/MCP/external-agent events are visible.
- No secrets exposed.

## PR63 — Operator dashboard shell

### Goal

Add minimal dashboard shell or API-ready dashboard DTOs.

### Modules

```text
Runs
Plans
Skills
Policies
Reviews
Integrations
Quality
```

### Acceptance

- Read-only first.
- No dashboard-only business logic.
- Dashboard consumes API/query models.

## PR64 — Human review inbox API/UI

### Goal

Make review workflow operable.

### Acceptance

- List pending review requests.
- Approve/reject/request changes.
- Review actions record actor context.
- Approval resumes execution only through governance path.

## PR65 — Documentation and examples

### Goal

Add product-grade documentation.

### Add

```text
docs/examples/
docs/api/
docs/operator/
docs/developer/
```

### Acceptance

Examples cover:

- start a run;
- execute a plan;
- define a recipe;
- define a skill package;
- invoke a Conexus model tool;
- attach Athanor evidence provenance;
- submit an Athanor candidate;
- queue review;
- approve review;
- inspect audit packet.

No example may imply that Agentor canonizes knowledge.

## Phase 13 exit criteria

- Management APIs exist.
- Timeline/replay APIs exist.
- Operator surface exists.
- Review inbox is usable.
- Documentation/examples are coherent and boundary-safe.
