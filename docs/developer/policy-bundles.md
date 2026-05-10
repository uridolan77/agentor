# Policy Bundles — Developer Guide

Phase 17 (PR81–PR85) replaces the flat `PolicyProfileRules` config with a versioned, auditable, composable policy system.

## Concepts

### PolicyBundle

A `PolicyBundle` is a named, versioned set of `PolicyRule` objects.

- Versioned by `PolicyBundleVersion` (`major.minor`, e.g. `"1.0"`).
- Immutable after publication — rules cannot be modified once `Publish()` is called.
- Created via `PolicyBundle.Create(...)`. Published via `bundle.Publish(now)`.
- Bundles created through the API are immediately published.
- Duplicate rule IDs within a bundle are rejected on creation.

### PolicyRule

Each rule declares:

| Field | Type | Purpose |
|---|---|---|
| `Kind` | `PolicyRuleKind` | ToolAccess \| ModelBudget \| McpToolAccess \| ExternalAgentAccess |
| `Scope` | `PolicyRuleScope` | Global \| Tenant \| Workspace \| Project \| KnowledgeScope |
| `Effect` | `PolicyRuleEffect` | Allow \| Deny \| RequiresReview |
| `TargetKey` | `string?` | Tool key, MCP key, agent key, or budget dimension |
| `ThresholdValue` | `string?` | Numeric threshold for budget rules (invariant-culture) |

Factory helpers keep call sites readable:

```csharp
PolicyRule.ToolAllow(id, "my.tool")
PolicyRule.ToolDeny(id, "my.tool")
PolicyRule.ToolRequiresReview(id, "my.tool")
PolicyRule.McpToolDeny(id, "mcp.server.tool")
PolicyRule.ExternalAgentDeny(id, "external-agent.invoke")
PolicyRule.ModelBudgetMaxCost(id, 5m)
PolicyRule.ModelBudgetMaxLatency(id, 500)
```

### ActivePolicyProfile

`ActivePolicyProfile` is the runtime marker that links a named profile to a specific bundle version. The evaluator reads this at call time to resolve which bundle's rules apply.

## Evaluation order

When an active policy profile with a bundle is configured, `RuntimePolicyEvaluator` evaluates in this order:

1. **Tool not registered** → `Deny` (UNKNOWN_TOOL)
2. **Deny list** → `Deny` (TOOL_DENIED)
3. **MCP deny list** → `Deny` (MCP_TOOL_DENIED)
4. **External-agent deny list** → `Deny` (EXTERNAL_AGENT_TOOL_DENIED)
5. **Allow list enforcement** → `Deny` (TOOL_NOT_ALLOWED) when list is non-empty and tool is absent
6. **Model budget gates** → `Deny` (BUDGET_DECLARED_COST / BUDGET_DECLARED_LATENCY)
7. **Bundle-driven RequiresReview keys** → `RequiresReview` (TOOL_REVIEW_REQUIRED)
8. **Risk-level threshold** → `RequiresReview` (TOOL_RISK_REVIEW)
9. **Allow** (RUNTIME_ALLOW)

`RequiresReview` and `Deny` are always distinct outcomes. A `RequiresReview` decision can be resumed after human approval; a `Deny` cannot.

## Scoped rule merging (Phase 26)

When an active bundle is resolved for a run, `PolicyBundleRulesAdapter.ToProfileRules(bundle, AgentRunScope)` keeps rules whose scope matches the run’s `TenantId` / `WorkspaceId` / `ProjectId` / `KnowledgeScopeId`, then merges overlapping tool-access rules by **specificity** (**KnowledgeScope → Project → Workspace → Tenant → Global**). At **equal** specificity, outcomes merge as **Deny > RequiresReview > Allow**.

**Security note:** a **more-specific Allow** can therefore override a **Global Deny** for the same tool key—the narrower scope wins. Treat Global denies as deployment-wide defaults, not absolute blocks, unless higher-specificity rules are curated to match.

## Fallback path

When no `ActivePolicyProfile` is set, the evaluator falls back to `RuntimePolicyOptions` (including its optional `ActiveProfile` field). This preserves full backward compatibility with the existing flat-config approach from PR52.

## API

### Create a bundle

