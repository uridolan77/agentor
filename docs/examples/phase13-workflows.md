# Phase 13 example walkthrough (illustrative)

> All payloads are **illustrative**. Replace host, ids, and bodies with values from your environment. Agentor **does not** canonize knowledge; Athanor and Conexus remain separate services.

## A. Start a run (existing runtime API)

```http
POST /api/v1/agent-runs HTTP/1.1
Content-Type: application/json

{
  "agentName": "ExampleAgent",
  "objective": "Demonstrate Phase 13 read surfaces.",
  "traceId": "example-phase13-trace"
}
```

Assume response `202` with JSON containing `id` as `{runId}`.

## B. Operator dashboard

Requires **`OpsRead`** when endpoint authorization is enabled (same permission as `/api/v1/ops/*`). Ensure your actor principal maps to a role that includes `OpsRead` (default: `HumanOperator`, `System`; not `Service`).

```http
GET /api/v1/operator/dashboard HTTP/1.1
```

Use the returned module map as navigation metadata for a UI.

## C. Define a recipe and plan (management; no execution)

```http
POST /api/v1/recipes HTTP/1.1
Content-Type: application/json

{
  "name": "example.recipe",
  "version": "1.0.0",
  "topology": "SequentialPipeline",
  "steps": [
    { "sourceStepId": "s1", "orderIndex": 0, "kind": "Tool", "toolKey": "pr1.fake-tool" }
  ],
  "failureHandling": "FailFast",
  "notes": null
}
```

Capture `{recipeId}` from the response, then:

```http
POST /api/v1/plans HTTP/1.1
Content-Type: application/json

{
  "recipeId": "{recipeId}",
  "planId": null
}
```

This stores a plan artifact; it does **not** start execution.

## D. Timeline and audit packet aliases

```http
GET /api/v1/runs/{runId}/timeline HTTP/1.1
```

```http
GET /api/v1/runs/{runId}/audit-packet HTTP/1.1
```

Compare header `X-Agentor-Audit-Content-SHA256` with the same header from `GET /api/v1/agent-runs/{runId}/audit-export`.

## E. Conexus model tool (fake gateway)

When the Conexus integration mode is **Fake**, a model completion tool call still flows through **tool registration**, **policy evaluation**, and **trace emission**. It is an integration demonstration, not a production model deployment.

## F. Athanor evidence and candidates (non-canon)

Typical read paths (when enabled) look like:

```http
GET /api/v1/agent-runs/{runId}/athanor/evidence-provenance?... HTTP/1.1
```

```http
POST /api/v1/agent-runs/{runId}/athanor/candidates HTTP/1.1
```

These attach **candidate** or **evidence** material to the governed run record; they do not declare Athanor canonical truth inside Agentor.

## G. Review queue and decision

```http
GET /api/v1/reviews/pending HTTP/1.1
```

For a run in `RequiresReview`, record a decision (actor header shown):

```http
POST /api/v1/reviews/{runId}/decisions HTTP/1.1
Content-Type: application/json
X-Agentor-Actor-Id: 33333333-3333-4333-8333-333333333333

{
  "kind": "Approve",
  "note": null
}
```

Equivalent canonical route: `POST /api/v1/agent-runs/{runId}/human-review` with the same body.
