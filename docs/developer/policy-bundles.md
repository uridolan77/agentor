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
| `Scope` | `PolicyRuleScope` | Global \| Tenant \| Workspace \| Project |
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

## Test fixtures

Deterministic fixture files live under `tests/Agentor.Application.Tests/fixtures/policy/`:

| File | Description |
|---|---|
| `allow-bundle.json` | Single Allow rule; proves Effect=Allow adapter path |
| `deny-bundle.json` | Tool deny + model budget deny; proves both Deny paths |
| `review-bundle.json` | Tool-specific RequiresReview + MCP deny; proves distinct outcomes |

## Known limitations (Phase 17)

### PolicyRuleScope is modeled but not enforced

Each `PolicyRule` carries a `Scope` field (`Global`, `Tenant`, `Workspace`, `Project`). The domain model stores these values correctly.

**Phase 17 does not enforce scope.** `PolicyBundleRulesAdapter.ToProfileRules()` iterates all rules in the bundle and maps them into a flat `PolicyProfileRules` without filtering by run identity. A rule marked `Scope = Tenant` is applied just like a rule marked `Scope = Global`.

This is a deliberate deferral (see `docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md` item `SCOPE-001`). Scoped enforcement requires the evaluation path to receive run-identity context (`TenantId`, `WorkspaceId`, `ProjectId`) and apply scope filters at adapter time. That is a v1.1 concern.

**Do not assume scope filtering is active.** Any consumer that creates Tenant- or Project-scoped rules should be aware that in the current implementation those rules apply globally.

## Architecture boundaries

- **Agentor.Domain.Policy**: `PolicyBundle`, `PolicyRule`, `PolicyProfile`, `ActivePolicyProfile`, value objects and enums. No infrastructure dependencies.
- **Agentor.Application.Abstractions**: `IPolicyBundleRepository`, `IPolicyProfileRepository` interfaces.
- **Agentor.Infrastructure.Policy**: `PolicyBundleRulesAdapter` (converts bundle → `PolicyProfileRules`, no scope filtering in Phase 17), `InMemoryPolicyBundleRepository`, `InMemoryPolicyProfileRepository`.
- **Agentor.Infrastructure.RuntimePolicyEvaluator**: Bundle-aware via `IPolicyBundleRepository` + `IPolicyProfileRepository`. Falls back to `RuntimePolicyOptions` when no active profile is set.
- **Agentor.Contracts**: `PolicyBundleDtos.cs` — request/response DTOs.
- **Agentor.Api.Endpoints.PolicyBundleEndpoints**: Four management endpoints; no domain logic.
