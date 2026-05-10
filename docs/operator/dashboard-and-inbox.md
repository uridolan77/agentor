# Operator dashboard and review inbox

## Dashboard (`GET /api/v1/operator/dashboard`)

The dashboard response is **read-only** and **API-shaped**: it exposes links to primary modules (runs, plans, skills, policies, reviews, integrations, quality) and simple counts where cheaply available (for example pending human reviews). It does not embed business rules beyond what existing query handlers already compute.

**Design rule:** UIs and operators consume this DTO plus the underlying resource APIs. No dashboard-only source of truth.

## Review inbox (`GET /api/v1/reviews/pending`)

Lists runs whose status is **RequiresReview**, newest first within paging. Each row includes identifiers and optional reason text loaded from the run record.

Decisions use **`POST /api/v1/reviews/{runId}/decisions`** with `ApplyHumanReviewRequestDto` (`Kind`, optional `Note`), mirroring `POST /api/v1/agent-runs/{runId}/human-review`. Successful approvals resume tool execution only through the governed `ApplyHumanReviewDecisionHandler` path (policy re-evaluated with review context).

## Actor context

Human review decisions require a non-empty actor id in the application handler. In local/dev configurations the API may supply a documented fallback actor when `X-Agentor-Actor-Id` is absent; production deployments should treat the header (or equivalent identity) as mandatory for auditability.

## Boundaries

Approving a review **does not** write canonical knowledge to Athanor. Any Athanor project or candidate flows remain explicit, adapter-mediated operations with their own contracts.
