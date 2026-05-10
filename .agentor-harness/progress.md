# Agentor harness progress

## Phase 15 + PR75.6 (2026-05-10)

Completed **PR75.6** repository hygiene after **Phase 15 PR71-PR75** + **PR75.5** harness reconciliation.

- Removed tracked root scratch Python (`git rm` of `_*.py`, `write_phase9_payload.py`, and related one-off scripts).
- `.gitignore` root-only patterns for future `_*.py`, `write_*_payload.py`, scratch temp names, and disposable root folders (`scratch/`, `tmp/`, etc.).
- `scripts/verify-repo-clean.ps1` - root Python guard, UTF-8/BOM/null-byte scan for harness/scripts/.github/benchmarks/docs/src/tests plus root text files, harness shape checks, `passes=true` requires evidence.
- `scripts/run-benchmarks.ps1` - local `dotnet run -c Release` for BenchmarkDotNet (CI remains compile-only on `benchmarks/Agentor.Benchmarks`).
- Harness marker compacted in `current-pr.md`; `feature-list.json` harnessPass **PR75.6**; `docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md` lists every `passes: false` acceptance row with disposition notes.
- Tests: extended `JsonRedactionTests` / `RedactionPolicyTests`; expanded `ContractDtoCompatibilityTests` for representative public DTOs.
- **No** post-Phase 15 product features or new roadmap phases started.

Next harness marker: **post Phase 15** roadmap items only when explicitly scheduled.

## Phase 15 + PR75.5 (2026-05-10)

Completed **PR75.5** harness reconciliation after **Phase 15 PR71-PR75** v1.0 platform hardening.

- Redaction: `SensitiveFieldCatalog`, `RedactionPolicy`, `RedactionResult`, `JsonRedaction`; audit export and evaluation report JSON use structured key-name redaction; tests under `tests/Agentor.Application.Tests/Redaction/`; `docs/developer/phase15-redaction.md`.
- Performance: BenchmarkDotNet project `benchmarks/Agentor.Benchmarks`; `docs/developer/phase15-performance-baselines.md`.
- CI: `.github/workflows/ci.yml` — EF migrations list, evaluation test slice, Docker image build, TRX artifacts, benchmark Release compile.
- Upgrade readiness: `tests/Agentor.Contracts.Tests/ContractDtoCompatibilityTests.cs`; `docs/developer/CONTRACT_VERSIONING.md`; `docs/developer/MIGRATION_AND_UPGRADE.md`.
- RC artifacts: `docs/RELEASE/v1.0-RC.md`; `docs/ARCHITECTURE_BOUNDARY_REVIEW.md`; `docs/ROADMAP.md` Phase 15 note.
- Harness: `feature-list.json` phase **15**, harnessPass **PR75.5**, new acceptance rows with named evidence.

Next harness marker: **post Phase 15** roadmap items only when explicitly scheduled.

## Phase 14 + PR70.5 (2026-05-10)

Completed **PR70.5** harness reconciliation after **Phase 14 PR66-PR70** advanced evaluation work.

- Application evaluation layer: fixture registry (schema 4), harness fixture parsing, coordination profile materialization, quality rule sets (built-in JSON predicates only), coordination metrics from run/manifest artifacts, deterministic report outputs (Markdown/JSON/CSV) and CI artifact folder writer.
- Tests: `tests/Agentor.Application.Tests/Evaluation/` (`EvaluationFixtureRegistryTests`, `CoordinationProfileEvaluationTests`, `QualityRuleSetEvaluatorTests`, `EvaluationReportGeneratorTests`, plus existing harness tests).
- Documentation: `docs/developer/phase14-evaluation.md`.
- Harness: `feature-list.json` phase **14**, harnessPass **PR70.5**, new acceptance rows with named test/source evidence.

Next harness marker at the time: **Phase 15** when that phase is opened in planning docs.

## Phase 13 + PR65.5 (2026-05-10)

Completed **PR65.5** harness reconciliation after **Phase 13 PR61-PR65** product and operator surface work.

- Product API under /api/v1: management artifacts (recipes, plans, skills, policy-profiles), run inspection aliases (timeline, coordination-view, audit-packet), operator dashboard, review inbox aliases.
- Tests: Phase13ProductSurfaceApiTests (dashboard, management validation, timeline 404, audit-packet SHA vs audit-export, skill duplicate 409, reviews pending, review decision conflict on completed run).
- Documentation: docs/api/phase13-product-surface.md, docs/operator/dashboard-and-inbox.md, docs/developer/phase13-workflows.md, docs/examples/phase13-workflows.md.
- Harness: feature-list.json phase **13**, harnessPass **PR65.5**, expanded acceptance rows.

Next harness marker at the time: **Phase 14** (`PHASE_14_ADVANCED_EVALUATION.md`).