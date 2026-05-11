# Current PR — harness marker

Completed: Phase 35 **PR138–PR142** (production integration smoke pack): **`IntegrationSmokeOptions`** + **`SmokeMode`** + **`SmokeTarget`**; **`IntegrationSmokeConfigurationMerger`** maps smoke modes onto **`Agentor:Integrations:*:*`** for the smoke process; **`IntegrationSmokeRunner`** (Athanor read + gated candidate submit, Conexus completion + declared budget + telemetry check, MCP list/discover/invoke, external-agent discover/invoke); **`IntegrationSmokeReportWriter`** + **`IntegrationFailureRedaction`**; **`tools/Agentor.IntegrationSmoke`** + **`scripts/run-integration-smoke.ps1`**; **`docs/operator/integration-smoke.md`**; **`docs/REPO_TRUTH.md`** smoke bullet; tests **`IntegrationSmokeTests`** (merger, fake end-to-end, redaction, report writer). Harness: **`phase` 35**, **`harnessPass` PR142**. Verification: restore/build/test on **`Agentor.sln`**; **`verify-harness`** ExpectedPhase **35** / **PR142**; **`verify-repo-clean`**.

Next: Phase 36 (release candidate consolidation) only when explicitly scheduled.

Do not start the next phase during closeout.
