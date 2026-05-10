# Session handoff

## Completed (PR75.7)

- `verify-repo-clean.ps1`: full-repo text encoding and harness policy checks.
- `ci.yml`: `verify-harness` (ExpectedPhase 15, PR75.7) + `verify-repo-clean` after tests, before Docker.
- `AGENTS.md`: current boundaries; PR1 historical; closeout protocol.
- `src/Agentor.Api/Program.cs` + `Endpoints/*.cs`: endpoint wiring extracted; routes/tags unchanged.
- `.agentor-harness/*`: `current-pr`, `feature-list` (PR75.7 rows + ProfileId note fix), `progress`, `verification-log` (after verify), `session-handoff` (this file).
- `docs/RELEASE/v1.0-RC-DEFERRED-ITEMS.md` and new `v1.0-RC-REPO-STATUS.md`.
- UTF-8 no BOM on touched files; `verify-repo-clean` and `verify-harness` used as gate.

## Not started

- **Phase 16+** product roadmap features.
- **PR23-API-003 / PR24-API-003** still false: need running-run `WebApplicationFactory` fixture; not added in this cleanup pass.
- **PR52-004 / PR53-005** remain false: deferred to v1.1 per harness and deferred-items doc.

## Remaining risks / false acceptance

- See `feature-list.json` for all `passes: false` rows; four deferred items documented in `v1.0-RC-DEFERRED-ITEMS.md`.
- Broad encoding scan may surface legacy files in future edits; keep new text as UTF-8 no BOM.