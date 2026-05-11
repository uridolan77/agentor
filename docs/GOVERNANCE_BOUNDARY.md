# Governance boundary (Phase 11 baseline)

This document records non-goals and safety posture for the Phase 11 governance slice (PR51 through PR55) and PR55.5 harness hardening. It does not replace product security review.

## Actor boundary (PR54 + Phase 19)

- `ICurrentActorAccessor` remains the actor boundary used by governance mutations.
- API auth mode is now explicit (`Agentor:Auth:Mode = Fake | Header | Jwt`).
- Fake mode is local/test oriented and blocked outside Development/Test by default unless explicitly overridden.
- Header mode requires a valid GUID in the configured actor header.
- Jwt mode resolves actor id/display name/role from configurable claims and does not require provider-specific SDK coupling.
- Actor resolution failures produce unauthorized API responses where endpoint authorization is enforced.

## Human review (PR53)

- Human review decisions are governance records on the run: they gate whether execution may continue; they do not canonize knowledge in Athanor or elsewhere.
- Approve may reopen a tool that was held in RequiresReview; it does not override an immediate policy Deny outcome on a later policy evaluation (post-approval policy still applies in **`ReviewedToolContinuationService`** / **`PlanResumeOrchestrator`** via **`ReviewPolicyReevaluationService`**).
- Reject fails the reviewed run. RequestChanges and Escalate record a decision without resuming tool execution in the current handler path; the run remains in RequiresReview until further action.
- Escalated workflow: approving without **`HumanGovernanceApprover`** (or **`System`**) role is rejected at the domain/application boundary with **`GovernanceApproverRequiredException`** and surfaces as **HTTP 403** with code **`GovernanceApproverRequired`** on governance review POST routes (other invalid human-review states remain **409** **`HumanReviewInvalid`**).
- Multi-step plan resume beyond the single pending-tool path is not fully specified here; treat that as a follow-up hardening area.

## Policy profiles (PR52)

- RuntimePolicyOptions.ActiveProfile (PolicyProfileRules) is a first runtime profile hook: deny/allow lists, risk ceiling, model-call budget caps, MCP denied tool keys, and external-agent denied tool keys.
- It is not a versioned enterprise PolicyBundle, not a centralized policy distribution service, and not a full authorization engine.

## Endpoint authorization (Phase 19)

- Governance and policy endpoints now enforce permissions through `IAuthorizationDecisionService`.
- `RequiresReview` workflow decisions require governance write permission.
- Policy bundle creation/activation requires policy bundle write permission.
- Audit export requires audit read permission.
- Default role posture: `Service` is read-only; mutating governance actions require `HumanOperator` or `System`.

See `docs/security/auth-boundary.md` for the current auth mode and permission model.

## Audit export (PR55)

- GET /api/v1/agent-runs/{runId}/audit-export returns deterministic minified JSON and an X-Agentor-Audit-Content-SHA256 header over the UTF-8 encoding of that JSON.
- Redaction is baseline key-name substring redaction (apiKey, secret, password, token, configurable via Agentor:AuditExport). It does not guarantee secret hygiene for arbitrary payloads (values are not scanned; nested structures depend on property names).
