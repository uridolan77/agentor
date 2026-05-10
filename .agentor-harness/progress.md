# Agentor harness progress

## Phase 14 + PR70.5 (2026-05-10)

Completed **PR70.5** harness reconciliation after **Phase 14 PR66-PR70** advanced evaluation work.

- Application evaluation layer: fixture registry (schema 4), harness fixture parsing, coordination profile materialization, quality rule sets (built-in JSON predicates only), coordination metrics from run/manifest artifacts, deterministic report outputs (Markdown/JSON/CSV) and CI artifact folder writer.
- Tests: `tests/Agentor.Application.Tests/Evaluation/` (`EvaluationFixtureRegistryTests`, `CoordinationProfileEvaluationTests`, `QualityRuleSetEvaluatorTests`, `EvaluationReportGeneratorTests`, plus existing harness tests).
- Documentation: `docs/developer/phase14-evaluation.md`.
- Harness: `feature-list.json` phase **14**, harnessPass **PR70.5**, new acceptance rows with named test/source evidence.

Next harness marker: **Phase 15** when that phase is opened in planning docs.

## Phase 13 + PR65.5 (2026-05-10)

Completed **PR65.5** harness reconciliation after **Phase 13 PR61-PR65** product and operator surface work.

- Product API under /api/v1: management artifacts (recipes, plans, skills, policy-profiles), run inspection aliases (timeline, coordination-view, audit-packet), operator dashboard, review inbox aliases.
- Tests: Phase13ProductSurfaceApiTests (dashboard, management validation, timeline 404, audit-packet SHA vs audit-export, skill duplicate 409, reviews pending, review decision conflict on completed run).
- Documentation: docs/api/phase13-product-surface.md, docs/operator/dashboard-and-inbox.md, docs/developer/phase13-workflows.md, docs/examples/phase13-workflows.md.
- Harness: feature-list.json phase **13**, harnessPass **PR65.5**, expanded acceptance rows.

Next harness marker at the time: **Phase 14** (`PHASE_14_ADVANCED_EVALUATION.md`).
