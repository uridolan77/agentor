# Session handoff — Phase 17 PR85.5

## Completed

PR85.5 — Policy deferred-item reconciliation after Phase 17.

### PR52-004 closed

`feature-list.json` row `PR52-004` flipped from `passes: false` to `passes: true`. Phase 17 (PR81–PR85) fully implements the versioned enterprise policy model:

- `PolicyBundle` aggregate (versioned, immutable after publication, duplicate rule IDs rejected)
- `PolicyRule` with Kind/Scope/Effect taxonomy and factory helpers
- `PolicyProfile` + `ActivePolicyProfile` binding and runtime marker
- `PolicyBundleRulesAdapter` bridging bundle rules into `PolicyProfileRules`
- `IPolicyBundleRepository` + `IPolicyProfileRepository` abstractions + in-memory implementations
- 4 management API endpoints
- Audit export `policyIdentity` section
- 38 new tests; 3 fixture JSONs; `docs/developer/policy-bundles.md`

Engineering note added to `docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md`.

### SCOPE-001 documented

`PolicyRuleScope` (Global, Tenant, Workspace, Project) is modeled correctly on `PolicyRule` but is not enforced at evaluation time. `PolicyBundleRulesAdapter.ToProfileRules()` applies all rules regardless of scope.

Changes made to surface this clearly:

- `SCOPE-001` inline comment added to `PolicyBundleRulesAdapter.cs`
- XML doc `<remarks>` block added to the adapter class explaining the limitation
- "Known limitations (Phase 17)" section added to `docs/developer/policy-bundles.md`
- New item `SCOPE-001` (`passes: false`) added to `feature-list.json` and `v1.0-RC-DEFERRED-ITEMS.md`

### feature-list.json

- `phase`: 17 (unchanged)
- `harnessPass`: `PR85.5`
- Active `passes: false` items: `SCOPE-001`, `PR53-005`

## What was not started

- Phase 18 multi-step human review resume semantics was not started. No Phase 18 source files exist.
- No new evaluation semantics were introduced.
- No new API endpoints, auth/JWT, durable workers, or integrations.

## Remaining deferred items

- `SCOPE-001`: Tenant/Workspace/Project scope enforcement against run identity — deferred to v1.1.
- `PR53-005`: Multi-step plan executor resume semantics — deferred to v1.1.
