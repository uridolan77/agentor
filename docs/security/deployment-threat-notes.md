# Deployment Threat Notes (Phase 19 Auth)

This note captures operational risks and deployment controls for Agentor auth modes.

## Threats

1. Fake mode in production

- Risk: any caller is effectively treated as a trusted operator.
- Control: startup validation blocks Fake mode outside Development/Test unless explicitly overridden.

2. Header spoofing in Header mode

- Risk: clients can forge actor identity header if perimeter trust is weak.
- Control: only use Header mode behind a trusted gateway that rewrites/locks identity headers.

3. Invalid JWT claim mapping

- Risk: actor id or role cannot be resolved correctly, leading to access confusion.
- Control: configure claim mappings explicitly and treat unresolved actor id or role as unauthorized.

4. Alias endpoint authorization bypass

- Risk: alias endpoints can bypass canonical endpoint permission checks if not guarded consistently.
- Control: apply the same endpoint authorization checks to canonical and alias routes (`/agent-runs/*` and `/runs|/reviews` aliases).

5. Over-privileged service actors

- Risk: machine identities may perform governance mutations.
- Control: default role mapping keeps `Service` as read-only in `RoleBasedAuthorizationDecisionService`.

6. Audit data exposure

- Risk: unauthorized actors retrieving audit exports.
- Control: `GET /audit-export` now requires `AuditRead` permission.

## Deployment recommendations

1. Prefer `Jwt` mode for production.
2. Keep `Fake` mode disabled in production unless under explicit break-glass procedure.
3. If using `Header` mode, terminate trust at a single ingress and strip inbound identity headers from external clients.
4. Pin and review claim mappings as part of release validation.
5. Include authz endpoint tests for both canonical and alias routes in CI gates.

## Out of scope

- Identity provider onboarding details (tenant apps, consent flows, secret rotation) are deployment-specific and intentionally out of repository scope.
- Policy scope filtering (`SCOPE-001`) remains deferred to v1.1.