```
POST /api/v1/policy-bundles
Content-Type: application/json

{
  "name": "Production Safety v1",
  "version": "1.0",
  "rules": [
    { "kind": "ToolAccess", "scope": "Global", "effect": "Deny", "targetKey": "dangerous.tool", "description": "Deny dangerous tool." },
    { "kind": "ModelBudget", "scope": "Global", "effect": "Deny", "targetKey": "declaredCostUnits", "thresholdValue": "10", "description": "Cap cost at 10 units." }
  ]
}
```

Creating a bundle does **not** activate it. Returns the bundle ID.

### Retrieve a bundle

```
GET /api/v1/policy-bundles/{id}
GET /api/v1/policy-bundles          (list all)
```

### Activate a profile on a bundle

First create a policy profile via `POST /api/v1/policy-profiles` (existing endpoint).
Then activate it, binding it to a published bundle:

```
POST /api/v1/policy-profiles/{profileId}/activate
Content-Type: application/json

{
  "bundleId": "<bundle-guid>",
  "bundleVersion": "1.0"
}
```

Activation is explicit and audited (`ActivatedAt`, `ActivatedBy`). The evaluator immediately uses the new bundle rules for all subsequent tool calls.

## Audit export

When a policy profile is active, `GET /api/v1/agent-runs/{id}/audit-export` includes a `policyIdentity` section:

```json
"policyIdentity": {
  "profileId": "...",
  "profileName": "Production Safety v1",
  "bundleId": "...",
  "bundleVersion": "1.0",
  "activatedAt": "2026-05-10T12:00:00Z",
  "activatedBy": "..."
}
```

When no profile is active, `policyIdentity` is omitted from the JSON.

Canonical audit exports always include a root-level **`effectivePolicyScope`** object (`tenantId`, `workspaceId`, `projectId`, `knowledgeScopeId`) documenting the run identity used when resolving scoped bundle rules.

## Test fixtures

Deterministic fixture files live under `tests/Agentor.Application.Tests/fixtures/policy/`:

| File | Description |
|---|---|
| `allow-bundle.json` | Single Allow rule; proves Effect=Allow adapter path |
| `deny-bundle.json` | Tool deny + model budget deny; proves both Deny paths |
| `review-bundle.json` | Tool-specific RequiresReview + MCP deny; proves distinct outcomes |

## Scoped enforcement (Phase 26)

Rules are filtered by **run identity** before they influence `PolicyProfileRules`:

| `PolicyRuleScope` | Matches when |
|---|---|
| `Global` | Always |
| `Tenant` | `ScopeTenantId` equals run `TenantId` |
| `Workspace` | Tenant + workspace ids match |
| `Project` | Tenant + workspace + project ids match |
| `KnowledgeScope` | `ScopeKnowledgeScopeId` equals run `KnowledgeScopeId` |

**Precedence:** more specific scope wins over broader scope. If two rules apply at the **same** specificity for the same tool key, **Deny** beats **RequiresReview** beats **Allow**.

API requests (`CreatePolicyRuleDto`) accept optional `scopeTenantId`, `scopeWorkspaceId`, `scopeProjectId`, `scopeKnowledgeScopeId`; combinations must satisfy the domain validation on `PolicyRule`.

## Architecture boundaries

- **Agentor.Domain.Policy**: `PolicyBundle`, `PolicyRule`, `PolicyProfile`, `ActivePolicyProfile`, value objects and enums. No infrastructure dependencies.
- **Agentor.Application.Abstractions**: `IPolicyBundleRepository`, `IPolicyProfileRepository` interfaces; `PolicyEvaluationRequest` carries `AgentRunScope` for scoped bundle resolution.
- **Agentor.Infrastructure.Policy**: `PolicyBundleRulesAdapter` (bundle + run scope → `PolicyProfileRules`), `InMemoryPolicyBundleRepository`, `InMemoryPolicyProfileRepository`.
- **Agentor.Infrastructure.RuntimePolicyEvaluator**: Bundle-aware via active profile + `AgentRunScope` on each evaluation. Falls back to `RuntimePolicyOptions` when no active profile is set.
- **Agentor.Contracts**: `PolicyBundleDtos.cs` — request/response DTOs (including optional scope identifier fields on rules).
- **Agentor.Api.Endpoints.PolicyBundleEndpoints**: Four management endpoints; no domain logic.
