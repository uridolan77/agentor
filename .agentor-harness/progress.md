# Agentor harness progress

## Phase 17 PR81–PR85 (2026-05-10)

Completed **Phase 17 Enterprise Policy Model**:

- **PR81**: `PolicyBundle`, `PolicyBundleVersion`, `PolicyRule`, `PolicyRuleKind/Scope/Effect` domain model in `Agentor.Domain.Policy`. Versioned and immutable after publication. Duplicate rule IDs rejected.
- **PR82**: `PolicyProfile`, `PolicyProfileBinding`, `ActivePolicyProfile` domain types. `IPolicyBundleRepository` and `IPolicyProfileRepository` application abstractions.
- **PR83**: `PolicyBundleRulesAdapter` (bundle → `PolicyProfileRules`). `InMemoryPolicyBundleRepository`, `InMemoryPolicyProfileRepository`. `RuntimePolicyEvaluator` extended with bundle-aware 2-constructor pattern (3-param test constructor + 5-param DI constructor). `PolicyProfileRules.RequiresReviewToolKeys` added. `RequiresReview` remains distinct from `Deny`.
- **PR84**: `PolicyBundleDtos.cs` contracts. `PolicyBundleEndpoints.cs` (GET/POST bundles, POST activate). `Program.cs` registration. DI wiring in `DependencyInjection.cs`.
- **PR85**: `PolicyBundleTests.cs` (Domain.Tests — 25 new tests). `PolicyBundleEvaluationTests.cs` (Application.Tests — 13 new tests). Fixture JSONs: `allow-bundle.json`, `deny-bundle.json`, `review-bundle.json`. Audit export updated with `policyIdentity` section. `docs/developer/policy-bundles.md`.

Test totals: **298 passing, 0 failing** across all 5 test projects.

Evidence: `artifacts/verification/dotnet-{info,restore,build,test}.txt`.

## Phase 15 + PR75.8 (2026-05-10)

Completed **PR75.8** after **PR75.7**: closes Athanor API acceptance gaps **PR23-API-003** and **PR24-API-003** using `WebApplicationFactory` + `TestAgentRunRepository` to seed an `AgentRun` in **Running** (default POST `/api/v1/agent-runs` completes synchronously, so it cannot leave a running run).

- New tests: `tests/Agentor.Api.Tests/AthanorRunningRunApiTests.cs` (204 No Content on evidence-provenance, 202 Accepted on candidates).
- Support types: `tests/Agentor.Api.Tests/Support/TestAgentRunRepository.cs`, `AthanorRunningRunApiFixture.cs`.

**Not started:** Phase 16+ roadmap / v1.1 PolicyBundle / multi-step review resume (still tracked as false in harness where applicable).

Next harness marker: post–Phase 15 work when scheduled; do not start the next phase during closeout.
