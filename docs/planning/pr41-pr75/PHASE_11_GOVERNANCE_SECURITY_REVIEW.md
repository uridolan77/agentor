# Phase 11 — Governance, tenancy, security, and human review

## Purpose

Make Agentor safe for governed, multi-tenant, reviewable operation.

## Doctrine

```text
Governance decisions are explicit runtime artifacts.
Review can approve execution, but it does not canonize knowledge.
Deny cannot be converted into execution by ordinary approval.
Actor identity must be captured for mutating governance actions.
```

## PR51 — Tenant/project/workspace identity model

### Goal

Replace temporary identity conventions with explicit domain/application identity concepts.

### Add

```text
TenantId
WorkspaceId
ProjectId
KnowledgeScopeId
ActorId
```

### Acceptance

- Athanor calls no longer rely on `ProfileId-as-projectId` as a permanent convention.
- Backward compatibility path exists.
- API and command DTOs carry explicit scope where required.
- Tests cover scope propagation.

## PR52 — Policy bundles and policy profiles

### Goal

Move beyond flat runtime options into composable policy configuration.

### Suggested types

```text
PolicyBundle
PolicyProfile
ToolPolicyRule
ModelPolicyRule
McpPolicyRule
ExternalAgentPolicyRule
BudgetPolicyRule
ReviewPolicyRule
PolicyEvaluationContext
```

### Acceptance

- RuntimePolicyEvaluator can evaluate profile-based policy.
- Existing options still work as default policy profile.
- Tests cover tool risk, model budgets, MCP tool policies, and external-agent policies.

## PR53 — Human review workflow v1

### Goal

Add first-class review request and decision lifecycle.

### Suggested types

```text
ReviewRequest
ReviewDecision
ReviewDecisionKind = Approve | Reject | RequestChanges | Escalate
ReviewResolutionStatus
```

### Acceptance

- `RequiresReview` run/plan/tool outcomes can resume only after explicit approval.
- `Deny` cannot be resumed as ordinary approval.
- Review decisions are traceable and auditable.
- Approval permits execution continuation; it does not canonize output.

## PR54 — Actor/auth boundary

### Goal

Add actor context without committing to a full identity provider.

### Add

```text
ICurrentActorAccessor
ActorContext
ActorRole
FakeCurrentActorAccessor
```

### Acceptance

- Mutating governance actions require actor ID.
- Candidate/review/external-agent actions capture actor ID.
- Local fake actor works in development/tests.
- No full auth provider implementation yet.

## PR55 — Deterministic audit export

### Goal

Export complete run audit packets.

### Audit packet includes

```text
Run
Plan
Plan steps
Agent steps
Tool calls
Policy decisions
Review decisions
Athanor provenance
Conexus telemetry
MCP calls
External-agent calls
Session memory summary
Manifest hash
Quality summary
```

### Acceptance

- JSON export endpoint or query handler exists.
- Packet hash is deterministic.
- Redaction boundary exists.
- Tests prove secrets/sensitive configured keys are not exported.

## Phase 11 exit criteria

- Explicit scope identity exists.
- Policy profiles exist.
- Human review workflow exists.
- Actor context exists.
- Deterministic audit export exists.
