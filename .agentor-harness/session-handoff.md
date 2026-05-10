# Session handoff — Phase 17 PR81–PR85

## Completed

Phase 17 Enterprise Policy Model (PR81–PR85) fully implemented and verified.

### PR81 — PolicyBundle domain model

New files in `src/Agentor.Domain/Policy/`:

- `PolicyBundleVersion.cs` — comparable, immutable version value object (`major.minor`)
- `PolicyRuleKind.cs`, `PolicyRuleScope.cs`, `PolicyRuleEffect.cs` — enums
- `PolicyRule.cs` — rule entity with factory helpers
- `PolicyBundle.cs` — aggregate root; versioned, immutable after `Publish()`; duplicate rule IDs rejected on `Create()`

### PR82 — PolicyProfile binding

- `PolicyProfileBinding.cs`, `PolicyProfile.cs`, `ActivePolicyProfile.cs` in `Agentor.Domain.Policy`
- `IPolicyBundleRepository.cs`, `IPolicyProfileRepository.cs` in `Agentor.Application.Abstractions`

### PR83 — Policy evaluation adapter

- `src/Agentor.Infrastructure/Policy/PolicyBundleRulesAdapter.cs`
- `src/Agentor.Infrastructure/Policy/InMemoryPolicyBundleRepository.cs`
- `src/Agentor.Infrastructure/Policy/InMemoryPolicyProfileRepository.cs`
- `RuntimePolicyOptions.cs` — added `RequiresReviewToolKeys` to `PolicyProfileRules`
- `RuntimePolicyEvaluator.cs` — rewritten with 2-constructor pattern; bundle-aware; 7-step evaluation; `RequiresReview` distinct from `Deny`
- `DependencyInjection.cs` — registers new repos

### PR84 — Policy management API

- `src/Agentor.Contracts/PolicyBundleDtos.cs`
- `src/Agentor.Api/Endpoints/PolicyBundleEndpoints.cs` — 4 endpoints
- `src/Agentor.Api/Program.cs` — registered `v1.MapPolicyBundleEndpoints()`
- `src/Agentor.Api/Mapping/DtoMappings.cs` — bundle/rule/active-profile mappings

### PR85 — Fixtures, audit, docs

- `tests/Agentor.Domain.Tests/Policy/PolicyBundleTests.cs` — 25 tests
- `tests/Agentor.Application.Tests/Policy/PolicyBundleEvaluationTests.cs` — 13 tests
- `tests/Agentor.Application.Tests/fixtures/policy/{allow,deny,review}-bundle.json`
- `GetRunAuditExportQueryHandler.cs` — `policyIdentity` section added
- `docs/developer/policy-bundles.md`

## Verification

298 tests passing, 0 failures across all 5 projects. Evidence in `artifacts/verification/`.

## What was not started

- Phase 18+ roadmap items
- EF Core persistence for PolicyBundle/PolicyProfile (in-memory only for now)
- Policy scope enforcement (Tenant/Workspace/Project scopes stored but not evaluated)

## Remaining risks

- `PolicyRuleScope` values are stored on rules but not yet enforced at evaluation — all rules are treated as Global by the adapter.
- The activate endpoint sources profiles from `IManagementPolicyProfileStore` (existing flat store); the new `PolicyProfile` domain type exists but has no dedicated creation endpoint yet.
