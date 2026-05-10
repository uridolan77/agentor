# Agentor harness progress

## Phase 15 + PR75.7 (2026-05-10)

Completed **PR75.7** repository tightening after **PR75.6** hygiene.

- Broadened `scripts/verify-repo-clean.ps1` to scan UTF-8/BOM/null bytes across `.agentor-harness/`, `.cursor/`, `scripts/`, `.github/workflows/`, `docs/`, `src/`, `tests/`, `benchmarks/`, optional `fixtures/`, and root policy globs.
- CI (`.github/workflows/ci.yml`) now runs `verify-harness.ps1` and `verify-repo-clean.ps1` after tests, before Docker.
- `AGENTS.md` updated: current architecture boundaries, historical PR1 section, closeout protocol (example `PR75.7`).
- `Program.cs` remains the composition root; route mappings live in `src/Agentor.Api/Endpoints/*.cs` (behavior-preserving).
- Harness: `feature-list.json` notes corrected (ProfileId vs explicit project identity after Phase 11), `harnessPass` **PR75.7**; `current-pr.md`, `verification-log.md`, `session-handoff.md` reconciled.
- Docs: `docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md` triage sharpened; `docs/RELEASE/v1.0-RC-REPO-STATUS.md` added.
- Normalized UTF-8 **no BOM** for files touched in this pass.

**Not started:** Phase 16+ roadmap / new product features (explicit schedule only).

Next harness marker: post–Phase 15 work when scheduled; do not start the next phase during closeout.